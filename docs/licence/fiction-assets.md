# Licence audit — the fictional side and shippable art assets

**Family:** fiction + art assets. **Audited:** 2026-09-12. **Bar:** commercial
distribution on the Meta Horizon Store.

Every claim below is marked **VERIFIED** (I read the page or ran the command and
quote the result) or **INFERRED** (reasoned, not observed). Nothing is taken from
another document in this project, from a third-party summary, or from memory.

Not legal advice. This is a record of what the licence texts say, on the date
they were read, so that a lawyer reviewing the build does not have to re-find
them.

---

## 0. The two findings that are not about licensing

Both surfaced during the audit and both are defects, not paperwork.

### 0.1 The app cannot render its own units — VERIFIED

CosmoSynth renders every label through `CosmoText.cs`, which loads Unity's
built-in font:

```
unity/Assets/CosmoSynth/Runtime/UI/CosmoText.cs
    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
```

A search of the whole Unity project for font files returns **nothing** — no
`.ttf`, no `.otf`, no TMP asset, and no `com.unity.ugui` TextMeshPro package in
`Packages/manifest.json`. So this built-in is the only font in the build.

`LegacyRuntime.ttf` is Liberation Sans. Its **licence is fine** (SIL OFL 1.1,
bundling with software explicitly allowed). Its **coverage is not**. I parsed the
cmap table of upstream Liberation Sans 2.1.5 directly
(`data-staging/fonts-dejavu/fontcheck.py`, pure stdlib) and 15 required
codepoints are absent, including:

| Missing | Char | Used for |
|---|---|---|
| U+2609 | ☉ | **solar mass M☉, solar radius R☉, solar luminosity L☉** |
| U+2295 | ⊕ | **Earth mass M⊕, Earth radius R⊕** |
| U+2299 | ⊙ | alternate solar symbol |
| U+263D, U+263E | ☽ ☾ | lunar glyphs |
| U+260A, U+260B | ☊ ☋ | ascending / descending node — orbital elements |
| U+2604, U+2605, U+2606 | ☄ ★ ☆ | comet and star markers |
| U+26E2, U+29CE | ⛢ ⧎ | astronomical Uranus, misc |
| U+2070, U+2071 | ⁰ ⁱ | exponent digits for 10⁻¹¹ notation |

Liberation Sans *does* have all 28 Greek letters checked. It is specifically the
Sun, the Earth, the Moon and the nodes that are missing — the units this app
prints most often.

On device this shows as a blank box or nothing at all, at runtime only, with **no
error in any log**. This is the verification-gap pattern the project already
documents: a screenshot of a panel proves the panel rendered, not that the glyph
exists.

**Fix:** ship DejaVu Sans (staged, licence cleared below). One line in
`CosmoText.cs`. Then gate it with `font.HasCharacter(c)` over the required list
in a play-mode test, so a future font swap fails loudly.

### 0.2 The project ships almost nothing that needs clearing — VERIFIED

I grepped `unity/Assets` for every third-party art format. The complete list of
non-code, non-Unity-package binary assets in the build is:

```
Assets/StreamingAssets/CosmoSynth/surfaces/earth.jpg   238676 B
Assets/StreamingAssets/CosmoSynth/surfaces/moon.jpg    139068 B
```

Both are NASA, both already carry full provenance in `surfaces.json` (source,
reference URL, instrument, retrieval date), and both fall under the NASA terms
cleared in §3.4. **No fonts, no audio, no models, no third-party textures, no
unstated-licence assets anywhere in `Assets/`.** The 24 spacecraft meshes are
NASA 3D Resources, already documented in `docs/data-sources.md`.

The commercial exposure in the current build is: the two NASA JPEGs (clean), the
HYG star catalogue (CC BY-SA — pre-existing finding, not mine), and the font
gap above.

---

## 1. IP safety — the rename list

I read the three star-map variants on the laptop and the whole `~/projects/StarHolo`
tree on triton, and grepped both against ~70 franchise proper nouns
(Star Trek, Star Wars, Dune, Warhammer, Marvel, Doctor Who, Halo, Mass Effect).

