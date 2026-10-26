# Button artwork

The current build uses Star's complete sheets in `../button-art/`. See that
folder's README for the original files, frame mapping, masks and color options.
The format below is retained as a fallback for earlier individual frames.

Optional transparent PNG frames; the app works without them. Keep captions and
icons out of the image: Astera draws these so Play, Set up and Install remain
correct and accessible.

Names: `primary-normal.png`, `secondary-normal.png`, `quiet-normal.png`.
For each style, optional `-hover`, `-pressed`, and `-disabled` versions replace
`-normal`. Missing states reuse the normal frame; disabled fallback is dimmed.

512 × 96 is a useful starting size. The renderer stretches the centre and edges
separately, keeping the outer 24 pixels as corners (scaled to button height).
Keep corner details within that border. PNGs must be 3–4096 pixels per side and
at most 8 MB. Artwork is embedded at build time, with no runtime file setup.

Primary: Play and Install. Secondary: Check, Download, Settings and Exit.
Quiet: Restore and Cancel. A keyboard focus ring is drawn over every frame.
