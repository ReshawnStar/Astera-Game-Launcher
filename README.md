# Astera Game Launcher

**Your worlds. One place.** A small Windows game library and update companion by Star.

Development build: the first packaged Astera app download is still being prepared.
The Minecraft² content channel is live; content releases are separate from the app.

Minecraft² stays in CurseForge. Astera finds its profile, checks Star's signed
update channel, downloads changed managed files and installs a previewed update
with rollback protection. It does not download Minecraft or replace its launcher.

## Current build

- Steam-inspired library: games on the left, Play and update status on the right.
- v0.1.3 layout: large hero with a separate transparent character layer that can
  extend above the background; Overview, Updates, Backgrounds and Settings pages;
  update notes/activity and installation cards. Backgrounds and character PNGs
  are selected per game and remembered locally. Buttons use drawn placeholders
  until Star supplies final artwork; no crops from the concept image are used.
- Minecraft Squared profiles discovered by their CurseForge display name, even
  after folder renames. `But Again` is recognised as a **legacy folder name**.
  Multiple matching profiles require a choice; Astera never guesses between them.
- Play opens a standalone CurseForge application or its installed Start-menu
  shortcut (including Overwolf installations) when found. Other installations
  receive launch instructions. New profiles are created
  or imported in CurseForge, then linked in Settings; empty directories are not
  presented as registered launcher profiles.
- Signed update checks on startup and every 30 minutes while open/in the tray.
  The app can be closed completely through Exit; it installs no Windows service
  or login-startup task. Downloads and installation are explicit player actions.
- SHA-256 verification, HTTPS, local reuse and download caching. A repeat update
  downloads no unchanged payloads. This is file-level updating, not binary patches.
- Preview, Minecraft/Java process guard, backups, interrupted-install rollback,
  stale-preview protection and downgrade rejection.
- Worlds, maps, screenshots, accounts, launcher metadata and personal settings
  are protected. Changed local configuration defaults are retained.
- StarDash links to the existing **Godot desktop app**, discovered under Documents
  or chosen in Settings. Play validates the exported `StarDash.exe`/`StarDash.pck`
  pair and opens it directly. Its packaged update channel is not published yet;
  no StarDash, SRB2, saved settings or custom music files are updated by this build.
- v0.1.1 fixes release-note focus/selection during update checks and preserves
  paragraph breaks from signed notes that use Unix line endings.

The public channel address is configured in `channel-settings.json`. The app
verifies notices with the included public key even when the address is changed
in Settings. First hosted delivery passed on October 26, 2026: three actual
HTTPS payload downloads, protected-file checks, install, no-op repeat and restore
in a disposable profile. No mod code was launched and no live pack was changed.

## Minecraft² ownership

Astera's channel manages `stars_allies`, `stars_general_fixes`, optional standalone
`waystone_map_travel`, and explicitly listed configuration defaults. Third-party
mod binaries stay with CurseForge. The signed release lists the tested external
mod versions; players receive a prerequisite report if those differ.

Resource-pack archives are never automatically swept from the pack folder.
Shareable packs can be listed explicitly in `managed-files.json` with
`RedistributionApproved: true`; their updates then use the same verified download
and rollback path. The default list is empty. Stars compatibility is enabled and
other existing supplied packs stay in the profile.

Astera uses `Astera-minecraft-state.json` and `Astera-backups` inside the linked
profile, separate from the older full pack installer's ownership ledger. This
prevents an overlay update from retiring the installer's third-party pack files.

## Build and test

Players need Windows x64 and .NET Framework 4.8; they do not need PowerShell,
GitHub accounts, a developer SDK or signing keys. Maintainer scripts use
PowerShell 7.2+ and the Windows .NET Framework compiler.

```powershell
# Once on the publishing machine. The private key stays outside this repository.
.\scripts\Initialize-AsteraSigning.ps1

# Build only when releasing Astera itself, not for every Minecraft² update.
.\scripts\Build-Astera.ps1 -FeedUrl 'https://raw.githubusercontent.com/OWNER/REPO/main/channels/minecraft-squared.json'

# Use new, isolated directories for each test.
& 'D:\vale\StarsAllies-Storage\builds\Astera\0.1.1\Astera.exe' --self-test 'D:\vale\StarsAllies-Storage\audits\astera-network-new'
& 'D:\vale\StarsAllies-Storage\builds\Astera\0.1.1\Astera.exe' --engine-self-test 'D:\vale\StarsAllies-Storage\audits\astera-engine-new'
```

`channel.public.xml` is intentionally public. It is already initialised for the
first build; do not rerun key initialisation or replace that key casually.
`--data-dir <directory>` provides isolated app settings/cache for development.
It is not required for normal use. Build outputs and tests default to the
maintainer's organised D: storage; override script parameters on other machines.

See [PUBLISHING.md](PUBLISHING.md) for the release workflow and
[SECURITY.md](SECURITY.md) for the trust model and current limits.

For a complete local developer pass, run `scripts/Test-Astera.ps1`. It builds
the app, runs download/installation fixtures and mocks GitHub publication. Results
are saved under the new D: audit directory; no live game or real GitHub request
is involved. Actual hosted delivery is checked separately rather than inferred
from those local fixtures.
`scripts/Test-HostedChannel.ps1` performs that separate smoke test using a new
synthetic profile and the real signed HTTPS channel; it never launches mod code.

No source licence has been selected yet. Publishing this repository does not
change third-party games' or mods' licences.