### What was actually found — VERIFIED

| Where | What | Whose | Verdict |
|---|---|---|---|
| `holographic_star_map.html:44` | `SYS_MAP // MARVEL_COSMOS` | Marvel Entertainment | **Must go.** A trademark in a visible UI heading. |
| StarHolo — `CLAUDE.md`, `README.md`, `web/index.html`, `web/src/{main,bay,core,planet}.js` | `WARP CORE` ×6 as a scene title | Paramount / Star Trek | **Must go as a device name.** |
| StarHolo — same files | `dilithium` ×5, an engineering-bay part | Paramount / Star Trek | **Must go.** A coined fictional element, not physics. |

`holographic_star_map(1).html` and `(2).html` are **clean** — no franchise terms.

### What is NOT a problem — VERIFIED, and worth saying

- The star-map system names (`Alpha Core Prime`, `Chrono Frontier`, `Desert
  Sentinel`, `Sub-Zero Basin`, `Gaseous Nebula Orbit`, `Outer Core Ring`, `Deep
  Boundary`, `Searing Fragment`) are generic descriptive English. They borrow
  nothing. They are bland, which is a different complaint.
- The IDs `VG-429`, `PF-919`, `DA-125`, `AG-514`, `CH-024`, `AZ-704`, `AR-503`,
  `DD-210` are arbitrary alphanumerics. Nobody owns those.
- The 22 hits for `force` in StarHolo are all `forceUpdate` / `force: true`.
  Grep noise; checked and cleared.
- `warp` also appears as "warp starfield", a *visual effect*. The ordinary
  English verb is not owned. It is the compound **"warp core"**, naming a
  specific fictional device, that is the problem.

### The replacement scheme — the Ninth Field

The organising idea, and the reason it is better than a find-and-replace:
**CosmoSynth's fiction should live in the gap the real data leaves.** HYG ships
10 224 stars with **no usable parallax** — direction measured, distance not known
at all — and the app already refuses to invent distances for them. That is where
the authored worlds go. The fiction then never contradicts a measurement, and the
bridge from real to fictional is a real feature of the sky rather than a portal.

| Concept | Replacement | Why it is safe |
|---|---|---|
| The authored layer | **the Ninth Field** | Ordinary English on a real convention — surveys number their deep fields (Hubble Deep Field, GOODS-South). An authored field that does not exist. |
| Catalogue prefix | **NF** — `NF 4471` | Not in use by any real star catalogue (HD, HIP, GJ, TYC, SAO, NGC, IC, 2MASS). Mirrors real designation grammar, so the bridge reads naturally. |
| Individual worlds | **generated** — `NF 3203 Riadri` | See below. |
| Stations | `NF 3203 Anchorage` / `Waypoint` / `Relay` / `Drydock` / `Beacon` / `Transit` | Plain English nautical nouns, named off their primary as real installations are. |
| "warp core" | **the pair cell** / **pair-cell drive**; the room is the **drive bay** | Physics vocabulary (electron–positron pair production). You cannot trademark a physical process, and it suits an app built on real physics. |
| "dilithium" | **nothing — it runs on stored antimatter** | Do not invent a magic crystal. Every invented `-ium` risks colliding with someone's; real physics collides with no one. |
| "warp starfield" | **transit blur** / **starstreak** | Descriptive; drops the association for free. |

**World names are generated, not chosen.** This is the part that actually
protects the project. A human asked for "alien-sounding" names reaches for the
ones they have already heard — which is exactly how "warp core" and "dilithium"
got into StarHolo, and nobody did it on purpose.
`data-staging/fiction-naming-scheme/ninthfield_names.py` makes each name a fixed
function of a real galactic coordinate: invented syllable tables, a BLAKE2b bit
stream keyed on (l, b), and an explicit blocklist screen over every output.

Consequences: a generated name cannot be unconscious recall; the scheme is
demonstrably original because it is a published algorithm rather than a list of
inventions; and the name **encodes the direction**, so `NF 3203 Riadri` is
reversible to a point on the real sky the user can fly to.

Self-check output, VERIFIED by running it:

