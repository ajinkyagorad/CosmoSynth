"""The mission list: which spacecraft are shipped, where they are, and how big they are.

Three ways of saying where something is, because spacecraft move in three quite different ways
and forcing them all through one mechanism gets two of them badly wrong.

``SAMPLED``
    A table of state vectors from Horizons, Hermite-interpolated at runtime. Correct for
    *smooth* motion: heliocentric cruise, an escape hyperbola, a halo orbit about a Lagrange
    point. Webb and SOHO have no choice — a halo is a three-body path that no conic resembles —
    and Dawn thrusted continuously for years, which is a standing violation of the assumption
    elements are derived under.

    **A uniform table has exactly one step, and it does not resolve a flyby.** This is the
    limitation of the mechanism, and it used to be claimed here as a strength — that sampling
    "captures the planetary encounters that reshaped these paths". It does not. The step has to
    span the whole mission, so Voyager 1 gets 57 years in 900 samples and a gravity assist that
    reshapes its trajectory in hours falls between two of them.

    Measured against a six-hour Horizons reference across the encounter itself, the worst error
    of the shipped tables is:

        Voyager 1 at Jupiter, March 1979      3.6 million km
        Pioneer 10 at Jupiter, December 1973  5.7 million km
        Ulysses at Jupiter, February 1992     0.8 million km

    Away from the encounters the same tables are good to a median of 0.3 to 52 km, and Webb's
    halo — the case the mechanism actually exists for — to 8.5 km. Five to seven orders of
    magnitude between the two regimes, in one table, with one step.

    The cadences were nevertheless too coarse for no reason: every one of them was between 1.25
    and 5.8 times below what MAX_SAMPLES allowed. Spending the budget cut the encounter errors
    by 2.4 to 4.8 times and the cruise errors by about ten, for roughly a megabyte. Note how far
    short of the fourth-order prediction that is — a 5.8-fold finer step should be a thousand
    times better and delivers three — because convergence order is a property of smooth
    functions and a flyby is not one.

    Fixing it properly needs non-uniform sampling, which the shipped format deliberately does
    not support because storing sample times triples the file. So the encounters remain wrong by
    millions of kilometres, the figure is written into each mission's provenance, and nothing
    here claims otherwise.

``KEPLER``
    Osculating elements at an epoch, as the planets use. Correct for anything in a tight orbit
    about one body, and specifically *better* than sampling for eccentric ones.

    This is the counter-intuitive part and it is worth stating. Uniform sampling in time puts
    fewest samples where the object moves fastest, which is exactly periapsis. Juno's orbit
    runs from 75 000 km at perijove to 8 million km at apojove; at any cadence cheap enough to
    ship, it travels millions of kilometres between consecutive samples near Jupiter, and the
    interpolated path cuts the corner straight through the planet. A conic is an approximation
    to Juno's real orbit, but it is an approximation that stays outside Jupiter. The error a
    conic makes is a precession of the orbit; the error a coarse table makes is geometry that
    is physically impossible.

    **That precession is not always slow, and calling it slow was the mistake here.** Close to
    an oblate primary it is fast: the ISS's node turns 4.95 degrees a day and Hubble's 6.8, so
    within a fortnight of the epoch the drawn orbital plane is visibly wrong and within three
    months it is arbitrary. The conic is still the right *shape* — the altitude and the period
    stay honest for years — but where the spacecraft is on it, and which way the plane faces,
    are not. That is why each of these carries a window derived from its own precession rate
    rather than from how long the mission lasted.

``SITE``
    A published latitude and longitude, rotated into place by the same IAU model the host
    planet is drawn with. Exact, six numbers, valid at every epoch — where sampling a landing
    site would need dozens of points per Martian day and still drift.

Every entry also carries the window over which its placement means anything. Elements and
landing sites evaluate happily forever, so without a window Cassini would still be circling
Saturn nine years after it entered the atmosphere. Outside its window a spacecraft is not
drawn at all; wind the clock back and it reappears.

**The dates below are the outer bound, not the window.** They say when the spacecraft existed
and was operating, which is a different and much weaker claim than knowing where it was.
``build_spacecraft.py`` narrows them per placement: a sampled table is clipped to its own first
and last sample, and a conic to one full revolution of its fastest precessing angle either side
of its epoch. That second rule is the one that bites. The ISS's node regresses 4.95 degrees a
day under Earth's oblateness, so its elements are worth about 73 days, not the 27 years the
mission dates would allow — and drawing it in an arbitrary plane for the other 26.8 years was
the same class of error as drawing a Cassini that had already burned up.

A consequence worth stating: **the close orbiters have a shelf life.** Their epochs are set
near the build date so the derived window straddles it, and a few months after an import the
Earth and Mars orbiters will stop being drawn until it is run again. That is the honest
behaviour — the alternative is drawing them somewhere they are not — but it means this file
goes stale in a way the planets never do.

**On the sizes.** ``length_metres`` is a published dimension, applied by scaling the model's
longest axis to match. Whether a given export includes a deployed boom is not something the
file records, so the dimension is a measurement and the mapping is an assumption: the importer
records the size as Inferred and names the dimension it used. Getting it wrong makes a
spacecraft the wrong size, which is a presentational error — the position stays measured.

The NAIF identifiers and every coverage window below were verified against Horizons rather
than assumed. ``-25``, for instance, is Lunar Prospector, not Magellan.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Dict, List, Optional
from urllib.parse import quote

#: Base of the NASA 3D Resources model repository.
MODEL_REPO = "https://github.com/nasa/NASA-3D-Resources"
MODEL_RAW = "https://raw.githubusercontent.com/nasa/NASA-3D-Resources/master/3D%20Models"

#: NASA publishes these models into the public domain, subject to the usage guidelines below.
MODEL_LICENCE = "NASA 3D Resources (public domain; see NASA media usage guidelines)"
MODEL_LICENCE_URL = "https://www.nasa.gov/nasa-brand-center/images-and-media/"

#: Placement kinds.
SAMPLED = "sampled"
KEPLER = "kepler"
SITE = "site"

#: A sampled trajectory past this many points is a sign the cadence is wrong for the motion,
#: not that the mission deserves more data. The importer refuses rather than shipping it.
MAX_SAMPLES = 900

#: ...and every sampled mission should spend it. The steps below are the finest whole number of
#: days that fits inside the cap over each mission's span, which is not what they used to be:
#: every table was between 1.25 and 5.8 times coarser than the budget allowed, for no reason
#: anyone had written down. Hermite interpolation is fourth order, so Pioneer 10 going from 150
#: to 26 days is a thousandfold reduction in interpolation error and Voyager's 60 to 24 is
#: fortyfold, at the cost of roughly doubling the catalogue to about a megabyte. That does not
#: make a flyby resolvable — see the module docstring — but there is no argument for being
#: wrong by more than the format requires.

#: Epoch for the osculating elements of anything still flying.
#:
#: Set near the build date deliberately. A conic is only worth anything for a bounded span
#: about its epoch (see the module docstring), so an epoch chosen for tidiness rather than for
#: recency produces a catalogue whose close orbiters are already outside their own window on
#: the day it ships. Move this forward whenever the importer is re-run.
RECENT_EPOCH = "2026-07-31"

#: Second zonal harmonic of every body a spacecraft is placed against, dimensionless.
#:
#: Oblateness is what decides how long an osculating element set means anything, and it is the
#: only perturbation with a closed form cheap enough to apply here. It lives in the mission
#: table rather than in the Solar System catalogue because nothing else in the application
#: needs it: the bodies are drawn as spheres and propagated as two-body conics.
#:
#: A body absent from this table has its window bounded by its mission dates instead, which is
#: correct for anything whose orbit is limited by third bodies or by manoeuvring rather than by
#: the primary's shape.
J2 = {
    # Earth: EGM2008 / WGS 84, the standard geodetic value.
    "earth": 1.08262668e-3,
    # Moon: GRAIL GRGM1200A (Lemoine et al. 2014).
    "moon": 2.0330530e-4,
    # Mars: MRO110B gravity field (Konopliv et al. 2011). Twice Earth's, which is why the
    # Mars orbiters' element sets go stale as fast as the Earth ones.
    "mars": 1.95545e-3,
    # Jupiter: Juno gravity science (Iess et al., Nature 555, 2018).
    "jupiter": 1.4696e-2,
    # Saturn: Cassini Grand Finale (Iess et al., Science 364, 2019).
    "saturn": 1.6291e-2,
    # The Sun's J2 is about 2.2e-7 and is deliberately absent: at Parker's semi-major axis it
    # moves the node by under a milliarcsecond a century, so including it would imply the
    # window was derived from something that mattered.
}


def horizons_center(naif: int) -> str:
    """Horizons observer string for a body centre."""
    return f"500@{naif}"


@dataclass
class Mission:
    """One spacecraft: what it is, where the data comes from, and how to draw it."""

    id: str
    name: str
    placement: str

    #: Catalogue id of the body this is stored relative to. Must exist in solar_system.json.
    parent: str

    #: Directory and file name inside NASA-3D-Resources/3D Models.
    model_dir: str
    model_file: str

    #: Published overall dimension, metres, mapped onto the model's longest axis.
    length_metres: float

    #: What that dimension actually is, e.g. "solar array span". Recorded in the provenance.
    length_note: str

    #: One line for the selection panel.
    summary: str

    #: Window over which the placement is claimed to be valid, ISO dates.
    known_from: str
    known_until: str

    triangle_budget: int = 6000

    # ---- Flying missions ------------------------------------------------------------
    naif_id: Optional[int] = None
    center: Optional[str] = None

    #: SAMPLED only: the span and cadence to fetch.
    start: Optional[str] = None
    stop: Optional[str] = None
    step: Optional[str] = None

    #: KEPLER only: the instant the osculating elements are taken at.
    epoch: Optional[str] = None

    # ---- Landed missions ------------------------------------------------------------
    latitude_deg: Optional[float] = None
    longitude_deg: Optional[float] = None
    altitude_km: float = 0.0
    site_name: str = ""
    site_reference: str = ""

    #: How the mission ended. Empty while it is still working.
    ended: str = ""

    @property
    def model_url(self) -> str:
        return f"{MODEL_RAW}/{quote(self.model_dir)}/{quote(self.model_file)}"


MISSIONS: List[Mission] = [
    # ==================================================================================
    # Leaving the solar system. Smooth hyperbolic escape; sampling captures the planetary
    # encounters that reshaped these paths, which no single conic can.
    # ==================================================================================
    Mission(
        id="voyager-1", name="Voyager 1", placement=SAMPLED, parent="sun",
        naif_id=-31, center=horizons_center(10),
        start="1977-09-06", stop="2035-01-01", step="24 d",
        known_from="1977-09-06", known_until="2035-01-01",
        model_dir="Voyager Probe (A)", model_file="Voyager Probe (A).glb",
        length_metres=13.0, length_note="magnetometer boom span",
        summary="In interstellar space since 2012; the most distant human object.",
        triangle_budget=7000),
    Mission(
        id="voyager-2", name="Voyager 2", placement=SAMPLED, parent="sun",
        naif_id=-32, center=horizons_center(10),
        start="1977-08-21", stop="2035-01-01", step="24 d",
        known_from="1977-08-21", known_until="2035-01-01",
        model_dir="Voyager Probe (B)", model_file="Voyager Probe (B).glb",
        length_metres=13.0, length_note="magnetometer boom span",
        summary="The only spacecraft to have visited Uranus and Neptune.",
        triangle_budget=7000),
    Mission(
        id="pioneer-10", name="Pioneer 10", placement=SAMPLED, parent="sun",
        naif_id=-23, center=horizons_center(10),
        start="1972-03-04", stop="2035-01-01", step="26 d",
        known_from="1972-03-04", known_until="2035-01-01",
        model_dir="Pioneer 10", model_file="Pioneer 10.glb",
        length_metres=9.1, length_note="magnetometer boom span",
        summary="First through the asteroid belt and past Jupiter; silent since 2003.",
        ended="Last signal received on 23 January 2003.",
        triangle_budget=4000),

    # ==================================================================================
    # Lagrange points. Not orbits about anything — three-body paths that only a table can
    # describe — and slow enough that a table describes them very well.
    # ==================================================================================
    Mission(
        id="jwst", name="James Webb Space Telescope", placement=SAMPLED, parent="earth",
        naif_id=-170, center=horizons_center(399),
        # JPL's Webb ephemeris runs 2021-12-25 to 2031-07-27. Asking past the end returns no
        # data at all rather than a short answer, so the stop is inside it deliberately.
        start="2022-01-25", stop="2031-07-01", step="4 d",
        known_from="2022-01-25", known_until="2031-07-01",
        model_dir="James Webb Space Telescope (B)",
        model_file="James Webb Space Telescope (B).glb",
        length_metres=21.2, length_note="sunshield long axis",
        summary="Halo orbit about Earth-Sun L2, 1.5 million km out, passively cooled to 50 K.",
        triangle_budget=9000),
    Mission(
        id="soho", name="SOHO", placement=SAMPLED, parent="earth",
        naif_id=-21, center=horizons_center(399),
        # SOHO is still operating; JPL's ephemeris for it simply stops in July 2026. The window
        # records what is known, not how long the spacecraft is expected to last.
        start="2015-01-01", stop="2026-07-01", step="5 d",
        known_from="2015-01-01", known_until="2026-07-01",
        model_dir="Solar and Heliospheric Observatory",
        model_file="Solar and Heliospheric Observatory.glb",
        length_metres=9.5, length_note="solar array span",
        summary="Watching the Sun from the Earth-Sun L1 point since 1996.",
        triangle_budget=6000),

    # ==================================================================================
    # Heliocentric cruise. Low eccentricity or long period, so uniform sampling is faithful.
    # ==================================================================================
    Mission(
        id="ulysses", name="Ulysses", placement=SAMPLED, parent="sun",
        naif_id=-55, center=horizons_center(10),
        start="1990-10-07", stop="2009-07-01", step="8 d",
        known_from="1990-10-07", known_until="2009-06-30",
        model_dir="Ulysses", model_file="Ulysses.glb",
        length_metres=3.2, length_note="overall width across the antenna",
        summary="Flew over the Sun's poles by slingshotting off Jupiter.",
        ended="Switched off on 30 June 2009.",
        triangle_budget=4000),
    Mission(
        id="osiris-rex", name="OSIRIS-REx / APEX", placement=SAMPLED, parent="sun",
        naif_id=-64, center=horizons_center(10),
        start="2016-09-09", stop="2030-01-01", step="6 d",
        known_from="2016-09-09", known_until="2030-01-01",
        model_dir=("Origins, Spectral Interpretation, Resource Identification, and Security - "
                   "Regolith Explorer (OSIRIS-REx)"),
        model_file="OSIRIS-REx.glb",
        length_metres=6.2, length_note="solar array span",
        summary="Returned a sample of Bennu in 2023; now on its way to Apophis.",
        triangle_budget=6000),
    Mission(
        id="dawn", name="Dawn", placement=SAMPLED, parent="sun",
        naif_id=-203, center=horizons_center(10),
        # Dawn's ephemeris begins at 12:40 on launch day, so a start of 00:00 that day falls
        # outside it and Horizons returns nothing.
        start="2007-09-28", stop="2018-10-31", step="5 d",
        known_from="2007-09-28", known_until="2018-10-31",
        model_dir="Dawn", model_file="Dawn.glb",
        length_metres=19.7, length_note="solar array span",
        summary="Orbited two worlds — Vesta, then Ceres — under continuous ion thrust.",
        ended="Ran out of hydrazine and fell silent on 31 October 2018.",
        triangle_budget=6000),

    # ==================================================================================
    # Close orbiters. Elements, for the reason argued in the module docstring: a conic is a
    # worse fit to the truth than a dense table would be, and a far better one than any
    # table cheap enough to ship.
    # ==================================================================================
    Mission(
        id="iss", name="International Space Station", placement=KEPLER, parent="earth",
        naif_id=-125544, center=horizons_center(399), epoch=RECENT_EPOCH,
        # Zarya launched 1998-11-20, but the station has been reboosted hundreds of times
        # since and its node regresses 4.95 deg/day, so the elements are worth about 73 days.
        # build_spacecraft.py derives that and clips this to it.
        known_from="1998-11-20", known_until="2031-01-01",
        model_dir="International Space Station (ISS) (A)",
        model_file="International Space Station (ISS) (A).glb",
        length_metres=109.0, length_note="main truss length",
        summary="Continuously crewed since November 2000, 400 km up at 7.7 km/s.",
        triangle_budget=6000),
    Mission(
        id="hubble", name="Hubble Space Telescope", placement=KEPLER, parent="earth",
        naif_id=-48, center=horizons_center(399), epoch=RECENT_EPOCH,
        # Node regresses 6.8 deg/day, and the orbit has decayed from 615 km to about 515 km
        # since launch. Clipped to roughly 53 days about the epoch.
        known_from="1990-04-24", known_until="2035-01-01",
        model_dir="Hubble Space Telescope (A)", model_file="Hubble Space Telescope (A).glb",
        length_metres=13.2, length_note="overall length",
        summary="In low Earth orbit since 1990; serviced five times by shuttle crews.",
        triangle_budget=5000),
    Mission(
        id="chandra", name="Chandra X-ray Observatory", placement=KEPLER, parent="earth",
        naif_id=-151, center=horizons_center(399), epoch=RECENT_EPOCH,
        # Not 1999-07-23, which is the shuttle launch: Chandra reached this orbit only after
        # the last of its integral propulsion burns on 1999-08-07. J2 is negligible at a
        # 63-hour orbit, so this date and the lunisolar perturbations are the whole bound.
        known_from="1999-08-07", known_until="2035-01-01",
        model_dir="Chandra X-ray Observatory", model_file="Chandra X-ray Observatory.glb",
        length_metres=13.8, length_note="overall length",
        summary="A 64-hour elliptical orbit reaching a third of the way to the Moon.",
        triangle_budget=5000),
    Mission(
        id="tess", name="TESS", placement=KEPLER, parent="earth",
        naif_id=-95, center=horizons_center(399), epoch=RECENT_EPOCH,
        # Not the 2018-04-18 launch: TESS was in phasing orbits until the lunar flyby of
        # 2018-05-17 placed it in the 2:1 resonant orbit, and science began 2018-07-25.
        known_from="2018-07-25", known_until="2035-01-01",
        model_dir="Transiting Exoplanet Survey Satellite (TESS) (A)",
        model_file="Transiting Exoplanet Survey Satellite (TESS) (A).glb",
        length_metres=3.7, length_note="span across the deployed solar arrays",
        summary="Finds transiting exoplanets from a 2:1 lunar-resonant orbit.",
        triangle_budget=4000),
    Mission(
        id="lro", name="Lunar Reconnaissance Orbiter", placement=KEPLER, parent="moon",
        naif_id=-85, center=horizons_center(301), epoch=RECENT_EPOCH,
        # LRO flew a 50 km circular mapping orbit from 2009 until December 2011 and has been
        # in this eccentric frozen orbit since. The 2009 launch date described a different
        # orbit entirely, which is not a perturbation of this one.
        known_from="2012-01-01", known_until="2030-01-01",
        model_dir="Lunar Reconnaissance Orbiter (A)",
        model_file="Lunar Reconnaissance Orbiter (A).glb",
        length_metres=3.9, length_note="overall length",
        summary="Mapping the Moon from a low polar orbit since 2009.",
        triangle_budget=6000),
    Mission(
        id="mro", name="Mars Reconnaissance Orbiter", placement=KEPLER, parent="mars",
        naif_id=-74, center=horizons_center(499), epoch=RECENT_EPOCH,
        # 2006-03-10 is Mars orbit insertion, into a 35-hour capture ellipse; aerobraking into
        # the science orbit finished at the end of August 2006. Mars's J2 then clips this to
        # about four months about the epoch.
        known_from="2006-09-11", known_until="2030-01-01",
        model_dir="Mars Reconnaissance Orbiter (MRO) (A)",
        model_file="Mars Reconnaissance Orbiter (MRO) (A).glb",
        length_metres=13.6, length_note="span across the solar arrays",
        summary="HiRISE has resolved metre-scale objects on Mars since 2006.",
        triangle_budget=8000),
    Mission(
        id="maven", name="MAVEN", placement=KEPLER, parent="mars",
        naif_id=-202, center=horizons_center(499),
        # Not RECENT_EPOCH: JPL's MAVEN ephemeris stops at 2026-03-01, and asking past the end
        # returns no data at all rather than a short answer, so the whole mission drops out of
        # the build. Same shape as SOHO below — the spacecraft is working, the published
        # trajectory simply ends — and the window says what is known rather than what is
        # expected, so it ends there too.
        epoch="2026-02-01",
        # MAVEN aerobraked to a lower apoapsis in February-April 2019 to serve as a relay;
        # the orbit before that is a different one, not this one perturbed.
        known_from="2019-05-01", known_until="2026-03-01",
        model_dir="Mars Atmosphere and Volatile EvolutioN (MAVEN) (A)",
        model_file="Mars Atmosphere and Volatile EvolutioN (MAVEN) (A).glb",
        length_metres=11.4, length_note="solar array span",
        summary="Measuring how Mars lost its atmosphere to the solar wind.",
        triangle_budget=6000),
    Mission(
        id="mars-odyssey", name="2001 Mars Odyssey", placement=KEPLER, parent="mars",
        naif_id=-53, center=horizons_center(499), epoch=RECENT_EPOCH,
        # 2001-10-24 is orbit insertion; aerobraking into the mapping orbit ran until
        # January 2002 and mapping began that February.
        known_from="2002-02-19", known_until="2030-01-01",
        model_dir="Mars Odyssey", model_file="Mars Odyssey.glb",
        length_metres=5.7, length_note="overall span including the boom",
        summary="The longest-serving spacecraft at Mars, in orbit since 2001.",
        triangle_budget=5000),
    Mission(
        id="juno", name="Juno", placement=KEPLER, parent="jupiter",
        naif_id=-61, center=horizons_center(599), epoch="2025-01-01",
        # Juno's period was cut by its satellite encounters: 53 days from capture, 43 after
        # Ganymede in 2021, 38 after Europa in 2022, 33 after the Io flybys of December 2023
        # and February 2024. The elements here describe the last of those, so the window
        # starts there rather than at capture. Jupiter's J2 is large but Juno's orbit is so
        # wide that it precesses slowly, so this date is the binding constraint.
        known_from="2024-03-01", known_until="2025-09-30",
        model_dir="Juno (A)", model_file="Juno (A).glb",
        length_metres=20.0, length_note="solar array span",
        summary="Polar orbits threading between Jupiter and its radiation belts.",
        ended="Extended mission concluded on 30 September 2025.",
        triangle_budget=7000),
    Mission(
        id="galileo", name="Galileo", placement=KEPLER, parent="jupiter",
        naif_id=-77, center=horizons_center(599), epoch="2000-01-01",
        known_from="1995-12-08", known_until="2003-09-21",
        model_dir="Galileo", model_file="Galileo.glb",
        length_metres=5.3, length_note="overall height",
        summary="Orbited Jupiter for eight years and dropped a probe into it.",
        ended="Deliberately flown into Jupiter on 21 September 2003.",
        triangle_budget=6000),
    Mission(
        id="cassini", name="Cassini", placement=KEPLER, parent="saturn",
        naif_id=-82, center=horizons_center(699), epoch="2010-01-01",
        known_from="2004-07-01", known_until="2017-09-15",
        model_dir="Cassini-Huygens (A)", model_file="Cassini-Huygens (A).glb",
        length_metres=6.8, length_note="overall height",
        summary="Thirteen years at Saturn, ending in the atmosphere in 2017.",
        ended="Entered Saturn's atmosphere on 15 September 2017.",
        triangle_budget=8000),
    Mission(
        id="parker-solar-probe", name="Parker Solar Probe", placement=KEPLER, parent="sun",
        naif_id=-96, center=horizons_center(10), epoch=RECENT_EPOCH,
        # The one close orbiter whose mission dates are already the right window: the final
        # Venus assist of 2024-11-06 froze this 88-day orbit, and nothing perturbs it quickly.
        known_from="2024-12-24", known_until="2030-01-01",
        model_dir="Parker Solar Probe", model_file="Parker Solar Probe.glb",
        length_metres=3.0, length_note="overall height including the heat shield",
        summary="Closest approach to the Sun of any spacecraft; inside the corona since 2021.",
        triangle_budget=6000),

    # ==================================================================================
    # Landed. Fixed in the host body's rotating frame.
    # ==================================================================================
    Mission(
        id="perseverance", name="Perseverance", placement=SITE, parent="mars",
        latitude_deg=18.4447, longitude_deg=77.4508,
        site_name="Jezero Crater",
        site_reference="NASA/JPL Mars 2020 landing site, planetocentric, IAU Mars frame",
        known_from="2021-02-18", known_until="2035-01-01",
        model_dir="Mars 2020 Perseverance Rover", model_file="Mars 2020 Perseverance Rover.glb",
        length_metres=3.0, length_note="rover body length",
        summary="Landed February 2021; caching samples of an ancient river delta.",
        triangle_budget=9000),
    Mission(
        id="insight", name="InSight", placement=SITE, parent="mars",
        latitude_deg=4.502, longitude_deg=135.623,
        site_name="Elysium Planitia",
        site_reference="NASA/JPL InSight landing site, planetocentric, IAU Mars frame",
        known_from="2018-11-26", known_until="2035-01-01",
        model_dir="InSight Cruise Lander",
        model_file="InSight Cruise Lander (arm deployed).glb",
        length_metres=6.0, length_note="solar array span",
        summary="Measured marsquakes from 2018 until dust ended the mission in 2022.",
        ended="Final transmission on 15 December 2022; the lander is still there.",
        triangle_budget=6000),
    Mission(
        id="viking-1", name="Viking 1 Lander", placement=SITE, parent="mars",
        latitude_deg=22.697, longitude_deg=312.05,
        site_name="Chryse Planitia",
        site_reference="NASA Viking 1 landing site, planetocentric, IAU Mars frame",
        known_from="1976-07-20", known_until="2035-01-01",
        model_dir="Viking Lander", model_file="Viking Lander.glb",
        length_metres=3.0, length_note="span across the footpads",
        summary="First successful US Mars landing, July 1976.",
        ended="Last contact on 11 November 1982; the lander is still there.",
        triangle_budget=6000),
    Mission(
        id="apollo-11-lm", name="Apollo 11 Lunar Module", placement=SITE, parent="moon",
        latitude_deg=0.67409, longitude_deg=23.47298,
        site_name="Tranquility Base",
        site_reference="Apollo 11 descent stage, LRO-derived coordinates (Wagner et al. 2017)",
        known_from="1969-07-20", known_until="2035-01-01",
        model_dir="Apollo Lunar Module", model_file="Apollo Lunar Module.glb",
        length_metres=9.4, length_note="landing gear span",
        summary="The descent stage left at Tranquility Base on 20 July 1969.",
        ended="Ascent stage departed 21 July 1969; the descent stage remains.",
        triangle_budget=7000),
]

# New Horizons is absent deliberately: Horizons has its ephemeris, but NASA-3D-Resources ships
# no model for it. Drawing another probe's body at New Horizons' position and labelling it New
# Horizons would be worse than the gap.

ALL: List[Mission] = MISSIONS


def by_id() -> Dict[str, Mission]:
    return {mission.id: mission for mission in ALL}
