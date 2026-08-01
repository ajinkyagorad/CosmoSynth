# Data sources and licensing

Every number in CosmoSynth traces to a source. This document lists them, notes their terms, and
flags what still needs checking before commercial distribution.

---

## Currently used

### JPL Horizons — <https://ssd.jpl.nasa.gov/horizons/>

The primary source. Supplies gravitational parameters, radii, densities, geometric albedos and
osculating orbital elements for all 31 bodies in the shipped catalogue, plus the 260 state
vectors in the validation fixture.

- **Accessed via**: the public API, offline, by `tools/cosmodata/`. Never at runtime.
- **Ephemeris**: DE440/DE441 planetary and lunar ephemerides.
- **Terms**: US Government work, not subject to copyright in the United States. NASA requests
  acknowledgement. No API key or registration required.
- **Etiquette**: JPL asks that automated clients keep their request rate modest. The importers
  sleep between requests by default (`--delay`).

### NASA 3D Resources — <https://github.com/nasa/NASA-3D-Resources>

The 24 spacecraft models. Fetched by `tools/cosmodata/build_spacecraft.py`, converted to the
compact `.csmesh` format, and committed under `StreamingAssets/CosmoSynth/models/`.

NASA publishes these into the public domain, subject to the [media usage
guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/). **Some models in that
repository are contributed rather than NASA-authored**; the ones shipped here were checked
individually, and each catalogue record carries the source URL and licence string.

Five things about this source are worth recording, because each cost time:

- **Almost every model is Draco-compressed** (`KHR_draco_mesh_compression`, and *required*, not
  optional). Draco is an entropy-coded connectivity format; decoding it is not a job for a
  hand-written parser. The importer shells out to `npx @gltf-transform/cli` for that one step.
  This is the only tool in `tools/cosmodata/` that is not standard-library-only, and the
  dependency is confined to it: the `.csmesh` output is committed, so building the app, running
  the tests and using the data need nothing but Python.
- **A silent decode failure looks exactly like a success.** A well-known Python mesh library,
  handed one of these models without a Draco decoder present, returns a scene with the right
  number of meshes and *exactly* the right triangle count — with every vertex at the origin,
  and no error raised. Triangle counts therefore prove nothing. `gltf.validate_decoded` checks
  that the geometry occupies space, which is the only cheap check that actually distinguishes
  the two.
- **Some models are simply authored inside out.** glTF fixes both conventions at once, so a
  model's winding and its normals must agree; SOHO's disagree on 100% of faces, and no amount of
  correct importing changes that. Which half is wrong is settled by the enclosed signed volume,
  which reads the geometry and no normals at all: SOHO encloses +0.0149 of its bounding box
  cubed, squarely among the 23 models that need no repair, so its winding is right and its
  normals are inverted. `gltf.orient_outward` negates them, says so in the build log, and
  refuses to guess where the volume is near zero because the model is open. Separately, a
  mirroring node transform requires the winding to be reversed as the specification says —
  which the importer did for its own axis conversion and not for transforms inside the model.
- **A failed import must not leave the previous answer behind.** The mesh builder used to write
  straight to its output path, so a model the importer *correctly refused* kept whatever was
  there from the run before — and `--skip-models` adopts any `.csmesh` it finds by name, so the
  next partial rebuild put the rejected file back into the catalogue as though it had just been
  made. That is how the inside-out SOHO nearly shipped after the check had already caught it:
  the refusal was printed, the old file survived, a later run adopted it. Meshes are now written
  to a temporary name and moved into place only when complete; any failure deletes the target
  outright and says so; an adopted mesh is re-validated against its own header before being
  trusted; `--only` merges into the existing catalogue instead of replacing it with the one
  mission it rebuilt; and the run reports any mesh no mission claims. Same lesson as the Draco
  trap one step further out — the artefact on disk is not evidence of the run meant to make it.
- **Model scale is not recorded in the files.** The exports carry no consistent unit, so each
  model's longest axis is scaled to a published dimension from the mission table. The dimension
  is a measurement; that the longest axis *is* that dimension is an assumption about the
  export. The pair is recorded as **Inferred**, naming the dimension used.

Two missions were dropped rather than approximated. New Horizons has a JPL ephemeris but no
model in the repository, and substituting another probe's body would have put something that is
not New Horizons at New Horizons' position under its name.

### HYG star database — <https://codeberg.org/astronexus/hyg>

The 119 613 real stars in the sky field, at measured positions. Fetched by
`tools/cosmodata/build_starfield.py`, written as a flat binary
(`StreamingAssets/CosmoSynth/sky/stars.csstars`, 2.9 MB, 24 bytes a star, sorted brightest
first) plus the 548 with proper names.

