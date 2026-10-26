# Astera UI Assets v1 — handoff to Vale

This package contains **21 actual independent icons** (editable SVG + transparent 128×128 PNG each), **36 separate UI text labels** (editable SVG + transparent PNG), a **full typography/character-set specimen image**, and a practical CSS typography guide.

## Recommended integration

- Place SVGs from `icons/svg/` into your app's assets folder. They are 24×24 viewBox line icons with transparent backgrounds; CSS colors can be changed because they use `currentColor`. SVGs default to pale white/lavender.
- Or use transparent raster icons from `icons/png_128/`, downscaling to 16, 20, 24, or 28 px (use the SVGs for best crispness).
- Buttons: background art is **separate**. Overlay an SVG icon plus native app text. Do not flatten icon/text/glow into the button image.
- The separate label SVG/PNG files are optional design assets; for real localization/accessibility/resizing, render live text in the UI instead.
- Vale handles the animated perimeter LED line independently. **No LEDs or backgrounds are embedded in the icons.**
- Font stack: Segoe UI Variable → Segoe UI → Inter → Arial → sans-serif. The specimen raster uses a locally installed Inter fallback to approximate Segoe UI. **No font files are included.**
- Typography: brand 30px/700, game title 40px/700, section 17px/650, sidebar 15px/600, primary buttons 15px/600, body 14px/400, captions 12px/400, eyebrow 11px/600 with wide tracking.
- The transparent PNGs of the text labels are previews only; avoid hard-coding them into production buttons since actual text rendering will scale better.
- Colors: icon #EDF1FF; muted copy #A9B8DC; violet accent #5844F4; panel #17213B.

## Files

- `icons/svg/*.svg`: 21 transparent vector symbols
- `icons/png_128/*.png`: 21 transparent PNG copies
- `text_labels/svg/*.svg`: separate label vectors (no button shape)
- `text_labels/png/*.png`: separate label PNGs at 2x density
- `guides/astera-typography.css`: text styles and theme tokens
- `guides/ui-text-labels.txt`: labels for localization/copy
- `guides/icon-usage-map.csv`: intent and recommended button text
- `previews/astera_icon_preview.png`: dark-backed icon contact sheet
- `previews/astera_typography_layout.png`: complete typography specimen with A–Z / a–z / 0–9 and samples

Notes: These are new lightweight vector assets drawn for Astera's existing simple rounded UI. No images of Cipher/Vale, no stars or ornamental button decorations, and no font binaries are included. Icon art and button text remain separate.
