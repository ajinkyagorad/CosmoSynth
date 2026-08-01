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

1. **Sampled ephemerides** — implemented (`SampledEphemeris`, cubic Hermite, verified
   fourth-order convergence) and now fed real data for the spacecraft, though not yet for the
   planets. Feeding it a table of real states from Horizons or SPICE reduces the error to the
   sampling error, which for ten-day sampling of a one-year orbit is 2e-6. This is the correct
   fix for the planets too and needs only importer work.
2. **Secular element rates** — cheap, and removes most of the long-period planetary drift.
3. **N-body propagation** for the bodies that need it. Necessary for mission planning; overkill
   for a viewer.

### Spacecraft are a different problem, and get a different answer

A planet's motion is two-body plus a small perturbation, so one method suits all of them. A
spacecraft's is not, and the 24 shipped here are placed by whichever of three mechanisms is
actually least wrong for that trajectory. The choice is recorded per body in its provenance.

| Placement | Used for | Error |
|---|---|---|
| **Sampled table** | Heliocentric cruise, escape hyperbolas, halo orbits about Lagrange points | 0.3–52 km in cruise; **millions of km across a gravity assist** |
| **Osculating elements** | Anything in a tight orbit about one body | The orbit precesses in reality and does not here, at a rate now computed per mission |
| **Surface site** | Landers and rovers | Exact, to the accuracy of the published coordinates |

The counter-intuitive one is the middle row, and it is worth spelling out because the obvious
reasoning gets it backwards. Sampling is *more* faithful than a conic — except where the
sampling cannot resolve the motion, and uniform sampling in time puts the fewest samples
exactly where the object moves fastest, which is periapsis.

Juno is the case that settled it. Its orbit runs from 75 000 km at perijove to 8 million km at
apojove. At any cadence cheap enough to ship inside the APK it travels millions of kilometres
between consecutive samples near Jupiter, and the interpolated path cuts the corner **straight
through the planet**. A conic is a worse fit to Juno's true orbit than a dense table would be,
and a far better one than any table we can actually ship: its error is a precession, not
geometry that is physically impossible. `tools/cosmodata/build_spacecraft.py` enforces the rule
mechanically, refusing to write a table over 900 samples rather than letting a bad cadence
through quietly.

### What a sampled table cannot do, measured

The counter-argument above is right, and the mirror image of it was previously stated as a
strength and is false: a uniform table does **not** capture the planetary encounters that
shaped the escape trajectories. The step has to span the whole mission — Voyager 1 covers 57
years — and a gravity assist reshapes a trajectory in hours. Against a six-hour Horizons
reference taken across the encounter:

| Encounter | Worst error of the shipped table |
|---|---|
| Voyager 1 at Jupiter, March 1979 | 3.6 million km |
| Pioneer 10 at Jupiter, December 1973 | 5.7 million km |
| Ulysses at Jupiter, February 1992 | 0.8 million km |

Away from the encounters the same tables are good to a median of 0.3–52 km, and Webb's halo
orbit — the case the mechanism exists for — to 8.5 km. That is five to seven orders of
magnitude between the two regimes inside one table.

The cadences were also simply too coarse: every one was between 1.25 and 5.8 times below what
the 900-sample cap allowed, for no recorded reason. Spending the budget cut the encounter errors
by 2.4–4.8× and the cruise errors by about ten, at the cost of roughly a megabyte. Note how far
short of the fourth-order prediction that is — a 5.8× finer step should be a thousandfold better
and delivers threefold — because convergence order describes smooth functions and a flyby is not
one. Non-uniform sampling would fix it properly and the format deliberately does not support it,
because storing sample times triples the file.

**A second-order consequence worth stating:** the planets are still two-body conics fitted at a
2026 epoch, and the drift table above only measures five years. Winding the clock back to a 1979
flyby puts the *planet* millions of kilometres from where it was, independently of the
spacecraft. Both errors are real and neither is bounded by the other.

### Knowing when you do not know

Every spacecraft carries the window over which its position is claimed, and **is not drawn
outside it**. This matters far more for spacecraft than for planets, because both closed-form
placements will evaluate happily forever: a conic does not know its spacecraft has crashed, and
the sampled interpolator clamps to its last value rather than failing. Without the window,
Cassini would still be circling Saturn today — smooth, plausible, and nine years after it
entered the atmosphere.

The window is **derived, not declared.** The mission table holds the outer bound — when the
spacecraft existed and was operating — and each placement narrows it to what its own data
supports:

- a **sampled table** is clipped to its own first and last sample. These used to be independent
  and disagreed on all eight sampled missions, the declared window running past the data by
  between one and 148 days;
- a **conic** is clipped to one full revolution of its fastest precessing angle either side of
  its epoch, computed from the parent's J2. That bound is not "the error is small" — at 359° of
  node regression the drawing is as wrong as at 361° — it is the point past which the drawn
  orbital plane has swept every orientation and says nothing at all.

The second rule is the one that bites, and it revealed the larger of the two window bugs. The
window stops a *dead* spacecraft being drawn; it was simultaneously licensing a *live* one to be
drawn in the wrong place for decades:

| Mission | Node regression | Window claimed | Window supported |
|---|---|---|---|
| ISS | −4.95 °/day | 27 years | 146 days |
| Hubble | −6.77 °/day (apsides +11.0) | 36 years | 65 days |
| Mars Odyssey | +6.41 °/day | 28 years | 139 days |
| MRO | −2.82 °/day | 24 years | 167 days |

Over the ISS's old window the node turned about 49 000° — 136 complete revolutions — so its
orbital plane was not approximately right, it was arbitrary.