```
demo OK  7080 directions, 7066 distinct names, 0.20% collisions
  l= 120.50 b=-30.25   NF 3203 Riadri     NF 3203 Anchorage
  l= 275.25 b= -8.75   NF 0779 Helfa      NF 0779 Anchorage
  l=  44.40 b= +3.30   NF 2081 Solurn     NF 2081 Anchorage
```

`demo()` asserts determinism, a collision rate under 2% across 7080 sky
directions, that the IP screen holds over **all** 7066 generated names rather
than a spot check, and that designations are well-formed.

A coined name **"Aletheon"** was considered and dropped: a web search on
2026-09-12 found it is the title of a published religious work by Adi Da Samraj.
Not a franchise collision, but a distinct existing work and avoidable.

### The edits

```
holographic_star_map.html:44
  -  SYS_MAP // MARVEL_COSMOS
  +  SYS_MAP // NINTH FIELD

StarHolo (6 occurrences of the scene title)
  -  WARP CORE       ->  DRIVE BAY
  -  warp core       ->  pair cell
  -  dilithium       ->  pair-cell lattice   (a part, not a substance)
  -  warp starfield  ->  transit blur
```

### What this does not cover — stated plainly

**Visual borrowing, which no grep finds.** Two things to check by eye before
shipping: station **silhouettes** (a shape that reads as a Death Star or a Borg
cube is a problem no rename fixes), and whether the HUD is a recognisable
recreation of a specific film's interface. Orange-on-black holographic UI is a
genre convention and is fine; the current star-map HUD is generic. I checked the
words. Somebody has to look at the pictures.

---

## 2. Decision table

| Source | Verdict | Decisive evidence (verbatim), URL, date read | Substitute |
|---|---|---|---|
| **DejaVu fonts** 2.37 | **OK** | *"Permission is hereby granted, free of charge … to use, copy, merge, publish, distribute, and/or sell copies of the Font Software"* and *"The Font Software may be sold as part of a larger software package but no copy of one or more of the Font Software typefaces may be sold by itself."* — the `LICENSE` **inside the release tarball**, staged as `fonts-dejavu/LICENCE-upstream.txt`; cross-read at <https://dejavu-fonts.github.io/License.html>, 2026-09-12 | — (this is the substitute) |
| **Liberation Sans** (= Unity `LegacyRuntime.ttf`) | **OK on licence, REPLACE on coverage** | *"This Font Software is licensed under the SIL Open Font License,"* — the `LICENSE` inside `liberation-fonts-ttf-2.1.5.tar.gz`; OFL text: *"Original or Modified Versions of the Font Software may be bundled, redistributed and/or sold with any software, provided that each copy contains the above copyright notice and this license."* <https://openfontlicense.org/open-font-license-official-text/>, 2026-09-12 | **DejaVu Sans** — 15 required codepoints absent, see §0.1 |
| **Noto Sans** / **Noto Sans Symbols 2** | **OK on licence, REJECTED on fitness** | SIL OFL 1.1, same OFL text as above | **DejaVu Sans.** Noto Sans misses 31 required codepoints (every planetary symbol, nine maths operators); Symbols 2 misses 153 and **does not have U+2640–U+2647 either**, so the fallback chain still fails on Saturn |
| **Kenney Interface Sounds** | **OK** | *"License: (Creative Commons Zero, CC0) … This content is free to use in personal, educational and commercial projects."* — `License.txt` **inside the pack itself**; cross-read at <https://kenney.nl/support>: *"All game assets on the asset pages are public domain licensed (CC0)." "You're free to use them, even in commercial projects."* 2026-09-12 | — |
| **Freesound** | **BLOCKED as a source** | *"freesound lets the user select one of three licenses for their sounds"* — one of the three is **CC BY-NC**. And: *"To download a sound, first make sure you are logged into your registered account."* <https://freesound.org/help/faq/>, 2026-09-12 | Kenney (UI) + synthesised bed |
| **OpenGameArt** | **CONDITIONAL per file; not used** | *"Yes, you can use this art. Even in commercial projects."* — but the same FAQ lists **CC BY-SA 3.0/4.0** and **GPL 2.0/3.0** among accepted licences. <https://opengameart.org/content/faq>, 2026-09-12 | Kenney (UI) + synthesised bed |
| **Ambient bed** | **OK — original work** | No upstream. Generated by `audio-original-ambient/make_ambient.py`, this project's own work | — |
| **ambientCG** materials | **OK** | *"All ambientCG assets are provided under the Creative Commons CC0 1.0 Universal License." "You can copy, modify, distribute and perform the assets, even for commercial purposes, all without asking permission."* <https://docs.ambientcg.com/license/> (302 from `/license`), 2026-09-12 | — |
| **Poly Haven** HDRI | **OK** | *"All assets (HDRIs, textures and 3D models) on this site are … licensed as CC0." "You can use our assets for any purpose, including commercial work." "You do not need to give credit or attribution when using them (although it is appreciated)."* <https://polyhaven.com/license>, 2026-09-12 | — |
| **NASA imagery** (nebula plates; also the two shipped surface JPEGs) | **OK, with three non-copyright restrictions** | *"NASA content – images, audio, video, and media files used in the rendition of 3-dimensional models, such as texture maps and polygon data in any format – generally are not subject to copyright in the United States."* and *"If the NASA material is to be used for commercial purposes, including advertisements, it must not explicitly or implicitly convey NASA's endorsement of commercial goods or services."* <https://www.nasa.gov/nasa-brand-center/images-and-media/>, 2026-09-12 | — |
| **ESA/Hubble imagery** | **OK — CC BY 4.0**, not used but cleared | *"Creative Commons Attribution 4.0 International license"*; materials *"may be reproduced without fee provided they are clearly and visibly credited"*; artwork based on Hubble images *"may be sold for your own profit as long as you credit clearly and visibly."* <https://esahubble.org/copyright/>, 2026-09-12 | — (available if deeper plates are wanted) |

