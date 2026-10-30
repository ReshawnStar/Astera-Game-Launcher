# Astera architecture map

## Start here

Program.cs selects an explicit audit/utility command or opens the normal app.
LibraryForm.cs creates the main window and shared state. LibraryLayout.cs builds
its controls. StartupScreen.cs prepares the first page behind the logo/dots cover.
TopNavigation.cs switches Library, Discover and Arcade; existing page-transition
files prepare a complete surface before revealing it.

This is a Windows Forms/.NET Framework application, not a web app. `LibraryForm`
and `Banner` are partial classes. Partial files share one object's lifetime; they
are an organizational boundary, not independent services.

## Where a change belongs

| Responsibility | Main owners |
| --- | --- |
| Entry point, utility/audit command dispatch | Program.cs |
| Window state, primary actions, shared fields | LibraryForm.cs |
| Layout, hero actions, page controls | LibraryLayout.cs, LayoutCards.cs |
| Borderless frame | WindowFrame.cs |
| Buttons, icons, colors | AsteraButton.cs, UiAssets.cs, ButtonAtlas.cs |
| Library search, grouping, favorites | LibraryCollections.cs, LibrarySearch.cs and related Library files |
| Library icon loading and GIF playback | LibraryIconLoading.cs, AnimatedLibraryIcons.cs |
| Banner rendering and player skin | Banner.cs, BannerPerformance.cs, SkinRig.cs, MinecraftSkin.cs |
| Discover cards/catalog and managed projects | DiscoverView.cs, DiscoverCatalog.cs, ManagedLibrary*.cs |
| Preferences and local persistence | Updates.cs (Preferences, Store, Channel) |
| Running-game detection and lifetime | RunningGames.cs, ProcessSnapshot.cs |
| Estimated playtime | Playtime.cs |
| Generic launch waiting | GameLaunchWaiting.cs, GameLaunchOverlay.cs, LaunchPolicy.cs |
| Minecraft window readiness | MinecraftWindows.cs, MinecraftLaunchProgress.cs |
| CurseForge dispatch and foreground policy | CurseForgeLaunch.cs, WindowsLaunchDesktop.cs, CurseForgeBackground.cs |
| Prism/profile setup | Prism*.cs, MinecraftProfiles*.cs, CurseForgeSetup.cs |
| Steam/itch metadata, artwork and launch | PlatformLibraries.cs, Steam*.cs, Itch*.cs |
| Roblox experience metadata and handoff | Roblox*.cs |
| Thunderstore discovery, import and managed copy | Thunderstore*.cs |
| FTL/Hyperspace/Multiverse installation | Multiverse*.cs, Ftl*.cs |
| Managed update planning and file transactions | MinecraftEngine.cs, FullPack*.cs, ManagedGames*.cs |
| Application self-update | SelfUpdate*.cs, AppUpdate*.cs, updater/UpdaterEntry.cs |
| Diagnostics and report collection | Diagnostics.cs and related diagnostic files |
| Isolated regression checks | *Audit.cs, *SelfTest.cs, scripts/Test-*.ps1 |

Names ending in `Ui` coordinate controls; engines/importers handle files and plans.
Do not mix player-facing descriptions with operational logs. Update activity is
scoped to the game that owns the operation.

## Launch flow

1. Play validates the selected entry/profile and blocks duplicate requests.
2. The owning platform or supported launcher receives the launch request.
3. Launch waiting is recorded independently from optional playtime tracking.
4. A full waiting cover shows status and optional flavor text. Banner and sidebar
   animations underneath are paused; the cover's dots still run.
5. Process/window probes run off-thread. Minecraft additionally needs a new visible
   game window and the selected profile's running log. Pre-existing game windows
   cannot satisfy that new request.
6. Completion removes the cover. Back to library only dismisses presentation; it
   does not terminate the game. Timeout asks for attention instead of claiming failure.

LaunchPolicy.cs owns the timeout/probe intervals. Rendering clocks remain separate.
CurseForgeBackground corrects at most one automatic manager activation and does not
hide sign-in prompts, close the manager or change Windows protocol defaults.

## Update flow and trust boundary

Read a signed notice -> verify signature/expiry/identity -> download -> verify
hash/size -> preview against current files -> stage -> validate -> back up managed
files -> commit -> verify -> record ownership. Failure uses the existing rollback.
Unknown user files and saves are outside managed ownership.

The self-updater is a separate embedded helper, built before the main EXE. It waits
for the running app, verifies the candidate, replaces it and checks restart health.
Do not add shell commands or bypass hash/signature checks to work around errors.

Preferences are local JSON under LocalAppData/Astera. Channel/project/profile IDs
are persisted contracts. Built-in IDs are Minecraft=0, Star Dash=1, Multiverse=2;
activity keys are builtin:0/1/2. A name cleanup must not renumber them.

## Build and test boundary

Build-Astera.ps1 is authoritative for compiled sources, embedded assets, pinned
vendor hashes and helper identity. Astera.csproj delegates to it. The formatter is
a separate SDK tool and is never shipped inside Astera. No new runtime dependency
was introduced for this readability pass.

Test-Astera.ps1 runs named providers serially. Focused checks identify themselves
in the receipt; only a full matching receipt plus updater checks can authorize a
normal app publication. ReleaseTestGate.ps1 enforces that distinction.