Where oblateness is negligible the mission dates are all there is, and those were checked rather
than assumed: Chandra reached its orbit on 1999-08-07 and not at its shuttle launch a fortnight
earlier; TESS only entered its resonant orbit after the lunar flyby of 2018-05-17; LRO flew a
circular mapping orbit until December 2011; MRO and Odyssey both aerobraked for months after
orbit insertion; Juno's period was cut four times by satellite encounters. JPL's Webb ephemeris
ends in July 2031, SOHO's in July 2026 and MAVEN's in March 2026 although both spacecraft are
working; Dawn's begins at 12:40 on launch day.

**This means the close orbiters have a shelf life.** Their epochs are set near the build date so
the derived window straddles it, and a few months after an import the Earth and Mars orbiters
stop being drawn until `build_spacecraft.py` is run again. That is the honest behaviour and it
is a real cost: the alternative is drawing them somewhere they are not.

Wind the clock back and the ended missions reappear — subject to everything above about how
well their position is known once you get there.

### The drawn size of a spacecraft is not its real size

A 13 m probe against a 143 000 km planet is around 1e-13 of a pixel at any scale that shows
both. Every spacecraft is therefore drawn enormously exaggerated, bounded at both ends:

- a **floor**, so it does not vanish — the same minimum-apparent-size rule the planets use;
- a **ceiling** of a quarter of its parent body's radius, so it cannot swallow the thing it
  orbits. Without this a 3 m Viking lander was drawn ten thousand kilometres across, larger
  than Mars, because from Earth the whole planet is three pixels;
- and where the ceiling is already below the floor, it is **not drawn at all**, because there
  is no size at which it would be both visible and honest.

`CelestialBodyView.SizeExaggeration` reports the factor in use, and the real dimension is in the
catalogue. Note that as of this writing neither is surfaced by the runtime interface — the
provenance panel has sections for orbit, rotation and atmosphere and none for a model, a
trajectory, a landing site or a coverage window — so the honest numbers exist and are currently
only visible in the editor's play-mode report.

**The drawn size is only as honest as the mesh's declared length**, which is a separate thing
that was wrong. The importer scaled each model so its longest axis matched the published
dimension, but measured that axis over every vertex in the buffer including ones no triangle
referenced — an artefact of vertex-cluster decimation. Ulysses shipped at 1.656 m against a
declared 3.200 m, and MAVEN 10 % small. Unreferenced vertices are now removed before the model
is measured, and both the packer and the test suite refuse a mesh that contains any.

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
  polar radii for bodies where Horizons publishes neither a polar radius nor a flattening, which
  are derived or assumed spherical and say which — see below.

### Planet shapes, and three ways of getting them wrong

Every planet in the catalogue was, until recently, either the wrong shape or no shape at all.
Three independent defects in one code path, each invisible on its own:

1. **The polar radius Horizons publishes was never read.** The scraper collected it; the
   importer chose between the equatorial and the volumetric mean radius and ignored it.
2. **The equatorial radius pattern did not match the giants.** Horizons labels them
   `Equat. radius (1 bar)` and the pattern allowed only `Equ.` and `Equatorial`, so the
   importer fell back to the volumetric mean. Jupiter's equatorial radius shipped as 69 911 km
   instead of 71 492.
3. **The volumetric mean was then shipped as the polar radius and labelled Measured** -
   citing a source that publishes no such number.

Together these made **all four giant planets perfect spheres**, and put Mars's polar radius
13.7 km and Earth's 14.3 km from their published values. The values now come from what Horizons
actually prints, in order of directness:

| Source | Used for | Result |
|---|---|---|
| Published polar radius | Jupiter, Saturn, Uranus, Neptune | Measured |
| Published flattening, `c = a(1-f)` | Earth, Mars | Measured |
| `c = Rvol^3 / a^2` from the volumetric mean | Mercury, Venus | Inferred, formula named |
| Nothing published | everything else | Inferred, "assumed spherical" |

The second row is the same measurement rearranged, not a model, and it is exact: Earth comes out
at **6356.752 km, the WGS 84 value**, and Mars at **3376.19 against the IAU 3376.20**. Where the
implied flattening exceeds 0.15 the body is irregular rather than oblate and none of this
applies; Phobos, Deimos, Bennu and Halley keep their cited published radii and stay spherical.

Resulting flattening, against the independent IAU figures: Jupiter 0.06487, Saturn 0.09796,
Uranus 0.02293, Neptune 0.01712, Earth 0.00335, Mars 0.00589 - all correct. The importer now
cross-checks the pair it derived against Horizons' own `Flattening` field, which is a different
number printed by the same source and is never used as an input, and complains if they disagree.

**Every volumetric quantity is unchanged**, because the mean radius reconstructed from each new
pair is the one it came from: Jupiter's is 69 911.3 km and its density 1326 kg/m3 either way.
What changes is the drawn oblateness and the radius a landing site is placed at, which moves the
Mars landers about 1.4 km.

One consequence is worth recording, because it is a shape of failure this project keeps meeting:
correcting Jupiter's radius made a *test* fail, and the test was the thing that was wrong. Juno's
perijove radius is 71 491 km against an equatorial 71 492, and `CelestialCatalog.Validate`
reported it as orbiting inside the planet. It is not - perijove occurs at +57 degrees latitude,
where Jupiter's surface is 68 106 km, so Juno clears the cloud tops by 3385 km, which is its real
perijove altitude. The check now computes the ellipsoid radius under the periapsis direction
instead of comparing against the equator.
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
