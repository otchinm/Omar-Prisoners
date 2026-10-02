# Texture pipeline

`build_textures.py` regenerates every texture in
`Assets/PrisonersOfOmar/Resources/Textures/{Env,Decals,Props,Foliage,Sky,FX,UI}` (plus
`UI/font_vhs.json` and `UI/font_vhs_big.json`). The names, sizes and alpha modes follow `Docs/ASSETS.md`.
The build is deterministic: every texture seeds its own RNG from its path, so building one texture or all
of them gives the same pixels.

```sh
pip install numpy scipy pillow OpenEXR
python3 Tools/AssetPipeline/textures/build_textures.py                 # everything (about 2 min)
python3 Tools/AssetPipeline/textures/build_textures.py --only 'Env/*'  # a subset (glob, can be repeated)
python3 Tools/AssetPipeline/textures/build_textures.py --list          # what exists, sizes, alpha
python3 Tools/AssetPipeline/textures/build_textures.py --qa /tmp/qa    # + contact sheets (keep them out of the repo)
```

## Look

The target is the Puppet Combo look (Murder House, Stay Out of the House): textures that read as
real photographs that have been crushed down to PS1 resolution. Each texture is built in three stages.

1. **Built 4–8× oversize.** The structure is procedural: boards, tiles, bricks, damask, signs and so on.
   The surface detail comes from **real photos**, mixed in as tileable high-pass layers. Uneven
   exposure, dirt, stains, water damage, drips, rust and chipped paint are layered on top.
2. **`core.degrade()`** does the PS1 / VHS processing:
   - box downscale to the final size, then a slight unsharp mask
   - posterize to 5 bits per channel with a light 4×4 Bayer dither
   - a JPEG round trip at quality 34–60 with 4:2:0 chroma. For tiling textures this runs on a 3×3 tiled copy, so block artefacts wrap correctly.
   - slight desaturation and darkening, and no pure white
   - cutouts get hard 0/255 alpha, with their colour bled into the transparent texels
3. **Tiling.** Tiling textures use only periodic noise (FFT 1/f noise, wrap-mode filters, wrapped
   drawing), and their photo layers are made seamless with a two-pass offset-and-blend
   (`core.make_tileable`). The `--qa` contact sheets show tiling textures repeated 2×2.

Unity is expected to import these point-filtered with no mip maps (see `Scripts/Editor`). Every
non-UI texture is a power of two. The UI stills `ending_*` (256×144) and `vhs_cover` (512×400) are not,
so their importer needs `npotScale = None`. The font atlases are padded to powers of two.

## Sources (downloaded on first run into `~/.cache/poc_assets`, never committed)

| source | license | used for |
|---|---|---|
| scikit-image 0.26 wheel (PyPI): `skimage/data/` `gravel`, `grass`, `moon`, `camera`, `retina` … | CC0 / public domain (see each photo's docstring in `skimage/data/_fetchers.py`) | grain / structure layers: gravel → concrete, asphalt, dirt, plaster, rust; grass → dead grass, wood fibre (stretched), hay, burlap; moon → plaster; retina → raw meat |
| `@pmndrs/assets` 1.7.0 (npm) `hdri/*.exr.js` (Poly Haven HDRIs, 512×256) | CC0 | `sky` HDRI clouds → night sky panorama (wraps because the equirect does); `sunrise` field photo → `painting_landscape` |
| fonts in `fonts/` (from the `@fontsource` npm packages) | SIL OFL 1.1 (licence texts next to them) | VT323 → bitmap UI fonts; Anton → titles and labels; Overpass ExtraBold → road signs and labels; Allerta Stencil → stencils |

Set `POC_ASSET_CACHE` to use a different cache directory.

## Layout

```
build_textures.py   entry point (CLI)
potex/core.py       registry (@texture), RNG, periodic noise, masks, grime library, degrade(), text
potex/sources.py    download, cache and load the photos / HDRIs
potex/mat.py        shared materials: photo grain, wood grain, boards, tile / brick grids, rust, paint chips
potex/env.py        Textures/Env      (47 tiling materials incl. chainlink / barbed wire cutouts)
potex/decals.py     Textures/Decals   (blood, graffiti, scrawled writing, stains, road paint, cracks)
potex/props.py      Textures/Props    (furniture, signs, barrels, cars, doors, windows, …)
potex/foliage.py    Textures/Foliage  (recursive dead trees, conifers, bushes, grass, corn, treeline)
potex/sky.py        Textures/Sky
potex/fx.py         Textures/FX       (4-frame looping flames / fire / explosions, VHS noise sources)
potex/ui.py         Textures/UI       (fonts + JSON, logo, icons, vignettes, VHS cover, ending stills)
potex/qa.py         contact sheets
fonts/              the .woff fonts + OFL licence texts
```

### Bitmap font JSON (`UI/font_vhs.json`, `UI/font_vhs_big.json`)

```json
{"cellW":10,"cellH":18,"cols":16,"rows":7,"first":32,"glyphs":" !\"#…~▶◀■●▲▼","advance":8,"lineHeight":18,
 "atlasW":256,"atlasH":128,"padding":1,"glyphW":8,"glyphH":16,"baseline":14,"source":"…","note":"…"}
```

Glyph *i* (its position in `glyphs`) is the `cellW`×`cellH` cell at `(i % cols * cellW, i / cols * cellH)`,
measured from the top-left of the atlas. Its ink starts at `(padding, padding)` inside the cell. To draw
a glyph, place the cell at `(penX - padding, penY - padding)` and then advance `penX` by `advance`. A new
line moves down by `lineHeight`. `baseline` is the baseline's row inside a cell. The glyphs are white with
hard alpha. The ASCII glyphs 32–126 come from VT323, thresholded. ▶ ◀ ■ ● ▲ ▼ are drawn by hand. The big
font has the same layout at twice the size (16×32 glyphs, 28 columns, a 512×256 atlas).
