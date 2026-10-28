# Astera — fake startup lines v1

230 original, short rotating startup messages for Astera; authored for Star, Vale, and the group.

They are **cosmetic flavor text**, not real diagnostics. Do **not** imply that a message like “Checking save files” means save files were checked. Put accurate background work and progress in a separate status label / progress report.

## What's inside

- `astera_startup_lines.json`: structured library with stable IDs, categories, and recommended category weights.
- `all_lines.txt`: plain UTF-8 text, one entry per line; easiest drop-in for an existing WinForms app.
- `boot.txt`, `cosmic.txt`, `games.txt`, `group.txt`, `rare.txt`: separate categories so Vale can turn group jokes off in public builds if desired.
- `lines.tsv`: spreadsheet-friendly review/edit file.
- `AsteraFlavorLines.cs`: standalone .NET Framework 4.8-compatible class that loads files, shuffles per category without repeating, and returns the next line. No external NuGet dependency.

## Suggested rotation

- Boot 38% · Games 23% · Group 18% · Cosmic 16% · Rare 5%.
- Display each for about 1.0–1.8 seconds; avoid rapid flashes. A static line is better than a flickering screen.
- Use a shuffle bag within each category so users do not see the same message multiple times in one launch.
- Stop changing lines once the page is ready to reveal. Loading animation should hide real resource loading, **not artificially delay it**.
- Provide an option to disable playful messages; keep real error reporting honest.
- Don't attach the fake messages to a percentage bar or list them as completed actions.
- For public releases, you can omit or filter the `group` category.

## WinForms example

```csharp
// During startup: construct once, then on a normal WinForms Timer tick:
var flavor = new AsteraFlavorLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "startup"));
// In an existing Timer.Tick handler:
startupFlavorLabel.Text = "> " + flavor.Next();
// Once real loading finishes:
startupTimer.Stop();
// Show the prepared page with the desired transition.
```

The included `AsteraFlavorLines` class uses the five category TXT files. Copy those files to an app-data or bundled-content folder you control; do not write generated files into a protected installation folder.

## Project notes

Includes direct references to Minecraft², Last Light, StarDash, FTL: Multiverse, Roblox, itch.io, Steam, Gacha Nova, and the group (Star, Kira, Carl, Atomic, Vale, Cipher). "Where is the knives" is excluded by request. Herobrine is intentionally included. Proofread or disable in-jokes for audiences outside the friend group.
