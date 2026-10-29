# Astera Game Launcher

**Your worlds. One place.** A small Windows game library and update companion by Star.

Development releases are available from https://github.com/ReshawnStar/Astera-Game-Launcher/releases/latest.
The Minecraft² content channel is live; content releases are separate from the app.

Minecraft² stays in CurseForge. Astera finds its profile, checks Star's signed
update channel, downloads changed managed files and installs a previewed update
with rollback protection. It does not download Minecraft or replace its launcher.

## Reported Windows Defender detection

One tester reported Defender removing Astera 0.1.19 as `Program:Win32/Contebrew.A!ml`,
which appeared in Brave as “Virus detected.” We believe this is an incorrect
detection; Microsoft has not confirmed that assessment, and no review result is
available yet. Download only from this repository's official releases. Do not
disable antivirus protection for Astera. Detection reports should include the
Astera version and exact threat name, without private account or folder details.

## Current build

Current0.1.30 adds a six-dot loading indicator and default-on The Renegade Collection support for FTL: Multiverse. Disable TRC in FTL settings if you prefer the base ship list. Add-on choices are saved for future updates; unknown external modifications are refused. See [TRC details](docs/TRC-provenance.md).

Astera supports animated GIF backgrounds/character layers/library icons,
including animated itch covers. YouTube links in update notes open an in-app
player on click (Microsoft Edge WebView2 Runtime required; no autoplay).
Add game → Roblox experience adds a Place-ID shortcut with public artwork;
Roblox manages its client and experience updates. Actual joining still depends
on the installed Roblox client and the player's access to the experience.

- Steam-inspired library: games on the left, Play and update status on the right.
- v0.1.3 layout: large hero with a separate transparent character layer that can
  extend above the background; Overview, Updates, Backgrounds and Settings pages;
  update notes/activity and installation cards. Backgrounds and character PNGs
  are selected per game and remembered locally. Buttons use drawn placeholders
  until Star supplies final artwork; no crops from the concept image are used.
- v0.1.4 repairs overflow-menu disposal. Astera writes versioned session/error logs
  under `%LocalAppData%/Astera/logs`. Updates → Open logs opens that folder.
  While open, Astera checks the linked Minecraft profile every minute for recent
  crash reports and JVM fatal-error logs, copying new content only. Collect game
  logs also snapshots Minecraft's latest.log or StarDash's Godot logs. Originals
  stay untouched; copies are hash-deduplicated with source receipts. Large files
  retain their last 4 MiB, marked `-tail`. Logs stay local and are never uploaded.
- v0.1.5 adds a soft travelling border glow to Play. It loops in four seconds,
  leaves the caption clear, and pauses when disabled, hidden or minimized.
- v0.1.6 embeds Star's original button and grayscale sheets. Rounded corners and
  glow padding survive resizing; Settings → Button color selects the original
  Violet/blue or mask-derived Teal, Rose and Gold. Secondary controls use quieter
  shades. Source PNGs remain unchanged; labels and the moving glow stay live.
- v0.1.7 uses the revised flat button sheets and supplied 21-icon set, applies
  the typography guide with installed-font fallbacks, and preserves the full
  design handoff in `assets/ui-v1`. Icons scale by visible bounds, keeping their
  aspect ratios. Both default and compact layouts are visually checked.
- v0.1.8 fixes live alpha repaint trails and compact hero-panel overlap, adds a
  borderless draggable/resizable frame with window controls, and replaces the
  notes/activity scrollbars with button-textured thumbs on dark tracks. The four
  Cipher v1.2 icon fixes are included. Empty character placeholders are hidden.
  The overflow menu's Capture UI snapshot saves visible client pixels plus
  control bounds locally. `--visual-check <new-folder>` opens an isolated test
  window, cycles tabs/compact/maximized/restored states, saves actual displayed
  pixels, and closes. It requires an unobscured window; it performs no updates.
- v0.1.9 embeds the supplied Minecraft² sunrise as its default background and
  adds a waving Minecraft-skin preview. When no custom character image is set,
  Astera reads the active public profile ID from the official Minecraft Launcher's
  local account file and retrieves public skin data from Mojang's HTTPS services.
  Authentication tokens are not used, transmitted or persisted by this feature.
  Posed images stay in the local Astera skin cache and refresh after a day.
  Backgrounds → Use my Minecraft Launcher skin disables/enables it. Custom character
  art takes priority. Missing accounts, unavailable skin services or disabled
  previews leave the character layer hidden. No Minecraft skin is bundled.
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

## Astera app releases (0.1.11 onward)

