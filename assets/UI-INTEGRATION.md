# UI asset integration v0.1.7

Star supplied the revised button sheets and `Astera_UI_Assets_v1` on 2026-10-26
and explicitly approved publishing these originals in the public Astera repo.

- Original input files remain unchanged. `ui-v1/` retains the complete handoff:
  21 SVG/PNG icon pairs, 36 SVG/PNG text label pairs, guides and previews.
- The typography package includes a font stack/specimen, **no font binaries**.
  Astera uses installed Segoe UI Variable/Text, then Segoe UI, Inter, Arial.
  Button labels use Segoe UI Semibold where available. Guide pixel sizes are
  converted to GDI points, rather than treating 40px as a 40pt font.
- PNG icons are embedded at 128px and downsampled at runtime. Their visible ink
  bounds determine aspect-preserving placement, so transparent margins do not
  shrink Play or distort arrows/ellipsis. Captions remain live/accessibile text.
- Only currently implemented actions expose their icons. Pause, retry and other
  supplied symbols do not introduce nonfunctional buttons.
- `ButtonAtlas.cs` maps the revised flat button silhouettes; the grayscale plate
  supplies accent and secondary shades. Corners and transparent padding survive
  resize. The independently animated glow remains on Play.
- The complete handoff is retained as source/design reference. Text-label PNGs,
  specimen images, CSS and SVGs are not rasterized into live button captions.

The supplied icon map and typography guide remain under `ui-v1/guides/`.

v0.1.8 overrides four SVG/128px pairs with Cipher's Icon Fixes v1.2:
folder-open, package-check, refresh-cw and scroll-text. The full patch handoff,
including its 16/24/32/64px variants, is retained in `icon-fixes-v1.2/`.
The v1 import manifest records the original import, before those replacements.
