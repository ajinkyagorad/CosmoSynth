"""The body list for the shipped Solar System catalogue.

Each entry pairs a Horizons target code with the metadata Horizons does not supply in a
machine-readable form. Values that are hand-entered here carry an explicit citation, because
the whole point of the provenance system is that no number arrives without one.

Notes on the hierarchy:

* Planets are their own body centres (199, 299, 399, ...), not the system barycentres. A
  viewer expects Earth to be drawn where Earth is. The cost is that Earth's Keplerian fit
  carries the ~4700 km monthly wobble about the Earth-Moon barycentre; the importer measures
  that residual and records it rather than hiding it.
* Moons orbit their planet's body centre.
* Rotation elements come from the IAU WGCCRE 2015 report and already live in
  CosmoSynth.Core.BodyOrientation, so they are referenced by name rather than duplicated.
"""

from __future__ import annotations

from typing import Any, Dict, List

SUN = "500@10"


def _center(naif: int) -> str:
    """Horizons observer string for a body's centre."""
    return f"500@{naif}"


# Atmospheric parameters. Sources are noted per body; these are textbook/mission values, not
# scraped, so they are marked Measured with a citation rather than Inferred.
ATMOSPHERES: Dict[str, Dict[str, Any]] = {
    "venus": {
        "surfacePressureBar": 92.0,
        "scaleHeightKm": 15.9,
        "topAltitudeKm": 250.0,
        "surfaceDensityKgPerM3": 65.0,
        "meanMolecularMassKgPerMol": 0.0434,
        "surfaceTemperatureK": 737.0,
        "composition": {"CO2": 0.965, "N2": 0.035},
        "rayleighScatteringPerKm": [0.0195, 0.0155, 0.0125],
        "mieScatteringPerKm": 0.012,
        "_source": "NASA Venus Fact Sheet",
    },
    "earth": {
        "surfacePressureBar": 1.01325,
        "scaleHeightKm": 8.5,
        "topAltitudeKm": 100.0,
        "surfaceDensityKgPerM3": 1.225,
        "meanMolecularMassKgPerMol": 0.028964,
        "surfaceTemperatureK": 288.0,
        "composition": {"N2": 0.7808, "O2": 0.2095, "Ar": 0.0093, "CO2": 0.0004},
        "rayleighScatteringPerKm": [0.0058, 0.0135, 0.0331],
        "mieScatteringPerKm": 0.0021,
        "_source": "US Standard Atmosphere 1976; NASA Earth Fact Sheet",
    },
    "mars": {
        "surfacePressureBar": 0.00636,
        "scaleHeightKm": 11.1,
        "topAltitudeKm": 120.0,
        "surfaceDensityKgPerM3": 0.020,
        "meanMolecularMassKgPerMol": 0.04334,
        "surfaceTemperatureK": 210.0,
        "composition": {"CO2": 0.9532, "N2": 0.027, "Ar": 0.016, "O2": 0.0013},
        # Mars's sky is butterscotch because suspended dust dominates over Rayleigh
        # scattering, so the Mie term is large and the Rayleigh term nearly grey.
        "rayleighScatteringPerKm": [0.0060, 0.0045, 0.0035],
        "mieScatteringPerKm": 0.030,
        "_source": "NASA Mars Fact Sheet",
    },
    "jupiter": {
        "surfacePressureBar": 1000.0,
        "scaleHeightKm": 27.0,
        "topAltitudeKm": 1000.0,
        "surfaceDensityKgPerM3": 0.16,
        "meanMolecularMassKgPerMol": 0.00223,
        "surfaceTemperatureK": 165.0,
        "composition": {"H2": 0.898, "He": 0.102},
        "rayleighScatteringPerKm": [0.0045, 0.0105, 0.0250],
        "mieScatteringPerKm": 0.008,
        "_source": "NASA Jupiter Fact Sheet",
    },
    "saturn": {
        "surfacePressureBar": 1000.0,
        "scaleHeightKm": 59.5,
        "topAltitudeKm": 1500.0,
        "surfaceDensityKgPerM3": 0.19,
        "meanMolecularMassKgPerMol": 0.0023,
        "surfaceTemperatureK": 134.0,
        "composition": {"H2": 0.963, "He": 0.0325},
        "rayleighScatteringPerKm": [0.0050, 0.0100, 0.0200],
        "mieScatteringPerKm": 0.010,
        "_source": "NASA Saturn Fact Sheet",
    },
    "uranus": {
        "surfacePressureBar": 1000.0,
        "scaleHeightKm": 27.7,
        "topAltitudeKm": 1000.0,
        "surfaceDensityKgPerM3": 0.42,
        "meanMolecularMassKgPerMol": 0.00264,
        "surfaceTemperatureK": 76.0,
        # Methane absorbs strongly in the red, which is why Uranus and Neptune look blue.
        "composition": {"H2": 0.825, "He": 0.152, "CH4": 0.023},
        "rayleighScatteringPerKm": [0.0020, 0.0110, 0.0300],
        "mieScatteringPerKm": 0.004,
        "_source": "NASA Uranus Fact Sheet",
    },
    "neptune": {
        "surfacePressureBar": 1000.0,
        "scaleHeightKm": 19.7,
        "topAltitudeKm": 1000.0,
        "surfaceDensityKgPerM3": 0.45,
        "meanMolecularMassKgPerMol": 0.00269,
        "surfaceTemperatureK": 72.0,
        "composition": {"H2": 0.80, "He": 0.19, "CH4": 0.015},
        "rayleighScatteringPerKm": [0.0015, 0.0105, 0.0320],
        "mieScatteringPerKm": 0.004,
        "_source": "NASA Neptune Fact Sheet",
    },
    "titan": {
        "surfacePressureBar": 1.47,
        "scaleHeightKm": 21.0,
        "topAltitudeKm": 600.0,
        "surfaceDensityKgPerM3": 5.4,
        "meanMolecularMassKgPerMol": 0.02801,
        "surfaceTemperatureK": 94.0,
        "composition": {"N2": 0.943, "CH4": 0.049, "H2": 0.001, "Ar": 0.007},
        "rayleighScatteringPerKm": [0.0180, 0.0130, 0.0080],
        "mieScatteringPerKm": 0.025,
        "_source": "Cassini-Huygens HASI; NASA Titan Fact Sheet",
    },
    "pluto": {
        "surfacePressureBar": 1.0e-5,
        "scaleHeightKm": 50.0,
        "topAltitudeKm": 1600.0,
        "surfaceDensityKgPerM3": 1.0e-5,
        "meanMolecularMassKgPerMol": 0.02801,
        "surfaceTemperatureK": 44.0,
        "composition": {"N2": 0.99, "CH4": 0.005, "CO": 0.005},
        "rayleighScatteringPerKm": [0.0050, 0.0090, 0.0180],
        "mieScatteringPerKm": 0.002,
        "_source": "New Horizons REX/ALICE occultation results",
    },
}


