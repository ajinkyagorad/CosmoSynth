"""Thin client for the JPL Horizons API.

Horizons is an online ephemeris service, not a bulk database. It is the right source for
"where was this body at this instant", but it must never be called from the running
application: every query is a round trip to JPL, rate-limited and occasionally slow. The
architecture is therefore always the same — fetch offline, resample, ship a compact local
file, interpolate at runtime.

Reference: https://ssd.jpl.nasa.gov/horizons/manual.html
API notes:  https://ssd-api.jpl.nasa.gov/doc/horizons.html
"""

from __future__ import annotations

import json
import re
import time
import urllib.parse
import urllib.request
from dataclasses import dataclass, field
from typing import Any, Dict, Iterable, List, Optional

API_URL = "https://ssd.jpl.nasa.gov/api/horizons.api"

#: JPL asks that automated clients keep their request rate modest.
DEFAULT_REQUEST_DELAY_SECONDS = 1.0

#: Reference frames Horizons can express results in.
#:   FRAME    - ICRF/J2000 equatorial. The frame SPICE, Gaia and star catalogues use.
#:   ECLIPTIC - ecliptic and mean equinox of the reference epoch. Familiar orbital elements.
REF_PLANE_ICRF = "FRAME"
REF_PLANE_ECLIPTIC = "ECLIPTIC"


class HorizonsError(RuntimeError):
    """Raised when Horizons rejects a query or returns something unparseable."""


@dataclass
class VectorSample:
    """A single position/velocity sample, in km and km/s."""

    julian_date_tdb: float
    calendar: str
    position_km: List[float]
    velocity_km_s: List[float]

    def to_json(self) -> Dict[str, Any]:
        return {
            "jdTdb": self.julian_date_tdb,
            "calendar": self.calendar,
            "positionKm": self.position_km,
            "velocityKmPerSec": self.velocity_km_s,
        }


@dataclass
class ElementSample:
    """Osculating elements as Horizons reports them, in km, seconds and degrees."""

    julian_date_tdb: float
    calendar: str
    values: Dict[str, float] = field(default_factory=dict)

    def to_json(self) -> Dict[str, Any]:
        return {
            "jdTdb": self.julian_date_tdb,
            "calendar": self.calendar,
            "values": self.values,
        }


def _request(params: Dict[str, str], timeout: float = 90.0, require_ephemeris: bool = True) -> str:
    """Issue one Horizons query and return the raw text payload.

    ``require_ephemeris`` must be False for constants-only queries (MAKE_EPHEM='NO'), which
    legitimately return no $$SOE block. Horizons signals errors in prose with a 200 status, so
    checking for the expected block is the only reliable way to notice a failed query — but
    applying that check to a query that never asks for one rejects every valid response.
    """
    query = {"format": "text"}
    # Horizons wants most values wrapped in single quotes; doing it here keeps callers clean.
    for key, value in params.items():
        query[key] = value if key == "format" else f"'{value}'"

    url = f"{API_URL}?{urllib.parse.urlencode(query)}"
    try:
        with urllib.request.urlopen(url, timeout=timeout) as response:
            payload = response.read().decode("utf-8", errors="replace")
    except Exception as exc:  # noqa: BLE001 - surfaced verbatim to the caller
        raise HorizonsError(f"Horizons request failed: {exc}\n  url: {url}") from exc

    def fail(reason: str) -> None:
        snippet = payload.strip()
        if len(snippet) > 900:
            snippet = snippet[:900] + " ..."
        raise HorizonsError(f"{reason}\n  url: {url}\n  said: {snippet}")

    if require_ephemeris:
        if "$$SOE" not in payload:
            fail("Horizons returned no ephemeris block.")
    elif "No matches found" in payload or "Cannot interpret" in payload:
        fail("Horizons could not resolve the target.")

    return payload


def _extract_block(payload: str) -> List[str]:
    """Return the lines between the $$SOE / $$EOE markers."""
    match = re.search(r"\$\$SOE\n(.*?)\$\$EOE", payload, re.DOTALL)
    if not match:
        raise HorizonsError("Could not locate the $$SOE/$$EOE ephemeris block.")
    return [line for line in match.group(1).splitlines() if line.strip()]


_NUMBER = r"[-+]?\d*\.?\d+(?:[EeDd][-+]?\d+)?"


def _to_float(text: str) -> float:
    # Horizons occasionally emits Fortran-style D exponents.
    return float(text.replace("D", "E").replace("d", "e"))


