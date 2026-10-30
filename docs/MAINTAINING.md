# Maintaining Astera

This guide is for a person taking over the project without the original chat or
an AI service. Read ARCHITECTURE.md for the feature map. There is no generated
source that needs a conversation to recreate it.

## Build it locally

Requirements: Windows x64, .NET Framework 4.8 and PowerShell 7. The C# compiler
comes from Windows' Framework64 directory. Optional formatting tools require the
.NET 10 SDK. Runtime code deliberately stays compatible with the Framework C#
compiler; the formatting tool is a separate modern .NET application.

From the repository root, choose writable local work folders. Avoid staging
installations in Documents/Desktop when OneDrive manages those locations.
On Star's development PC, use D:/vale/StarsAllies-Storage for generated builds and
audits; it is not a required path on anybody else's PC.

```powershell
$work = Join-Path $env:LOCALAPPDATA 'Astera-Development'
$build = Join-Path $work 'build'
$audit = Join-Path $work ('audit-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
pwsh -File scripts/Build-Astera.ps1 -OutputDirectory $build
& (Join-Path $build 'Astera.exe')
```

Fully exit an already running Astera through its tray before trying a different
build. Otherwise single-instance activation returns to the existing process.
Normal app launch uses your local settings; automated audits use isolated state.

`Astera.csproj` is an IDE entry point whose Build target runs the same script.
`dotnet msbuild Astera.csproj -t:Build -p:OutputPath=<folder>` is also supported.
Set `PowerShellExecutable` if `pwsh` is not on PATH. The script embeds resources,
verifies pinned dependencies, generates version/helper identities and builds the
separate updater. Do not maintain another independent list of embedded resources.

## Make a small change

1. Find the owner in ARCHITECTURE.md. Read the associated audit before editing.
2. Keep file ownership, profile identity and persistence formats stable.
3. Build into a development output directory, never over the running app.
4. Run checks for the affected feature and nearby behavior. Review the diff.
5. Only run the whole suite when preparing a release or changing shared systems.

The source is split into readable files, with four-space indentation and ordinary
braces. `LibraryForm` is partial: each file owns one part of the same window. Its
fields and methods are intentionally shared; don't create a second Form instance
to reach a feature. A large rewrite of that shared state is not required for a
small change. Separate behavior changes from large formatting diffs in commits.

```powershell
pwsh -File scripts/Test-Astera.ps1 -ListChecks
pwsh -File scripts/Test-Astera.ps1 -UseExistingBuild -BuildDirectory $build `
  -TestRoot $audit -Only 'runtime-health,minecraft-launch'
```

`-Only` accepts comma-separated names. Publisher checks also need the synthetic
network fixture; the runner adds that dependency. Each audit directory must be
new so older failure evidence is preserved. UI audits run serially. Do not run
another UI test suite at the same time or use the desktop during native captures.
The receipt explicitly says `focused` or `full`. Focused receipts cannot publish. Use
`-ContinueOnFailure` during a broad audit to collect all failures in one pass;
the receipt remains failed and cannot publish. Capture failures are recorded, not
converted into passes.

For a release, omit `-Only`, then run `Test-AsteraSelfUpdate.ps1` against the exact
same executable. Full publisher/signature integration and self-update tests need
the publisher's separately stored encrypted signing key. Most feature work and
focused tests do not. Never ask another maintainer to put that key in Git or send
it in a bug report. A new publisher should provision their own identity through
a deliberate key/channel migration, not bypass signature checks.

## Keep the source readable

`.editorconfig` describes indentation; `.gitattributes` makes line endings stable.
Use the SDK-bundled Roslyn formatter, which refuses changes to syntax tokens or
node structure. It touches only our C# source, not vendor files or game assets.

```powershell
dotnet run --project tools/SourceFormatter -- --check .
dotnet run --project tools/SourceFormatter -- --write .
```

Add descriptive names and short comments about reasons, ownership or thread
boundaries. Avoid comments that just narrate the next statement. Persisted
numeric game IDs and string keys must remain stable across upgrades.

## UI and performance rules

- Native controls and their fields belong to the UI thread. Do filesystem,
  process enumeration and network work in the existing background tasks.
- An animation timer is not a work timer. Don't do OS scans on every paint.
- Do not rebuild a page when a background poll reports the same value.
- Pause hidden animation work; an opaque sibling cover does not change Visible.
- Dispose owned images, streams, timers and whole ProcessSnapshot instances.
  Disposing a Process wrapper does not stop the game it describes.
- Do not cache installation-safety checks. Those must see current files/processes.
- Treat a detected process, visible game window and loaded world as different facts.

`--runtime-health-audit <new-folder>` checks owned process lifetimes and hidden
animation pause/resume. `--performance-check <new-folder> <pose>` measures painted
banner frames with an existing SkinRig pose plus its .skin.png/.model companions.
It reports UI paint rate, render cost, working set and CPU, not hardware presentation
FPS. Don't promise a universal frame rate from one machine's measurement.

## Content and releases

Changing text/art/catalog entries is different from replacing the app. Catalog and
content publication scripts use a local signing key, then upload and verify the
payload before conditionally promoting the signed feed. Never manually edit an
encoded signed channel or change a published release asset in place.

- Minecraft² pack: Build-MinecraftPackZip.ps1, then Publish-FullPackUpdate.ps1.
- Discover catalog: Publish-DiscoverCatalog.ps1 (inspect its parameters first).
- Launcher app: bump VERSION, update app-release-notes.md, build, full tests,
  updater tests, security scan, then Publish-AppUpdate.ps1 with matching receipts.
- Create installers only for deliberate releases, not every content update.

Run a script with `Get-Help <script> -Full` or read its parameter block before use.
Check both the package and its exact release notes before publishing. Player notes
explain player-visible changes; developer plumbing belongs here or in diagnostics.

## If something breaks

Read Astera's local logs and the audit's failure.txt/result.json first. Find the
last known-good source/release and compare only the relevant system. Do not
experiment on a friend's only save or a live Minecraft world. Preserve unique
failure evidence. Roll back managed files with the existing recovery path; do not
remove unknown user files to make a test pass. Never turn off antivirus protection
to make an installer or updater work.

