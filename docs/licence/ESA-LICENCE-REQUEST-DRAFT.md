> **Status, decided by the owner on 2026-09-12: DO NOT SEND YET.** There is no app to license
> data for - the render bug is unfixed, nothing is built, and nothing has been tested on a
> headset. Send this only when a working build exists and deployment is actually being planned,
> and only for whatever distribution is real at that point. Until then the app is a private
> development build, which is inside the non-commercial terms, and the ledger records what would
> have to be swapped if the answer is no.

# Draft: commercial licence request to ESA (for the owner to send)

**Why this one email matters.** ESA's science-archive terms put Gaia DR3, Hipparcos and Tycho-2
under CC BY-NC 3.0 IGO, and name `data.licences@esa.int` as the route to commercial use. Those
three are the upstream of the HYG and AT-HYG catalogues the app ships today, so one answer decides
whether CosmoSynth can keep measured stellar distances — the thing that makes the star field three
dimensional rather than a dome — in a commercial build. Without them the fallbacks are the Yale
Bright Star Catalogue (about 9,000 naked-eye stars) and UCAC4 (positions and magnitudes, no
parallaxes), which is a sky you can look at but not fly through.

Send from your own address; this is a request, not legal advice.

---

**To:** data.licences@esa.int
**Subject:** Commercial licence request — Gaia DR3, Hipparcos and Tycho-2 in an educational VR app

Dear ESA Data Licensing team,

I am requesting authorisation for commercial use of data from the ESA Space Science Archives, as
described in the Terms and Conditions for the use of data in the ESA space science archives.

**Who I am.** Ajinkya Gorad, an independent developer in Helsinki, Finland (PhD, Aalto University,
2025). The work is self-funded.

**The product.** CosmoSynth, a mixed-reality astronomy application for Meta Quest 3 headsets,
intended for the Meta Horizon Store at a modest one-off price. It lets a viewer stand inside a
scale model of the universe and travel continuously outward: from the Solar System, through the
nearby stars, into the structure of the Milky Way, out to the Local Group and the large-scale
distribution of galaxies, and finally to the cosmic microwave background. Every quantity shown
carries a provenance label naming its source and stating whether it is measured, inferred or
generated, and the application shows the viewer that label on request. Its purpose is educational.

**The data requested.**

1. *Gaia DR3* (`gaiadr3.gaia_source`): a magnitude-limited extract of roughly 100,000 to 500,000
   stars — source identifier, right ascension, declination, parallax, proper motion, radial
   velocity, G magnitude and BP−RP colour.
2. *The Hipparcos catalogue* (I/239) and *Tycho-2* (I/259): the equivalent astrometric and
   photometric columns.
3. Where relevant, derived catalogues built on these, such as the HYG compilation.

**How it would be used.** The extract would be converted offline into a compact binary table and
shipped inside the application, which works entirely offline. It is used to place each star at its
measured position and to derive its colour and brightness. The data is not exposed for bulk export
and is not resold as data; it is rendered as a view of the sky.

**Attribution.** The application would carry the credit "Credit: ESA, Gaia DPAC" for Gaia and the
equivalent ESA credit for Hipparcos and Tycho-2, in an in-app credits panel and in the store
listing, alongside the acknowledgement text Gaia DPAC requests.

I would be glad to provide a build for review, or to accept conditions on the size or form of the
extract. If a licence cannot be granted, I would appreciate knowing whether any ESA-hosted
astrometric data is available under terms permitting commercial use, so that I can plan around it.

With thanks,

Ajinkya Gorad
ajinkyagorad@gmail.com

---

**If the answer is no**, the app ships its commercial build on the public-domain fallbacks (UCAC4,
Yale BSC5, IAU star names, SDSS, NASA and CC0 sources) and keeps the Gaia-derived star field behind
the licence switch for personal, non-commercial builds. Every layer is designed to be droppable
without a code change, so this stays a build-time choice rather than a rewrite.