def fetch_vectors(
    body: str,
    center: str,
    start_time: str,
    stop_time: str,
    step_size: str,
    ref_plane: str = REF_PLANE_ICRF,
) -> List[VectorSample]:
    """Fetch state vectors for ``body`` relative to ``center``.

    ``VEC_TABLE='2'`` selects the position+velocity table; ``OUT_UNITS='KM-S'`` matches the
    unit convention used throughout CosmoSynth.Core, so nothing needs converting downstream.
    """
    payload = _request(
        {
            "COMMAND": body,
            "EPHEM_TYPE": "VECTORS",
            "CENTER": center,
            "START_TIME": start_time,
            "STOP_TIME": stop_time,
            "STEP_SIZE": step_size,
            "VEC_TABLE": "2",
            "OUT_UNITS": "KM-S",
            "REF_PLANE": ref_plane,
            "REF_SYSTEM": "ICRF",
            "VEC_CORR": "NONE",  # geometric states, not light-time corrected
            "CSV_FORMAT": "NO",
        }
    )

    samples: List[VectorSample] = []
    lines = _extract_block(payload)

    # Records arrive as three lines: an epoch, then X/Y/Z, then VX/VY/VZ.
    index = 0
    while index < len(lines):
        header = lines[index].strip()
        epoch_match = re.match(rf"({_NUMBER})\s*=\s*A\.D\.\s*(.+?)\s+TDB", header)
        if not epoch_match:
            index += 1
            continue

        if index + 2 >= len(lines):
            break

        position = [_to_float(v) for v in re.findall(rf"[XYZ]\s*=\s*({_NUMBER})", lines[index + 1])]
        velocity = [_to_float(v) for v in re.findall(rf"V[XYZ]\s*=\s*({_NUMBER})", lines[index + 2])]

        if len(position) == 3 and len(velocity) == 3:
            samples.append(
                VectorSample(
                    julian_date_tdb=_to_float(epoch_match.group(1)),
                    calendar=epoch_match.group(2).strip(),
                    position_km=position,
                    velocity_km_s=velocity,
                )
            )
        index += 3

    if not samples:
        raise HorizonsError(f"Parsed no vector samples for body {body!r}.")
    return samples


def fetch_elements(
    body: str,
    center: str,
    start_time: str,
    stop_time: str,
    step_size: str,
    ref_plane: str = REF_PLANE_ECLIPTIC,
) -> List[ElementSample]:
    """Fetch osculating orbital elements for ``body`` relative to ``center``.

    Horizons labels these EC, QR, IN, OM, W, Tp, N, MA, TA, A, AD and PR. They are JPL's own
    conversion of the same underlying state vector, which makes them an independent check on
    our state-to-elements code rather than merely a restatement of it.
    """
    payload = _request(
        {
            "COMMAND": body,
            "EPHEM_TYPE": "ELEMENTS",
            "CENTER": center,
            "START_TIME": start_time,
            "STOP_TIME": stop_time,
            "STEP_SIZE": step_size,
            "OUT_UNITS": "KM-S",
            "REF_PLANE": ref_plane,
            "REF_SYSTEM": "ICRF",
            "CSV_FORMAT": "NO",
        }
    )

    samples: List[ElementSample] = []
    lines = _extract_block(payload)

    current: Optional[ElementSample] = None
    for line in lines:
        epoch_match = re.match(rf"\s*({_NUMBER})\s*=\s*A\.D\.\s*(.+?)\s+TDB", line)
        if epoch_match:
            if current is not None:
                samples.append(current)
            current = ElementSample(
                julian_date_tdb=_to_float(epoch_match.group(1)),
                calendar=epoch_match.group(2).strip(),
            )
            continue

        if current is None:
            continue

        for key, value in re.findall(rf"([A-Za-z]+)\s*=\s*({_NUMBER})", line):
            current.values[key] = _to_float(value)

    if current is not None:
        samples.append(current)

    if not samples:
        raise HorizonsError(f"Parsed no element samples for body {body!r}.")
    return samples


#: Separator between a label and its value. Horizons writes the unit either after a comma
#: ("GM, km^3/s^2 =") or in brackets ("GM (km^3/s^2) ="), and small bodies drop it entirely
#: ("GM= 62.6284"), so the separator has to absorb all three.
_UNITS = r"[^=\n]{0,32}?"