### The three NASA restrictions, because they are not about copyright

1. **No implied endorsement.** The app and its store listing must not suggest
   NASA endorses it. Credit lines fine; "NASA-powered" or NASA imagery in an
   advert for the app, not.
2. **The NASA Insignia ("meatball") and Logotype are protected by law** and need
   specific approval for commercial use. Keep both out of the app, the icon, the
   splash screen, and every store asset. This is trademark, and it survives the
   fact that the images are public domain. Check plates for a burned-in logo.
3. **Identifiable people** in NASA images may raise privacy or publicity rights.
   None of the staged plates contain people; the archive is full of ones that do.

### The ESA/Hubble exceptions worth recording

Logos need prior written consent; **music, scientific papers and code are not
covered** by the CC BY grant; and the credit wording must be reproduced
unaltered, e.g. "ESA/Hubble".

---

## 3. Staged inventory

Root: `~/projects/CosmoSynth/data-staging/`. Every directory has `SOURCE.txt`,
`LICENCE.txt` and `NOTES.md`. **Total 57 833 405 bytes (57.8 MB)** against a
400 MB budget. `df -h /` before and after: 15 GB free, unchanged — no fetch here
came close to the 8 GB abort threshold, and `fetch_textures.py` / `fetch_nebula.py`
both check it before each download.

