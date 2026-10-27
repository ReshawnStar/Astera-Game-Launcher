# Astera 0.1.24

- Reduced library flicker while hovering over games and playing animated icons.
- Animated icons now refresh their image area without redrawing the whole row.
- GIF playback follows the artwork's frame timing and uses less idle timer work.

## Known issue

One tester reported Windows Defender detecting Astera 0.1.19 as Program:Win32/Contebrew.A!ml.
We believe the detection is incorrect; Microsoft has not reviewed it yet.