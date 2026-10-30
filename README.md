# Astera Game Launcher

**Your worlds. One place.** A Windows game library and update companion by Star.

[Download Astera](https://github.com/ReshawnStar/Astera-Game-Launcher/releases/latest)

Astera brings installed games, Minecraft profiles and selected modpacks into one
library. It supports Steam, itch.io, Roblox shortcuts, local executables and
Astera-managed projects. External platforms keep responsibility for their games
and accounts. Astera does not distribute Minecraft or read launcher login tokens.

Minecraft² installs include the pack's mods, configuration defaults, resource packs
and shaders. Updates preserve worlds and personal settings. Minecraft profiles can
use CurseForge or Prism; each selected profile keeps its own artwork.

## Working on the code

Start with [the maintainer guide](docs/MAINTAINING.md). It covers building, small
changes, focused tests, debugging, content updates and releases. The
[architecture map](docs/ARCHITECTURE.md) explains which files own each feature.

- `src/`: application and isolated audit code, formatted as normal C#.
- `updater/`: the separate self-update helper.
- `scripts/`: build, test and publication commands.
- `assets/`: supplied artwork and pinned dependency files with provenance.
- `content/` and `channels/`: descriptions, catalogs and signed update notices.
- `tools/SourceFormatter/`: repeatable syntax-preserving formatting tool.

Build from PowerShell 7 with `scripts/Build-Astera.ps1 -OutputDirectory <folder>`.
The project file delegates to this same build, so resources and the updater cannot
silently diverge between the command line and an IDE. No signing key is required
to build or run focused application checks.

The current public release is 0.1.40. Source 0.1.41 is a maintenance candidate;
changing `VERSION` does not publish anything. See [validation and limitations](docs/MAINTENANCE-0.1.41.md)
and [player-facing release notes](app-release-notes.md).
Older implementation and release details are preserved in
[the historical README](docs/history/README-through-0.1.40.md), not current setup instructions.

## Updates and diagnostics

Application updates, Minecraft² pack updates and Discover project releases are
separate channels. Managed downloads are checked against signed metadata and file
hashes, backed up and installed with rollback protection. A new modpack release
does not require a new launcher build or installer.

Astera writes local diagnostics under `%LocalAppData%/Astera/logs`. The app offers
Open logs and Collect game logs. Logs are not automatically uploaded. Keep private
paths, account information and save files out of public issue reports.

## Windows security reports

Defender has reported Contebrew and Bearfoos detections for earlier Astera builds,
including an update helper. Their original cause remains unresolved; clean local
scans are not proof that another computer cannot flag a build. Use official releases
and report the exact version and threat name. Do not disable antivirus protection
or add exclusions as an installation solution.

