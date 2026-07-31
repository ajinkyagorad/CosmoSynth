# Accuracy and its limits

This document states what CosmoSynth's numbers are worth. Every figure here is **measured by
the test suite**, not estimated — run `./run-tests.sh Horizons` to reproduce the tables.

The project's premise is that a simulator which cannot say how good its numbers are is a
picture, not an instrument. So the limits are documented as carefully as the capabilities.

---

## 1. Is the code correct?

The state-vector-to-orbital-elements conversion is checked against JPL's own published
osculating elements for the same instant and the same state vector. Because JPL derived their
elements from the vector we are handed, any disagreement is our arithmetic, with the physics
held fixed.

| Body | semi-major axis | eccentricity | inclination | RAAN | arg. periapsis | mean anomaly |
|---|---|---|---|---|---|---|
| Mercury | 2.6e-16 | 4.4e-16 | 0.00000″ | 0.00000″ | 0.00000″ | 0.00000″ |
| Venus | 4.1e-16 | 2.8e-16 | 0.00000″ | 0.00000″ | 0.00000″ | 0.00000″ |
| Earth–Moon barycentre | 4.0e-16 | 6.9e-18 | 0.00000″ | 0.00000″ | 0.00000″ | 0.00000″ |
| Mars barycentre | 1.2e-14 | 7.6e-15 | 0.00000″ | 0.00000″ | 0.00000″ | 0.00000″ |
| Jupiter barycentre | 1.7e-11 | 6.4e-13 | 0.00000″ | 0.00000″ | 0.00007″ | 0.00007″ |
| Saturn barycentre | 6.0e-13 | 3.1e-14 | 0.00000″ | 0.00000″ | 0.00000″ | 0.00000″ |
| Uranus barycentre | 5.0e-16 | 5.6e-17 | 0.00000″ | 0.00000″ | 0.00000″ | 0.00000″ |
| Neptune barycentre | 3.5e-11 | 2.4e-11 | 0.00000″ | 0.00000″ | 0.00046″ | 0.00046″ |
| Moon (geocentric) | 1.3e-12 | 1.2e-12 | 0.00000″ | 0.00000″ | 0.00000″ | 0.00000″ |
| Ceres | 4.3e-10 | 2.5e-10 | 0.00000″ | 0.00000″ | 0.00097″ | 0.00098″ |

Distances agree to within **9e-10 relative**, angles to under **0.001 arcseconds** — about the
apparent width of a coin at four thousand kilometres. The residuals that are not at machine
precision come from two places, neither of which is a defect:

- **Eccentricity of near-circular orbits is ill-conditioned.** The eccentricity vector is built
  from `v² − μ/r`, a difference of two large and nearly equal numbers, so several digits cancel
  before the result exists. This is why equinoctial elements were invented. At Neptune the 2.4e-11
  residual in `e` displaces the orbit's focus by about a hundred metres across four and a half
  billion kilometres. The test therefore asserts on `a·e`, the geometric consequence, rather
  than on a digit count.
- **The obliquity constant.** Horizons' ecliptic output is built on the IAU 1976 obliquity
  (84381.448″); the current IAU 2006 value is 84381.406″. The 42-milliarcsecond difference is
  irrelevant for rendering but visible at full precision, so `StandardFrames` exposes both and
  defaults to the 1976 value when comparing against JPL.

Two independent propagators — classical elements and universal variables — are also
cross-validated against each other on both synthetic and real orbits, agreeing to 1e-9
relative. Agreement between two unrelated derivations is much stronger evidence than either
one round-tripping against itself, which only proves a function is invertible.

---

## 2. How good is the physics model?

**This is the limitation that matters.** CosmoSynth currently propagates two-body Keplerian
orbits. The real solar system is an n-body problem with relativistic corrections, and a conic
section is an approximation to it. The table below is the measured drift between our
propagation and JPL's real ephemeris, as a fraction of orbital radius:

| Body | after ~2 months | after 1 year | after 5 years |
|---|---|---|---|
| Mercury | 4.3e-5 | 9.6e-5 | 3.8e-4 |
| Venus | 9.4e-6 | 7.4e-5 | 2.0e-4 |
| Earth–Moon barycentre | 1.8e-5 | 3.9e-5 | 9.9e-5 |
| Mars barycentre | 4.2e-6 | 1.3e-4 | 4.2e-4 |
| Jupiter barycentre | 4.4e-7 | 6.0e-6 | 2.8e-4 |
| Saturn barycentre | 2.5e-6 | 6.2e-5 | 1.2e-3 |
| Uranus barycentre | 1.5e-6 | 3.6e-5 | 6.8e-4 |
| Neptune barycentre | 8.6e-7 | 2.1e-5 | 4.1e-4 |
| Ceres | 5.2e-6 | 1.9e-4 | 3.0e-3 |
| **Moon (geocentric)** | **3.8e-2** | **1.1e-1** | **2.2e-1** |

