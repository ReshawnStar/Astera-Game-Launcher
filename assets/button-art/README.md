# Star's button artwork

Revised flat sheets supplied 2026-10-26; both 2164 × 727 RGBA PNGs, copied unchanged:

- `button-atlas.png`: Downloads/Astera/Buttons.png
  SHA256 `30AFF22A8952CD968F3ED368DD529607435214B067B546FD3F0C1038A1B9027B`
- `button-mask.png`: Downloads/Astera/Button Mask.png
  SHA256 `320F7AE2FBB44654E10F00BE522C08239F87CF248E0A471293A5ED5092B32F5A`

`ButtonAtlas.cs` maps nine padded source rectangles directly from these sheets.
The nearest aspect ratio is selected; 48px source corner borders are preserved
while the center stretches. Half-pixel sampling prevents seams. The source blue
alpha defines the silhouette for all variants, preserving the original glow.

Violet uses the original color image. Other accents (Teal, Rose, Gold), secondary
buttons, and quiet buttons use the supplied grayscale plate as their shading
source. Hover/pressed/disabled appearances are cached in memory. Color is chosen
in Astera's Settings tab and saved locally. Labels/icons and the animated border
remain live code. No generated image variants replace or modify these originals.

If the sheet layout changes, update the frame coordinates and build validation.
The older `assets/buttons` individual-frame format remains a code fallback;
these supplied artist sheets take precedence in the current build.