**Licence: CC BY-SA 4.0 — attribution AND share-alike.** This is the strongest claim on
anything in this project; every other source here is public-domain government work. Two
consequences, recorded now rather than discovered during a licence audit:

- The attribution must travel with the build, not merely live in a source file.
- The derived catalogue the importer writes is an **adaptation of a ShareAlike work**, so that
  file carries the same licence. It does not follow that the application does — the app is not
  an adaptation of a star catalogue — but that is a question for a lawyer, and it is exactly the
  kind of thing this document exists to raise before it becomes expensive.

Required attribution: *HYG star database v4.4 (astronexus), CC BY-SA 4.0 — compiled from the
Hipparcos, Yale Bright Star and Gliese catalogues.* The underlying astrometry is ESA and Yale
work; HYG is the compilation.

Two honesty notes carried into the data itself:

- **Temperature is inferred, never measured.** It comes from the B-V colour index through
  Ballesteros' formula (EPL 97 34008, 2012), and for the 1890 stars with no published colour
  index it comes from the spectral class alone — a weaker claim still, and flagged separately in
  the record so the runtime can tell the two apart.
- **10 224 stars have no usable parallax.** HYG writes 100 000 pc as a sentinel. These are kept
  and flagged rather than dropped or placed at 100 000 pc: their *direction* is measured and
  their distance is not known at all, which is precisely the case the sky-sphere treatment
  already exists for. Dropping them would delete a tenth of the sky; placing them would invent a
  distance.

### Spacecraft trajectories — JPL Horizons

The same service as the planets, queried for spacecraft NAIF identifiers. Coverage is
per-object and finite, and was verified rather than assumed — `-25` is Lunar Prospector, not
Magellan; JPL's Webb ephemeris ends in July 2031; Dawn's begins at 12:40 on launch day, so a
request starting at midnight that morning returns nothing at all.

Every spacecraft therefore ships with the window over which its position is claimed to be
known, and is **not drawn outside it**. See `docs/accuracy.md` for why that matters more for
spacecraft than for planets.

### IAU WGCCRE rotational elements

Pole right ascension and declination, prime meridian angle and rotation rate for 20 bodies.

- **Source**: Archinal et al., *Report of the IAU Working Group on Cartographic Coordinates and
  Rotational Elements: 2015*, Celestial Mechanics and Dynamical Astronomy 130:22 (2018).
  <https://doi.org/10.1007/s10569-017-9805-5>
- **Terms**: published values, cited. The small periodic terms are omitted — they matter for
  cartography, not for rendering.
- **Location**: `Core/Frames/BodyOrientation.cs`, as static fields. The importer records only
  the body's name and the loader resolves it, so these live in exactly one place.

### NASA planetary fact sheets — <https://nssdc.gsfc.nasa.gov/planetary/factsheet/>

Bond albedos, atmospheric pressures, scale heights, temperatures and compositions.

- **Terms**: US Government work, not subject to domestic copyright.
- **Location**: `tools/cosmodata/bodies.py`, each with its source noted inline.

### IAU 2015 Resolution B3 nominal values

Solar luminosity, effective temperature, solar radius, and the astronomical unit.

- **Terms**: published standard.
- **Location**: `Core/Physics/Constants.cs`.

### CODATA 2018

The gravitational constant *G* and the Stefan–Boltzmann constant.

### Individual papers

Four small bodies carry hand-entered values because Horizons prints small-body masses in a
prose format whose trailing multiplier would be a factor-of-10⁴ silent error to misparse:

| Body | Source |
|---|---|
| Phobos, Deimos | Jacobson (2010), Mars satellite ephemeris MAR097; radii from Willner et al. (2014) and Thomas et al. (2000) |
| Bennu | Scheeres et al. (2019), OSIRIS-REx radio science, *Nature Astronomy* 3:352 |
| 1P/Halley | Giotto/Vega encounter results. Nucleus mass uncertain to ~2×, so this is recorded as **Inferred**, not Measured |

---

## Planned, not yet integrated

Listed with the licence question that has to be answered before each is shipped.