| Path | Bytes | sha256 | Count |
|---|---|---|---|
| `fonts-dejavu/DejaVuSans.ttf` | 757 076 | `7da195a74c55bef988d0d48f9508bd5d849425c1770dba5d7bfc6ce9ed848954` | 5918 codepoints |
| `fonts-dejavu/DejaVuSans-Bold.ttf` | 705 684 | `e6476c1b80502924294eed40894c5b18e06c181444ca953e5334262df9c27724` | — |
| `fonts-dejavu/DejaVuSansMono.ttf` | 340 712 | `b4a6c3e4faab8773f4ff761d56451646409f29abedd68f05d38c2df667d3c582` | 3322 codepoints |
| `fonts-dejavu/LICENCE-upstream.txt` | 8 816 | — | the release's own LICENSE |
| `fonts-dejavu/coverage-report.txt` + `fontcheck.py` | 8 393 | — | the check and its output |
| `audio-kenney-cc0/kenney_interface-sounds.zip` | 834 536 | `f2193d072726d6758a5f7871b2dcc54dcce0d5c35c6f0a62f92549b327c81232` | **100** `.ogg` files |
| `audio-original-ambient/cosmosynth_ambient_bed_32s.wav` | 2 048 044 | `ebbaf1ec7c1c39d4fcafcd33268ee4ac0de6a77e86893930d5c04e3b92e95a3e` | 1 024 000 frames, 32 kHz mono |
| `textures-ambientcg-cc0/Rock030_1K-JPG.zip` | 9 677 446 | `2fd0bc2d214e2487c19062ed0798a85dd135b41cfc12bbaed42d5acae238f989` | 11 files |
| `textures-ambientcg-cc0/Rock023_1K-JPG.zip` | 7 832 742 | `8793a32778c79cd62eb26a200c85b3747d119e7c570a7318bd124fb8342a7b04` | 11 files |
| `textures-ambientcg-cc0/Lava004_1K-JPG.zip` | 8 986 332 | `fee9256befd26b59a7ff452ab6379dab4cdace5d7bee7f78cd9ad8176dd67c28` | 11 files |
| `textures-ambientcg-cc0/Snow010A_1K-JPG.zip` | 6 581 401 | `06852017cc69c885b4ee375f4dc8cf16184456b7f6f02de821fb7d2f3cbe8768` | 11 files |
| `textures-ambientcg-cc0/MetalPlates006_1K-JPG.zip` | 5 247 281 | `0b7d3736bf76103f158bc2f550e28b54a2ea9ba553c9253ac172eca3bafb02b5` | 11 files |
| `textures-ambientcg-cc0/Ground037_1K-JPG.zip` | 10 574 958 | `4e69db2b7251b1736fa70fe6fe4ab80d64bd220117dd5dd4c7caaead51c2574e` | 11 files |
| `textures-polyhaven-cc0/dikhololo_night_1k.hdr` | 1 745 132 | `6861489f983cdc0c22435b781a3487171bd25f25f9cc52c46ac402d48e08249e` | 1k equirect RGBE |
| `imagery-nebula-nasa/PIA23405.jpg` | 61 919 | `c42116636678e66c42dd9e98836002289426183bb17a295d19b604e8ba89d2ec` | 1280×521 RGB |
| `imagery-nebula-nasa/PIA22568.jpg` | 110 647 | `cd07de032080287c66f4a7ee8e8fe1f2d7291061cda8534986a6ddfd0026780e` | 1280×473 RGB |
| `imagery-nebula-nasa/GSFC_20171208_Archive_e001435.jpg` | 194 070 | `74a36d0407f4262d336067622441bdce7addd88e242e885d4e67d1e13d6300e0` | 1280×1280 RGB |
| `fiction-naming-scheme/ninthfield_names.py` | 6 776 | `b3da68742e57e50e5e2278da49434d8e2c6f9a7eef5cbe3665c92474123cc880` | 7066 names over 7080 directions |
| `unity-builtin-font-liberation/LiberationSans-Regular.ttf` | 410 712 | `76d04c18ea243f426b7de1f3ad208e927008f961dc5945e5aad352d0dfde8ee8` | 2327 codepoints (the record of what ships today) |
| `REJECTED-noto-sans-symbols2/NotoSansSymbols2-Regular.ttf` | 671 568 | `c4a0a80f0041ce4be81e2478faad22776d23edb98ae3f0d19bd37044820ecf9d` | 2641 codepoints |
| `REJECTED-freesound/`, `REJECTED-opengameart/` | docs only | — | no files fetched |

Reproduce any of it: `fonts-dejavu/fontcheck.py`, `audio-original-ambient/make_ambient.py`,
`fiction-naming-scheme/ninthfield_names.py`, and `_tools/fetch_textures.py` /
`_tools/fetch_nebula.py`. All are standard library except the two that use numpy.

> **Trap worth recording:** `make_ambient.py` must not be run from `/tmp` on
> triton. Another agent has left a `/tmp/inspect.py` that shadows the stdlib
> `inspect` module; numpy then fails to import with `No module named 'bpy'`,
> which points nowhere near the cause.

