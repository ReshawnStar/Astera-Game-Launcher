# Update trust and scope

Astera verifies RSA-SHA256 signed metadata with an embedded public key before
interpreting release paths or download instructions. Payloads use SHA-256 and
signed byte sizes. Production network requests require HTTPS through every
redirect and reject embedded credentials, oversized responses and expired notices.
The localhost HTTP transport exists only in in-process test construction; there
is no app setting that enables production HTTP.

The signature authenticates the update notice. This is separate from Windows
Authenticode: the current development EXE is unsigned, and a valid notice does
not certify that its game code has no bugs. Get the first app from Star's trusted
distribution channel. Checksums alone do not authenticate an unsigned first app.

Allowed profile roots are limited to managed mods, archive assets and configuration
defaults. Traversal, reserved filenames, duplicate archive entries and redirected
targets are refused. The Minecraft channel additionally refuses third-party mod
replacement. The separate Astera ledger prevents ownership collisions with the
older full-pack installer. Local settings/worlds are excluded, not backed up and
then overwritten.

An update stages and checks replacements and backups before applying individual
atomic file replacements. Caught failures roll back; unfinished journal entries
block a new install until restored. A stale preview or later user edit invalidates
the operation instead of being silently overwritten. Power-loss recovery still
requires the user to select the unfinished backup in Restore backup.
An exclusive profile lock prevents two simultaneous Astera installs/restores.
Tested CurseForge dependencies are also checked for changes between preview and
application, rather than assuming the launcher remained idle during a download.

No game/app is force-closed to install an update. The current Java guard is
conservative and may also require closing unrelated Java tools. Installation and
restore cannot be cancelled midway; downloads/checks can be cancelled.

Known v0.1 limits: no authenticated private feeds, no app self-update, no StarDash
update adapter yet (local Godot launch is linked), no automatic CurseForge profile registration, no binary delta patching,
no resumable partial transfers, no signing-key export/rotation wizard, and no
automatic cache-pruning UI. Cached files can be removed while Astera is closed;
profile rollback folders are separate and should be retained deliberately.

No network service, telemetry, account login or remote command execution is
installed. While running/in the tray, Astera contacts its configured feed and
the signed payload addresses only. Public hosting is pending setup.
