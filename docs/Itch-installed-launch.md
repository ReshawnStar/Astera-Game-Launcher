# itch.io installed-game launch repair — 0.1.32

The old handoff sent `itch://install?game_id=…&launch`. The installed itch26.1.9 handler treats the host `install` as an install request and calls `queueInstall`; it does not treat the extra `launch` parameter as a direct installed-game launch. This caused the upload chooser even when a Windows game was already installed.

Official source inspected:
- https://github.com/itchio/itch/blob/v26.1.9/src/main/reactors/url.ts
- https://github.com/itchio/itch/blob/v26.1.9/src/common/util/navigation.ts
- https://itch.io/docs/itch/integrating/manifest-actions.html

Astera now reads the bounded local install receipt, verifies the game ID, and uses its Windows launch target. Older receipts fall back to their installed file list. Only existing validated Windows executables contained in the install folder are candidates; helper programs, path traversal, streams and reparse targets are excluded. It does not recursively pick files from stale version folders or unrelated installations. A unique target launches directly with the executable's directory as its working directory. Multiple targets require a one-time choice saved as a relative path; the chosen target is revalidated against the current receipt on every launch.

If a game has `.itch.toml` actions or no supported native target, Astera opens the existing itch game page (`itch://games/<id>`) so itch can handle its custom arguments, prerequisites or HTML launch. These fallback cases may require Play in itch. Direct native launches do not inherit itch-specific sandbox/API/session settings. No itch database/account credentials are read or modified at runtime.

The reported local installation was checked read-only: its receipt selects the root Windows game executable and excludes UnityCrashHandler64 and an older nested copy. An exact executable/working-directory launch plan was generated; no game was started automatically and no live preferences were edited. Automated fixtures cover older receipts, multiple candidates, saved selection, identity/path validation and the fallback route.

Playtime and Last played now sit beside the hero's Play controls at normal and compact sizes. Last played records a confirmed observed session, not merely a button click or a failed start. Existing totals remain compatible; older sessions have no invented historical timestamp. These remain Astera-local estimates rather than imported Steam statistics.
