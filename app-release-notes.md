# Astera 0.1.39 — Gacha installation repair

- Shortened temporary installation paths for Gacha Nebula + to fix Windows path-length failures.
- Temporary-file cleanup now retries brief file locks and preserves the original error if cleanup is blocked.
- Added clearer guidance for permission and path-length errors.

If Windows reports that a game path is too long, choose a short local folder such as C:\Games.