Add game selects a local Windows EXE. Custom entries are sorted A–Z alongside
the managed games and run from their own EXE folder. Game settings can rename,
relink or remove an entry; removal never deletes the game. Custom games do not
inherit Minecraft or FTL update actions. Their artwork is saved independently.

The small icon beside the sidebar version checks the signed Astera app channel.
Startup/30-minute checks follow the existing automatic-check setting. Updating
requires a download and restart confirmation. A separate helper waits for Astera
to close, verifies the announced EXE and keeps one previous executable. It rolls
back automatically if the new app fails its startup health check. This is a
startup check, not a guarantee against later crashes or sudden power loss.
Game saves and preferences are outside the replaced EXE. Older app versions
without this feature need the 0.1.11 EXE once before future app updates work.

For a release, change VERSION, place Cipher's player-facing notes in
app-release-notes.md, and run these from PowerShell 7 on the signing PC:

```powershell
./scripts/Test-Astera.ps1 -TestRoot 'D:/vale/StarsAllies-Storage/audits/my-release'
./scripts/Test-AsteraSelfUpdate.ps1 -AuditRoot 'D:/vale/StarsAllies-Storage/audits/my-release-self-update'
./scripts/Publish-AppUpdate.ps1 -TestReceipt 'D:/vale/StarsAllies-Storage/audits/my-release/result.json' -SelfUpdateReceipt 'D:/vale/StarsAllies-Storage/audits/my-release-self-update/result.json' -Publish
```

The publisher refuses receipts for different EXE bytes, verifies its signing key,
uploads the immutable release, downloads it back and checks its SHA-256 before
conditionally promoting channels/astera.json. Without -Publish it only prepares
an isolated signed release folder. A failed upload never promotes the channel;
a channel conflict requires inspection before retrying. The private signing key
stays DPAPI-encrypted outside Git. Minecraft content publishing remains separate;
routine content updates do not create a new app installer or EXE.

Astera 0.1.12 removes hover/select playback and sound settings. Original audio assets remain archived for possible future use. Game descriptions and patch notes
remain separate from installation/update diagnostics.

## Steam artwork and launch handoff (0.1.13)

Added Steam games are identified from the selected EXE's actual steamapps/common
folder and its matching appmanifest, including nested EXEs and secondary drives.
Artwork is read locally from that user's Steam installation cache (modern per-app
folders and legacy flat filenames). It never downloads artwork or copies personal
Steam account data. Custom Astera backgrounds/characters take priority; Reset
artwork restores the detected banner. Missing cached images use the default scene.

Steam games use Steam's launch protocol, so Steam handles updates, launch options
and game startup. A pending update reported by the local manifest changes Play to
UPDATE & PLAY: Steam can update then launch. OPEN IN STEAM opens that game's page.
This is a Steam handoff, not a guarantee of unattended downloads or a separate
update-only API. Steam may require login or display its normal prompts. Astera
does not edit Steam manifests or schedule automatic game downloads. Selected game
metadata refreshes in the background every 15 seconds while the launcher is open.

Custom games support Backgrounds → Choose icon for transparent PNG library
icons. Images preserve alpha, fit inside a padded square and override Steam's
cached game icon. Reset artwork restores automatic artwork. Steam JPGs retain
the pixels supplied by Steam; Astera does not remove their baked-in backgrounds.

### Installed library picker and Steam news

Add game offers Steam library, itch.io games and a local EXE. Steam discovers
registered libraries from libraryfolders.vdf and installed app manifests; entries
launch by app ID and do not guess a game's EXE. itch.io reads public game metadata
from .itch/receipt.json.gz inside its apps folder. Its default is per-user AppData;
Choose library folder supports another install location and remembers that folder.
This picker does not read the itch account database or credentials. itch launch
uses its official protocol, which may ask the user to confirm startup.

Steam UPDATE opens the selected game's Steam library page and never requests
launch. Steam's public protocol has no verified universal update-only command;
the user may need to press Update/Resume in Steam. PLAY uses the normal Steam
launch route after the pending update clears. The earlier Update & Play behavior
is superseded. Official developer posts are fetched asynchronously from Steam's
public GetNewsForApp API, with dates, plain text formatting and a local offline
cache. These posts may include announcements as well as patch notes.

## Settings and diagnostics in 0.1.14

The header Settings button contains launcher preferences. Each game's Settings tab contains only that game's location, setup and artwork actions. Button colors live under global Appearance: the four legacy colors plus twelve requested palette options. Decorative animations can be disabled. About and the overflow menu expose the embedded Astera changelog.

