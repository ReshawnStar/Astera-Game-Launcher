# Astera 0.1.21

- Steam games without a local icon can use their store portrait as a library image.
- Steam artwork downloads now follow the selected game and stop when you switch away.
- Downloaded images are resized and cached locally, with a size limit for the cache.
- Custom icons and backgrounds keep priority over automatic artwork.

- itch.io supports plain receipts and still-image cover URLs alongside compressed receipts.
- itch cover downloads now cancel when switching games, with a bounded local cache.

## Known issue

One tester reported Windows Defender detecting Astera 0.1.19 as Program:Win32/Contebrew.A!ml.
We believe the detection is incorrect; Microsoft has not reviewed it yet.