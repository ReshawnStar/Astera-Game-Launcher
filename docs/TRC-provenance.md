# The Renegade Collection support

Astera downloads original add-on archives; it does not embed or rehost them.

- The Renegade Collection 2.6 by Kix, original English archive identified by the translation project's `packaging.fullOriginal` catalogue: https://github.com/ftl-mv-translation/trc/blob/main/mvloc.config.jsonc
- Original bytes: https://raw.githubusercontent.com/ftl-mv-translation/trc/trc-english/Multiverse_-_TRC_2.6.zip
- Size 7,536,655; inspected SHA256 `F34C1D7F69DCE723E7BE3479C0F9D075E2A8858D3947F1AAD1F4C330501338E3`.
- TRC Generated Gibs 1.4.2 by Epirasque/Dalvest, explicitly for TRC2.6; original Drive ID `1BoYFKqPQQJ5CE9kSbufSKx9jWdyeiYRl`, corroborated by https://github.com/ftl-mv-translation/mvinstaller/blob/main/mvinstaller/signatures.py
- Size 3,065,732; inspected SHA256 `927D6FF155E89A46ED2AA543C74E4C4C9C7ABFC84216CEA653EA67E482C81149`.

These are local pins after inspection, not claims of author-published digests. A changed upstream archive is refused until reviewed.

Patch order in one ftlman operation: Assets, Data, optional TRC, optional base Generated Gibs, optional TRC Generated Gibs. TRC debris is selected only when both TRC and debris are enabled. The supported combination is MV5.5.x/TRC2.6; unverified future MV branches are refused with these add-ons selected.

TRC defaults on, including legacy preferences lacking the new field. Explicit opt-out is saved. Installed versions are recorded and compared on future update checks. Every update/removal rebuilds from the original vanilla backup. Changes made by another manager are refused rather than silently discarded; unrelated add-ons are not adopted by this integration.

Verification checks the source archive pins/metadata, every direct TRC image/audio resource, all 180 ordered ship slots (170 without TRC), the enabled `ADDON_TRC` event, and both generated-debris image/layout sets. The game saves/unlocks are outside the managed-file set. Page18 is supplied; ships are not automatically unlocked.