Current global controls cover update checks, tray behavior, library locations, effects, colors and diagnostics. Windows startup registration and automatic managed downloads remain future features; no inactive placeholders are shown. Managed-file backups stay enabled. A library migration/export feature and a unified update center are not implemented.

Running-state detection guards custom Windows games, platform imports, StarDash and FTL, using process paths and Steam's local running flag. Minecraft stays a CurseForge launch handoff. A per-settings-root single-instance guard brings an existing Astera window forward. FTL report collection preserves original files and deduplicates local copies; it does not upload reports. Consecutive duplicate activity messages are hidden in the UI while individual entries remain in the session log.

## Full Minecraft² pack ZIP channel (0.1.15)

The production Minecraft channel is now `channels/minecraft-pack.json`. Its signed
schema-2 notice identifies one complete ZIP, exact SHA256/size, expiry and the
entire release manifest. Astera downloads/verifies that ZIP and uses the same
preview, backup, guarded install and restore engine as component updates. This
channel can own all declared pack mods; schema-1 component notices remain limited
to Stars components. Existing default feed addresses migrate; explicit custom
channels stay unchanged. Older Astera builds retain their component-only channel.

Create a compatible Minecraft/Fabric profile in CurseForge once and link it in
Astera. There is no CurseForge project publication requirement, no launcher
replacement and no automatic CurseForge profile registration.

Release routine (ZIPs are produced only for deliberate pack releases):

1. Test the source profile and close Minecraft.
2. Run `scripts/Build-MinecraftPackZip.ps1 -ReleaseName <fresh-name> -IncludeShaders`.
   The first release can import retirement history with `-PreviousReleaseZip`.
3. Test the actual ZIP using `--audit-full-pack-release <zip> <new-test-directory>`.
4. Run `scripts/Publish-FullPackUpdate.ps1 -Zip <zip> -NotesFile <player-notes> -TestReceipt <audit-result.json> -Publish`.

The publisher signs locally, uploads the ZIP to GitHub Releases, downloads it back
to verify its hash, then conditionally promotes the signed channel. Friends use
Astera's existing Check/Update buttons; they never need to extract a ZIP.
`full-pack-rules.json` controls approved configuration defaults and pack order.
Active root JARs, resource ZIPs and requested shader ZIPs are included. Backup
folders, disabled files, worlds, accounts, personal options, maps and caches are
excluded. Existing personal configuration and shader selections are preserved.

A changed release downloads the complete ZIP. Matching installed files are not
rewritten; a matching cached ZIP is reused. This is not a delta download system.
For a single release command, use `scripts/Publish-MinecraftPackRelease.ps1 -ReleaseName <fresh-name> -NotesFile <notes.md> -Publish`. It includes shaders, builds the ZIP, audits an isolated install, then signs/uploads/promotes it. Use `-Preview` for inventory only. No launcher EXE is rebuilt.

First-time CurseForge import: `scripts/Build-CurseForgeSetup.ps1 -PackZip <tested-pack.zip>` creates a tiny, empty profile ZIP with the matching Minecraft/Fabric versions. Import it in CurseForge, then link the created profile in Astera and Update. This does not import/register a profile automatically. The setup ZIP contains no mods or personal data.

## Thunderstore profiles (0.1.36)

Add a supported installed Steam game with **Add game → Thunderstore profiles**, or
select an existing Steam entry. Astera discovers the game's profiles in the
standard Thunderstore Mod Manager and r2modman data folders. A custom data folder
can be selected in the profile picker. Profiles remain grouped inside one game.

Choose Vanilla or a named profile, then Play. Direct modded launch and share-code
installation currently support Mono BepInEx profiles such as Lethal Company.
Unsupported loaders do not silently fall back to vanilla. The selected game must
be stopped before switching or replacing its profile.

**Update by code** checks the exported mod versions, dependencies and Thunderstore
community before review. Astera builds an independent managed copy, retains the
previous owned copy as a backup and preserves the manager's original. Personal
configuration absent from the new pack survives; supplied pack configuration takes
precedence. **Use original** switches back to the manager copy. Version-specific
archives are hash-checked and reused; metadata requests are cached and paced, with
bounded retries respecting the server's retry delay.

The native game launch uses Steam and the selected profile's absolute preloader
path. Only known Doorstop root files are linked, with differing originals backed
up. Game executables and saves are not replaced by profile updates. Custom private
performance add-on channels are a future extension and are not part of this release.

Profile import behavior follows the upstream format documented at
https://github.com/ebkr/r2modmanPlus/wiki/Profiles and its implementation. YamlDotNet
18.1.0 is pinned from its official NuGet package; its MIT notice and provenance are
under assets/vendor/yamldotnet, and the notice is embedded with the dependency.
