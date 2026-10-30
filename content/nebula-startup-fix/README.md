# Gacha Nebula + startup repair

This is the C# save-protection helper shipped with the 2026.10.29 game update.
It preserves and permits the loading screen's exact startup-only AMF3 profile before the first gameplay save. Import and restore still require a full save.

Compile NebulaSafe.cs as a .NET Framework 4 Windows application with System.Windows.Forms, System.Drawing and System.Core references. The outer launcher lives alongside game/. No gameplay SWF or AIR identity changes are included.

--self-test <new-folder> runs the save-vault checks without launching the game.