# Fallback display colours, used before any texture streams in. Approximate integrated
# disc colours from spacecraft imagery; deliberately marked Inferred, not Measured.
DISPLAY_COLORS: Dict[str, List[float]] = {
    "sun": [1.00, 0.96, 0.88],
    "mercury": [0.55, 0.52, 0.49],
    "venus": [0.90, 0.82, 0.62],
    "earth": [0.25, 0.42, 0.60],
    "moon": [0.55, 0.54, 0.52],
    "mars": [0.72, 0.42, 0.28],
    "phobos": [0.42, 0.38, 0.35],
    "deimos": [0.45, 0.41, 0.37],
    "jupiter": [0.78, 0.70, 0.58],
    "io": [0.85, 0.76, 0.45],
    "europa": [0.78, 0.74, 0.68],
    "ganymede": [0.58, 0.54, 0.50],
    "callisto": [0.42, 0.39, 0.36],
    "saturn": [0.85, 0.78, 0.62],
    "enceladus": [0.92, 0.93, 0.94],
    "rhea": [0.72, 0.71, 0.70],
    "titan": [0.82, 0.66, 0.38],
    "iapetus": [0.50, 0.46, 0.42],
    "uranus": [0.62, 0.80, 0.83],
    "titania": [0.55, 0.51, 0.48],
    "oberon": [0.52, 0.48, 0.45],
    "neptune": [0.33, 0.47, 0.72],
    "triton": [0.80, 0.76, 0.72],
    "pluto": [0.72, 0.62, 0.53],
    "charon": [0.60, 0.58, 0.56],
    "ceres": [0.42, 0.40, 0.38],
    "vesta": [0.52, 0.48, 0.44],
    "pallas": [0.45, 0.44, 0.42],
    "eros": [0.50, 0.44, 0.38],
    "bennu": [0.28, 0.26, 0.25],
    "halley": [0.30, 0.29, 0.28],
}