In practical terms, five years from the element epoch:

- **Planets** are out by roughly 1e-4 of their orbital radius — about 15 000 km for Earth. At
  any scale where you can see the whole orbit this is far below one pixel. It is not adequate
  for navigation or for predicting an occultation.
- **The Moon is the failure case**, and it is in the fixture deliberately. The Sun perturbs the
  lunar orbit almost as strongly as the Earth binds it, so osculating elements are close to
  meaningless over months. After five years the Kepler Moon is 22% of a lunar distance from
  where the Moon actually is. Anything that needs a real Moon needs a real ephemeris.

### What fixes this

Ranked by effort against benefit:

1. **Sampled ephemerides** — already implemented (`SampledEphemeris`, cubic Hermite, verified
   fourth-order convergence). Feeding it a table of real states from Horizons or SPICE reduces
   the error to the sampling error, which for ten-day sampling of a one-year orbit is 2e-6.
   This is the correct fix and needs only importer work.
2. **Secular element rates** — cheap, and removes most of the long-period planetary drift.
3. **N-body propagation** for the bodies that need it. Necessary for mission planning; overkill
   for a viewer.

---

## 3. Time

- Simulation time is **TDB seconds past J2000**, matching SPICE's ET, so imported data needs no
  conversion.
- UTC exists only as a display and input format. The full IERS leap-second table since 1972 is
  applied; before 1972 the value is clamped rather than modelling the rubber-band era, and the
  clamp is documented in the source so it cannot be mistaken for a real quantity.
- `TDB − TT` uses the standard first-order Fairhead–Bretagnon term, good to a few microseconds.
- Calendar conversion is exact: it routes through an integer Julian Day Number plus a separate
  seconds-of-day term. A Julian Date carried in a single double resolves only ~20 µs, because
  the integer part consumes most of the mantissa — the interop accessor still offers one, and
  a test pins that precision floor so it stays a known cost rather than a surprise.

---

## 4. Precision

| Representation | Precision at Neptune (4.5e9 km) | Precision at 1 AU |
|---|---|---|
| `float` (Unity transform) | 300 km | 10 km |
| `double` (`Double3`, km) | 0.45 mm | 0.015 mm |

This is the entire reason for the double pipeline and the floating origin. A float at 1 AU
cannot resolve a city. Positions are held as doubles in kilometres and only converted to float
after the floating origin has rebased them to something small; the reference-frame tree keeps
the numbers small in the first place, so a lander near Enceladus is stored relative to
Enceladus (~100 km) rather than to the barycentre (~1.4e9 km).

Beyond about 1e17 km a single double starts to lose kilometre precision. Carrying a per-sector
integer offset is the standard remedy; it is not implemented, because nothing currently
rendered needs it.

---

## 5. What is measured, what is invented

Every field in the catalogue carries a provenance class, visible in the in-app inspector:

| Class | Meaning |
|---|---|
| **Measured** | Taken from a published dataset, with the citation attached |
| **Inferred** | Derived from measurements through a stated model |
| **Procedural** | Synthesised by our generators. Deterministic, but invented |
| **Fictional** | Authored. Makes no claim about reality |

Currently in the shipped Solar System catalogue:

- **Measured**: GM, radii, orbital elements, albedos, atmospheric pressures and compositions,
  rotational elements.
- **Inferred**: display colours (approximate integrated disc colour); atmospheric scattering
  coefficients (tuned to reproduce observed sky colour, not computed from radiative transfer);
  polar radii for bodies where only a mean radius is published, which are marked as assuming
  sphericity rather than silently treated as measurements.
- **Procedural**: all terrain. No measured elevation model is loaded yet, so every surface you
  see is generated. It is labelled as such.
- **Absent**: a field with no provenance entry reports as `Unknown`, never as measured.

Four bodies (Phobos, Deimos, Bennu, 1P/Halley) carry hand-entered GM values with paper
citations, because Horizons prints small-body masses in a prose format
(`Mass (10^20 kg ) = 1.08 (10^-4)`) whose trailing multiplier would be a factor-of-10⁴ silent
error to misparse. Halley's is marked **Inferred**, not Measured, because a comet nucleus mass
is uncertain to about a factor of two.

---

## 6. Known gaps

Stated plainly, so nobody discovers them by surprise:

- No real terrain. All surfaces are procedural.
- No atmospheric scattering shader yet; atmospheric *data* is present and validated.
- Rings are not rendered.
- No star catalogue. Gaia ingestion is designed for but not built.
- Barycentre wobble is not modelled: planets are their own body centres, so Earth's Keplerian
  fit absorbs the ~4700 km monthly Earth–Moon wobble as fit error.
- Light-time correction is not applied. Positions are geometric, so you see where a body *is*,
  not where it *appears*. This is the right default for a spatial simulator and the wrong one
  for reproducing an observation.
- The IAU rotation models omit the small periodic terms, which matter for cartography at the
  sub-kilometre level and not for rendering.