---

## 4. Attribution strings for the app credits

Use verbatim. Only the first is strictly obligatory; the rest are CC0 or public
domain, where crediting is optional but costs one line and makes the provenance
self-evident to a store reviewer.

**Required**

```
DejaVu Sans, DejaVu Sans Bold and DejaVu Sans Mono.
Fonts (c) 2003 Bitstream, Inc. Bitstream Vera is a trademark of Bitstream, Inc.
Arev glyphs (c) Tavmjong Bah. DejaVu changes are in the public domain.
Used under the Bitstream Vera Fonts Copyright.
```

**Required only if Liberation Sans is kept anywhere in the build**

```
Liberation Sans. Digitized data (c) 2010 Google Corporation with Reserved Font
Arimo, Tinos and Cousine. (c) 2012 Red Hat, Inc. with Reserved Font Name
Liberation. Licensed under the SIL Open Font License 1.1.
```

**Optional but recommended**

```
UI sounds by Kenney (kenney.nl), released under CC0 1.0 Universal.
Surface materials from ambientCG.com, licensed under CC0 1.0 Universal.
HDRI environment from Poly Haven (polyhaven.com), licensed under CC0 1.0 Universal.
Ambient score generated procedurally for CosmoSynth.
```

**Per-image, required by NASA's request for attribution** — the credit differs
per file and was read from the API record for each rather than assumed, because
images.nasa.gov also hosts ESA and STScI material:

```
Perseus Molecular Cloud — NASA/JPL-Caltech (Spitzer Space Telescope).
Cat's Paw Nebula — NASA/JPL-Caltech (Spitzer Space Telescope).
Solstice coronal mass ejection, 20 June 2013 — NASA/GSFC (SDO).
Earth surface — NASA Visible Earth, Blue Marble (Terra/MODIS composite).
Lunar surface — NASA SVS / LRO, LROC WAC colour global mosaic.
```

**If ESA/Hubble material is ever added:** the credit must be reproduced
unaltered and visibly, e.g. `ESA/Hubble`.

---

## 5. What I could not verify

| Claim | Status | Why |
|---|---|---|
| Unity's `LegacyRuntime.ttf` **is** Liberation Sans | **INFERRED** | The file is not on disk — it lives inside Unity's built-in resources blob (`find / -name LegacyRuntime.ttf` returns nothing). I verified upstream Liberation Sans 2.1.5 byte-for-byte instead. Unity's own docs and release notes say the built-in is Liberation Sans; I did not confirm the embedded build's cmap. **The fix does not depend on which it is:** ship a font we chose and checked. |
| Liberation Sans's gaps appear as tofu **on a Quest 3** | **INFERRED** | Verified: the codepoints are absent from the font. Not verified: the on-device visual. Unity's dynamic-font path renders `.notdef` for an absent glyph. This wants a device screenshot of a panel printing `M☉`. |
| The eleven files inside each ambientCG zip | **INFERRED** for their individual roles | Verified: eleven files per zip, and that each zip is a real zip. Not verified: the exact filenames per map type. Unzip one and read the names before wiring it up — the GL vs DX normal-map convention is the thing to get wrong. |
| Kenney's `kenney.nl/support` page text | **VERIFIED** via WebFetch summary quoting it, **and independently** by the `License.txt` inside the pack, which is the stronger evidence | — |
| That the ~70-name franchise grep is exhaustive | **INFERRED** | It covers the franchises named in the task plus Doctor Who, Halo, Mass Effect and Elder Scrolls. It cannot find visual borrowing, paraphrase, or a franchise I did not list. |
| The `NF` prefix collides with no real catalogue | **INFERRED** | Checked against the catalogues CosmoSynth actually uses (HD, HIP, GJ, TYC, SAO, NGC, IC, 2MASS, UCAC4). Not checked against the full VizieR catalogue-acronym registry. |
| Freesound / OpenGameArt file-level licences | **NOT CHECKED** | Deliberately. Neither was used; both need per-file clearance, and Freesound needs an account this agent must not create. |

---

## 6. Ledger rows for `docs/data-sources.md`