# Explicit overrides for bodies whose Horizons constants block cannot be parsed safely.
#
# Horizons writes small-body masses as e.g. "Mass (10^20 kg ) = 1.08 (10^-4)", where the
# trailing parenthetical is a further multiplier. Misreading it is a factor of 10^4 error in
# the mass — the kind of mistake that produces a plausible-looking but silently wrong orbit.
# Rather than regex a format that ambiguous, these four bodies carry published values with
# citations. They are still Measured, just measured by someone whose paper we can name.
OVERRIDES: Dict[str, Dict[str, Any]] = {
    "phobos": {
        "gmKm3S2": 7.087546066894452e-4,
        "meanRadiusKm": 11.08,
        "equatorialRadiusKm": 13.0,
        "_source": "Jacobson (2010), Mars satellite ephemeris MAR097; radii from Willner et al. (2014)",
    },
    "deimos": {
        "gmKm3S2": 9.615569648120313e-5,
        "meanRadiusKm": 6.27,
        "equatorialRadiusKm": 7.8,
        "_source": "Jacobson (2010), Mars satellite ephemeris MAR097; radii from Thomas et al. (2000)",
    },
    "bennu": {
        "gmKm3S2": 4.892e-9,
        "meanRadiusKm": 0.2451,
        "equatorialRadiusKm": 0.2825,
        "_source": "Scheeres et al. (2019), OSIRIS-REx radio science, Nature Astronomy 3:352",
    },
    "halley": {
        # A comet nucleus is irregular, outgassing and poorly constrained; this GM is derived
        # from a nucleus mass of ~2.2e14 kg with an uncertainty of roughly a factor of two.
        # It is recorded as Inferred rather than Measured for that reason.
        #   GM = G * M = 6.6743e-20 km^3 kg^-1 s^-2 * 2.2e14 kg = 1.468e-5 km^3/s^2
        "gmKm3S2": 1.468e-5,
        "meanRadiusKm": 5.5,
        "equatorialRadiusKm": 8.0,
        "_inferred": True,
        "_source": "Giotto/Vega encounter results; nucleus mass ~2.2e14 kg, uncertain to ~2x",
    },
}