| Data | Source | Licence note |
|---|---|---|
| High-precision ephemerides | [NAIF SPICE](https://naif.jpl.nasa.gov/naif/data.html) | Public domain. CSPICE is unrestricted. Kernels are large — preprocess offline into compact segments rather than parsing in C# at runtime |
| Planetary elevation and imagery | [USGS Astropedia](https://astrogeology.usgs.gov/search) | Mostly public domain, **but check per product** — some incorporate third-party contributions with their own terms |
| Raw mission data | [NASA PDS](https://pds.nasa.gov/) | Public domain; needs substantial preprocessing (PDS3/PDS4, ISIS `.cub`, IMG) before Unity can use it |
| Lunar terrain | [LROC QuickMap](https://quickmap.lroc.asu.edu/) | LROC data is public; the QuickMap interface itself is ASU-operated — attribute correctly |
| Mars terrain | [Mars ODE](https://ode.rsl.wustl.edu/mars/) | MOLA, HiRISE, CTX, THEMIS. Public, per-instrument attribution expected |
| Small bodies | [JPL SBDB](https://ssd.jpl.nasa.gov/tools/sbdb_lookup.html) | Same terms as Horizons |
| Minor planet orbits | [Minor Planet Center](https://www.minorplanetcenter.net/) | **Check carefully.** MPC is IAU-operated with its own data-use policy, not automatically NASA-equivalent |
| Exoplanets | [NASA Exoplanet Archive](https://exoplanetarchive.ipac.caltech.edu/) | Operated by Caltech/IPAC under NASA contract. Acknowledgement text is specified — use theirs verbatim |
| Stars (deeper than HYG) | [ESA Gaia Archive](https://gea.esac.esa.int/archive/) | **ESA, not NASA.** CC BY-SA 3.0 IGO with a specific required acknowledgement. HYG (now shipped, see above) covers the naked-eye and telescopic sky; Gaia is what a fainter or more distant catalogue would need |
| Star identity | [SIMBAD](https://simbad.cds.unistra.fr/simbad/) | CDS-operated. Requires the CDS acknowledgement; bulk use has its own conditions |
| Galaxies | [NASA/IPAC NED](https://ned.ipac.caltech.edu/) | Caltech/IPAC under NASA contract; specified acknowledgement |
| Telescope imagery | [MAST](https://archive.stsci.edu/) | Mission-dependent. Hubble and JWST public data are generally free to use; **individual images may have photographer or processing credits** |
| Earth imagery | [NASA Worldview](https://worldview.earthdata.nasa.gov/) / [Earthdata](https://www.earthdata.nasa.gov/) | Mostly open; some products carry contributor terms |
| Earth elevation | [Copernicus DEM](https://dataspace.copernicus.eu/) | **ESA/EU, not NASA.** Copernicus has its own licence with attribution requirements and some redistribution conditions |
| Weather and climate | [Copernicus CDS](https://cds.climate.copernicus.eu/) | Copernicus licence; registration required |
| Ocean bathymetry | [GEBCO](https://www.gebco.net/) | Free to use with a required attribution statement |
| Borders and coastlines | [Natural Earth](https://www.naturalearthdata.com/) | Public domain |
| Roads and places | [OpenStreetMap](https://www.openstreetmap.org/) | **ODbL.** Share-alike. Has real implications for a commercial product — read it before integrating |

---

## The rule this table exists to enforce

**"Hosted by NASA" does not mean "public domain."**

NASA and ESA archives host contributed datasets, mission products with instrument-team
attribution requirements, and images with individual credits. Gaia is ESA under CC BY-SA.
Copernicus is EU with its own terms. OpenStreetMap is ODbL share-alike. The Minor Planet Center
is IAU-operated. Each of those would be a licensing problem if assumed to be NASA-equivalent.

Before any commercial distribution, every dataset and every derived texture needs an individual
licence and attribution audit. The provenance system makes that tractable — each field records
its source, so the set of sources actually in use can be enumerated from the catalogue rather
than reconstructed from memory.

---

## Reproducing the data

```bash
# The shipped Solar System catalogue (31 bodies, ~85 kB)
python3 tools/cosmodata/build_solar_system.py

# The validation fixture (10 bodies, 260 state vectors, ~104 kB)
python3 tools/cosmodata/fetch_validation_fixture.py

# The spacecraft: trajectories from JPL, models from NASA (24 craft, ~956 kB + ~2 MB of meshes)
python3 tools/cosmodata/build_spacecraft.py

# The real sky: 119613 stars from HYG (~2.9 MB). CC BY-SA 4.0 — read the licence note above.
python3 tools/cosmodata/build_starfield.py

# ...just the trajectories, leaving the committed meshes alone
python3 tools/cosmodata/build_spacecraft.py --skip-models
```

All are network-bound and take a few minutes. All use only the Python standard library except
`build_spacecraft.py`, which additionally needs `npx` on the path to decompress Draco geometry —
see the NASA 3D Resources section above.

Generated files carry a `source` field naming the query and the retrieval date, and every body
carries per-field provenance. Nothing in the pipeline invents a value silently: where Horizons
publishes no gravitational parameter, the importer **skips the body and says so** rather than
guessing, because a fabricated mass would propagate into every orbit that depends on it and
look entirely plausible while doing so.