def fetch_body_constants(body: str) -> Dict[str, float]:
    """Scrape the physical-constants header Horizons prints for a body.

    That header is free-form prose laid out for human eyes, and its shape differs by body
    class — a planet, the Sun, a moon and an asteroid each label the same quantity
    differently:

        Earth   GM, km^3/s^2             = 398600.435436
        Io      GM (km^3/s^2)            = 5959.9155+- 0.004
        Ceres   GM= 62.6284

    So the extraction is a set of alternative patterns per quantity, tried in order of
    specificity. It is best-effort by design: anything unrecognised is simply absent from the
    result rather than guessed at, and the caller decides what to do about a missing value.
    Silently defaulting a mass or a radius would push a fabricated number into every orbit
    that depends on it.
    """
    payload = _request(
        {
            "COMMAND": body,
            "OBJ_DATA": "YES",
            "MAKE_EPHEM": "NO",
        },
        require_ephemeris=False,
    )

    patterns = {
        # The negative lookahead keeps "GM 1-sigma, km^3/s^2 = 0.0014" from being read as GM.
        "gmKm3S2": [
            rf"\bGM(?!\s*1-sigma){_UNITS}=\s*({_NUMBER})",
        ],
        "meanRadiusKm": [
            rf"Vol\.?\s*mean\s*radius{_UNITS}=\s*({_NUMBER})",
            rf"\bMean\s*radius{_UNITS}=\s*({_NUMBER})",
            rf"\bRAD=\s*({_NUMBER})",
            rf"Radius\s*\(photosphere\){_UNITS}=\s*({_NUMBER})",
            # Bare "Radius (km) = ..." is how the outer moons are labelled. Tried last so it
            # cannot shadow the more specific forms above. Note that for an irregular body
            # this line reads "Radius (km) = 13.1 x11.1 x9.3" and the pattern picks up only
            # the longest semi-axis — which is why bodies.py overrides the small irregulars
            # with published mean radii instead of relying on this.
            rf"\bRadius{_UNITS}=\s*({_NUMBER})",
        ],
        # Horizons abbreviates this three ways and the first version of this pattern only
        # allowed two of them. "Equat. radius (1 bar)" is how all four giant planets are
        # labelled, so none of them matched, the caller fell back to the volumetric mean, and
        # Jupiter shipped with an equatorial radius of 69911 km instead of 71492 — which,
        # combined with the polar radius never being read at all, made every gas giant
        # perfectly spherical. One optional-group pattern covers Equ., Equat. and Equatorial.
        "equatorialRadiusKm": [
            rf"Equ(?:at(?:orial)?)?\.?\s*radius{_UNITS}=\s*({_NUMBER})",
        ],
        "polarRadiusKm": [
            rf"Polar\s*radius{_UNITS}=\s*({_NUMBER})",
        ],
        # Published directly for the giants, and worth having as an independent check on the
        # pair above rather than as a source: a and c are what the renderer needs.
        #
        # Two spellings, and only one of them is a number. The giants print "Flattening =
        # 0.06487" while Earth and Mars print "Flattening = 1/298.257", so a single pattern
        # reads Earth's as 1 — a body flattened to a disc, which is exactly the sort of
        # plausible-looking wrong value this scraper exists to avoid producing. The reciprocal
        # form is captured separately and inverted below.
        "_flatteningReciprocal": [
            rf"Flattening{_UNITS}=\s*1\s*/\s*({_NUMBER})",
        ],
        "flattening": [
            rf"Flattening{_UNITS}=\s*({_NUMBER})",
        ],
        "siderealRotationPeriodDays": [
            rf"Sidereal\s*rot\.?\s*period{_UNITS}=\s*({_NUMBER})\s*d\b",
        ],
        "siderealRotationPeriodHours": [
            rf"Sidereal\s*rot\.?\s*period{_UNITS}=\s*({_NUMBER})\s*h\b",
            rf"\bROTPER=\s*({_NUMBER})",
        ],
        "obliquityToOrbitDeg": [
            rf"Obliquity\s*to\s*orbit{_UNITS}=\s*({_NUMBER})",
        ],
        "geometricAlbedo": [
            rf"Geometric\s*[Aa]lbedo{_UNITS}=\s*({_NUMBER})",
            rf"\bALBEDO=\s*({_NUMBER})",
        ],
        "densityGCm3": [
            rf"\bDensity{_UNITS}=\s*({_NUMBER})",
            rf"Mean\s*density{_UNITS}=\s*({_NUMBER})",
        ],
    }

    result: Dict[str, float] = {}
    for name, candidates in patterns.items():
        for pattern in candidates:
            match = re.search(pattern, payload, re.IGNORECASE)
            if match:
                try:
                    result[name] = _to_float(match.group(1))
                except ValueError:
                    continue
                break

    # "1/298.257" is the same quantity written the other way up. Resolved here so callers see
    # one field with one meaning, and so the direct pattern -- which would otherwise read the
    # leading 1 -- can never win for a body that uses the reciprocal form.
    denominator = result.pop("_flatteningReciprocal", None)
    if denominator:
        result["flattening"] = 1.0 / denominator

    return result


def polite_sleep(seconds: float = DEFAULT_REQUEST_DELAY_SECONDS) -> None:
    """Pause between requests so a batch import stays a good citizen of JPL's service."""
    time.sleep(seconds)


def dump_json(path: str, payload: Any) -> None:
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=2)
        handle.write("\n")