#: The catalogue. ``orientation`` names a static field on CosmoSynth.Core.BodyOrientation.
BODIES: List[Dict[str, Any]] = [
    # --- Star ---
    {"id": "sun", "name": "Sun", "command": "10", "type": "Star", "parent": None,
     "orientation": "Sun", "designations": ["NAIF 10", "Sol"],
     "luminosityWatts": 3.828e26, "effectiveTemperatureK": 5772.0, "spectralType": "G2V"},

    # --- Planets ---
    {"id": "mercury", "name": "Mercury", "command": "199", "type": "Planet", "parent": "sun",
     "center": SUN, "orientation": "Mercury", "designations": ["NAIF 199"], "bondAlbedo": 0.088},
    {"id": "venus", "name": "Venus", "command": "299", "type": "Planet", "parent": "sun",
     "center": SUN, "orientation": "Venus", "designations": ["NAIF 299"], "bondAlbedo": 0.76},
    {"id": "earth", "name": "Earth", "command": "399", "type": "Planet", "parent": "sun",
     "center": SUN, "orientation": "Earth", "designations": ["NAIF 399"], "bondAlbedo": 0.306},
    {"id": "mars", "name": "Mars", "command": "499", "type": "Planet", "parent": "sun",
     "center": SUN, "orientation": "Mars", "designations": ["NAIF 499"], "bondAlbedo": 0.25},
    {"id": "jupiter", "name": "Jupiter", "command": "599", "type": "Planet", "parent": "sun",
     "center": SUN, "orientation": "Jupiter", "designations": ["NAIF 599"], "bondAlbedo": 0.503},
    {"id": "saturn", "name": "Saturn", "command": "699", "type": "Planet", "parent": "sun",
     "center": SUN, "orientation": "Saturn", "designations": ["NAIF 699"], "bondAlbedo": 0.342},
    {"id": "uranus", "name": "Uranus", "command": "799", "type": "Planet", "parent": "sun",
     "center": SUN, "orientation": "Uranus", "designations": ["NAIF 799"], "bondAlbedo": 0.300},
    {"id": "neptune", "name": "Neptune", "command": "899", "type": "Planet", "parent": "sun",
     "center": SUN, "orientation": "Neptune", "designations": ["NAIF 899"], "bondAlbedo": 0.290},

    # --- Moons ---
    {"id": "moon", "name": "Moon", "command": "301", "type": "Moon", "parent": "earth",
     "center": _center(399), "orientation": "Moon", "designations": ["NAIF 301", "Luna"],
     "bondAlbedo": 0.11},
    {"id": "phobos", "name": "Phobos", "command": "401", "type": "Moon", "parent": "mars",
     "center": _center(499), "designations": ["NAIF 401"], "bondAlbedo": 0.071},
    {"id": "deimos", "name": "Deimos", "command": "402", "type": "Moon", "parent": "mars",
     "center": _center(499), "designations": ["NAIF 402"], "bondAlbedo": 0.068},
    {"id": "io", "name": "Io", "command": "501", "type": "Moon", "parent": "jupiter",
     "center": _center(599), "orientation": "Io", "designations": ["NAIF 501"], "bondAlbedo": 0.63},
    {"id": "europa", "name": "Europa", "command": "502", "type": "Moon", "parent": "jupiter",
     "center": _center(599), "orientation": "Europa", "designations": ["NAIF 502"], "bondAlbedo": 0.67},
    {"id": "ganymede", "name": "Ganymede", "command": "503", "type": "Moon", "parent": "jupiter",
     "center": _center(599), "orientation": "Ganymede", "designations": ["NAIF 503"], "bondAlbedo": 0.43},
    {"id": "callisto", "name": "Callisto", "command": "504", "type": "Moon", "parent": "jupiter",
     "center": _center(599), "orientation": "Callisto", "designations": ["NAIF 504"], "bondAlbedo": 0.22},
    {"id": "enceladus", "name": "Enceladus", "command": "602", "type": "Moon", "parent": "saturn",
     "center": _center(699), "orientation": "Enceladus", "designations": ["NAIF 602"], "bondAlbedo": 0.81},
    {"id": "rhea", "name": "Rhea", "command": "605", "type": "Moon", "parent": "saturn",
     "center": _center(699), "designations": ["NAIF 605"], "bondAlbedo": 0.48},
    {"id": "titan", "name": "Titan", "command": "606", "type": "Moon", "parent": "saturn",
     "center": _center(699), "orientation": "Titan", "designations": ["NAIF 606"], "bondAlbedo": 0.22},
    {"id": "iapetus", "name": "Iapetus", "command": "608", "type": "Moon", "parent": "saturn",
     "center": _center(699), "designations": ["NAIF 608"], "bondAlbedo": 0.25},
    {"id": "titania", "name": "Titania", "command": "703", "type": "Moon", "parent": "uranus",
     "center": _center(799), "designations": ["NAIF 703"], "bondAlbedo": 0.17},
    {"id": "oberon", "name": "Oberon", "command": "704", "type": "Moon", "parent": "uranus",
     "center": _center(799), "designations": ["NAIF 704"], "bondAlbedo": 0.14},
    {"id": "triton", "name": "Triton", "command": "801", "type": "Moon", "parent": "neptune",
     "center": _center(899), "orientation": "Triton", "designations": ["NAIF 801"], "bondAlbedo": 0.76},

    # --- Dwarf planets and small bodies ---
    {"id": "pluto", "name": "Pluto", "command": "999", "type": "DwarfPlanet", "parent": "sun",
     "center": SUN, "orientation": "Pluto", "designations": ["NAIF 999", "134340 Pluto"],
     "bondAlbedo": 0.72},
    {"id": "charon", "name": "Charon", "command": "901", "type": "Moon", "parent": "pluto",
     "center": _center(999), "designations": ["NAIF 901"], "bondAlbedo": 0.25},
    {"id": "ceres", "name": "Ceres", "command": "1;", "type": "DwarfPlanet", "parent": "sun",
     "center": SUN, "orientation": "Ceres", "designations": ["1 Ceres", "A899 OF"], "bondAlbedo": 0.09},
    {"id": "vesta", "name": "Vesta", "command": "4;", "type": "Asteroid", "parent": "sun",
     "center": SUN, "orientation": "Vesta", "designations": ["4 Vesta", "A807 FA"], "bondAlbedo": 0.18},
    {"id": "pallas", "name": "Pallas", "command": "2;", "type": "Asteroid", "parent": "sun",
     "center": SUN, "designations": ["2 Pallas", "A802 FA"], "bondAlbedo": 0.10},
    {"id": "eros", "name": "Eros", "command": "433;", "type": "Asteroid", "parent": "sun",
     "center": SUN, "designations": ["433 Eros", "A898 PA"], "bondAlbedo": 0.16},
    {"id": "bennu", "name": "Bennu", "command": "101955;", "type": "Asteroid", "parent": "sun",
     "center": SUN, "designations": ["101955 Bennu", "1999 RQ36"], "bondAlbedo": 0.017},
    {"id": "halley", "name": "1P/Halley", "command": "90000030", "type": "Comet", "parent": "sun",
     "center": SUN, "designations": ["1P/Halley", "1P"], "bondAlbedo": 0.04},
]
