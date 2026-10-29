# Astera 0.1.39

Gacha Nebula + includes 215-character relative paths for bundled AIR extensions.
The former 52-character temporary folder name pushed C:\Games extraction paths
past legacy Windows MAX_PATH even when the final game folder would fit.

The temporary sibling now uses .an- plus sixteen random hexadecimal characters.
Cleanup validates that exact name pattern and parent, refuses links, retries brief
locks, logs deferred cleanup and never replaces an installation exception with
an unrelated cleanup exception. Access-denied and path-length errors have explicit
local/short-folder guidance. No administrator or security-setting change is used.

Actual published ZIP installation, all-entry C:\Games file/directory path budgets,
rollback/save preservation and deliberately locked AIR-license cleanup are covered
by the managed-game audit. All original game ZIP bytes remain unchanged.