Add under a new heading, **Art assets and the authored layer**. Note these are
*art*, not data, so they do not carry per-field provenance — they carry
per-asset provenance, which is what the staging directories hold.

| Asset | Source | Licence | Verdict | Required credit |
|---|---|---|---|---|
| UI and label font | [DejaVu fonts 2.37](https://dejavu-fonts.github.io/) | Bitstream Vera Fonts Copyright (permissive); DejaVu changes public domain | **OK** — sale as part of a larger package explicitly permitted | Yes, see §4. Ship `LICENCE-upstream.txt` in StreamingAssets |
| *(currently shipped)* Unity built-in font | Liberation Sans via `LegacyRuntime.ttf` | SIL OFL 1.1 | **Replace** — licence fine, 15 required codepoints absent incl. U+2609 ☉ and U+2295 ⊕ | Only if retained |
| UI sounds | [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds) | CC0 1.0 Universal | **OK** — no obligations | Optional |
| Ambient bed | Generated — `data-staging/audio-original-ambient/make_ambient.py` | Original work, project-owned | **OK** — no third-party claim | None |
| Authored-world surface materials | [ambientCG](https://ambientcg.com/) | CC0 1.0 Universal | **OK** — no obligations | Optional |
| Authored-world light probe | [Poly Haven](https://polyhaven.com/) | CC0 1.0 Universal | **OK** — no obligations | Optional |
| Authored backdrop plates | [NASA images](https://images.nasa.gov/) | US Government work, not subject to US copyright | **OK**, but: no implied endorsement, **no NASA insignia in app or store art**, check for identifiable people | Yes, per-image; read the credit field, do not assume "NASA" |
| Deep-sky plates (available, unused) | [ESA/Hubble](https://esahubble.org/) | CC BY 4.0 | **OK** — commercial sale explicitly permitted with visible credit | Yes, wording unaltered |
| Audio marketplace | [Freesound](https://freesound.org/) | Per-file: CC0 / CC BY / **CC BY-NC** | **BLOCKED as a source.** Individual CC0 sounds are fine after per-file clearance; download needs an account | Per file |
| Art marketplace | [OpenGameArt](https://opengameart.org/) | Per-file: CC0 / CC BY / **CC BY-SA** / **GPL** | **CONDITIONAL per file.** GPL submissions are unusable in a closed-source APK | Per file, verbatim from the artist's notice field |
| Fictional naming scheme | `data-staging/fiction-naming-scheme/ninthfield_names.py` | Original work, project-owned | **OK** | None |

### Amend the existing "rule this table exists to enforce"

The document's current rule is *"Hosted by NASA does not mean public domain."*
This audit adds its mirror image for art:

> **"CC0 site" does not mean "CC0 file."** ambientCG, Poly Haven and Kenney
> licence their whole catalogue at once, and are cheap to clear for that reason.
> Freesound and OpenGameArt licence *per upload*, and a `.wav` on disk carries no
> record of which licence it arrived under. Clear those one file at a time or not
> at all.

And one more, from §0.1:

> **A licence audit is not a fitness audit.** Liberation Sans passes every
> licensing question this document asks and still cannot print `M☉`. Ask both
> questions.

---

## 7. What to do next, shortest path

1. **Ship DejaVu Sans.** Copy `data-staging/fonts-dejavu/DejaVuSans.ttf` into
   `Assets/CosmoSynth/Fonts/`, change one line in `CosmoText.cs`, add
   `LICENCE-upstream.txt` to StreamingAssets, add the credit string.
2. **Add the glyph regression test** — `font.HasCharacter(c)` over the required
   codepoint list in `fontcheck.py`'s `NEED`. Without it the font can be swapped
   back silently.
3. **Do the four renames** in §1. Six occurrences in StarHolo, one in the star map.
4. Wire a dozen Kenney sounds and the ambient bed; leave the other 88 in staging.
5. Add an **Authored** provenance category beside Measured and Inferred, and put
   every `NF` body and every ambientCG surface in it. Authored worlds must never
   borrow a Measured badge, and measured bodies must never wear a CC0 rock texture.
