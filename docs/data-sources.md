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
| Stars | [ESA Gaia Archive](https://gea.esac.esa.int/archive/) | **ESA, not NASA.** Gaia data is released under CC BY-SA 3.0 IGO with a specific required acknowledgement. Do not assume NASA-style public domain |
| Star identity | [SIMBAD](https://simbad.cds.unistra.fr/simbad/) | CDS-operated. Requires the CDS acknowledgement; bulk use has its own conditions |
| Galaxies | [NASA/IPAC NED](https://ned.ipac.caltech.edu/) | Caltech/IPAC under NASA contract; specified acknowledgement |
| Telescope imagery | [MAST](https://archive.stsci.edu/) | Mission-dependent. Hubble and JWST public data are generally free to use; **individual images may have photographer or processing credits** |
| Earth imagery | [NASA Worldview](https://worldview.earthdata.nasa.gov/) / [Earthdata](https://www.earthdata.nasa.gov/) | Mostly open; some products carry contributor terms |
| Earth elevation | [Copernicus DEM](https://dataspace.copernicus.eu/) | **ESA/EU, not NASA.** Copernicus has its own licence with attribution requirements and some redistribution conditions |
| Weather and climate | [Copernicus CDS](https://cds.climate.copernicus.eu/) | Copernicus licence; registration required |
| Ocean bathymetry | [GEBCO](https://www.gebco.net/) | Free to use with a required attribution statement |
| Spacecraft models | [NASA 3D Resources](https://nasa3d.arc.nasa.gov/) | Mostly public, **but some models are contributed** and carry separate terms — check per asset |
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
```

Both use only the Python standard library. Both are network-bound and take a few minutes.

Generated files carry a `source` field naming the query and the retrieval date, and every body
carries per-field provenance. Nothing in the pipeline invents a value silently: where Horizons
publishes no gravitational parameter, the importer **skips the body and says so** rather than
guessing, because a fabricated mass would propagate into every orbit that depends on it and
look entirely plausible while doing so.
