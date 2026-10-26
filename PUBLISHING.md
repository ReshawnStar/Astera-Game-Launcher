# Publishing a Minecraft² update

Test the pack first, close Minecraft, and write player-facing `release-notes.md`.
Astera publishing does not rebuild an EXE. Application releases are separate.

## Local preparation

```powershell
.\scripts\Publish-MinecraftUpdate.ps1 -ReleaseName 'mc2-205' `
  -GitHubRepository 'OWNER/Astera-Game-Launcher' -Preview
```

Omit `-Preview` to prepare signed files under
`D:\vale\StarsAllies-Storage\releases\Astera\mc2-205`. Nothing is uploaded unless
`-Upload` is explicitly specified. `-ProfileDirectory` selects a custom CurseForge
location; otherwise a unique Minecraft Squared display name is discovered.

Only our mod IDs and `managed-files.json` configuration defaults are packaged.
Everything else is recorded as a CurseForge prerequisite, not redistributed.
Never add worlds, accounts or downloaded third-party binaries to those rules.
The publisher rejects likely credential fields and redirected input paths.
Explicitly shareable resource packs are supported through `ResourcePacks`, e.g.
`{"Path":"resourcepacks/Star-Custom.zip","RedistributionApproved":true}`. List
only files you own or have permission to distribute. Order entries from low to
high priority; the Stars compatibility builtin stays last. There is no automatic
bulk resource-pack upload.

## One-command publication after initial setup

Install/sign into GitHub CLI on the publishing machine. No publishing credentials
are stored in Astera or given to friends.
For a portable CLI, pass `-GitHubCliPath` or put its executable path in the ignored
`publish-settings.local.json` as `{"GitHubCliPath":"D:/path/to/gh.exe"}`. No PATH
or global Git credential-helper change is required. This repository's configured
destination is read from `channel-settings.json` when `-GitHubRepository` is omitted.

```powershell
.\scripts\Publish-MinecraftUpdate.ps1 -ReleaseName 'mc2-205' `
  -GitHubRepository 'OWNER/Astera-Game-Launcher' -Upload
```

This prepares and signs metadata, creates a content release with immutable
SHA-named `.bin` assets, then commits the signed notice to
`channels/minecraft-squared.json`. The channel commit includes the previous blob
SHA when replacing an existing notice. Failed/conflicting promotion leaves the
previous channel file intact. A successfully uploaded content release can remain
unpromoted if that final step fails; inspect before retrying with a new name.

The stable client address is:

```text
https://raw.githubusercontent.com/OWNER/Astera-Game-Launcher/main/channels/minecraft-squared.json
```

`-GitHubBranch` selects another channel branch. App and future Star Dash releases
do not change this address. The publisher does not use GitHub's “latest release”
shortcut, so unrelated releases cannot redirect Minecraft² to the wrong files.

After a successful upload, the local signed notice is retained as
`latest-minecraft.json`. Subsequent publications reuse unchanged asset URLs and
carry retirement history forward. For manual uploads or a new publishing machine,
provide the actual last published notice through `-PreviousFeed`; upload all new
assets before promoting its replacement. Keep old referenced assets available.

Release names are never reused. Downloaded bytes are checked against the signed
hashes before any installation. Changed local defaults are preserved and reported.

## Key and account recovery

The private publisher key defaults to
`D:\vale\StarsAllies-Storage\keys\Astera\publisher.key`. It is encrypted with
Windows DPAPI for the publishing Windows account. It does **not** belong in Git,
release assets, application resources or friend downloads. Copying that encrypted
file alone to a different Windows account is not a portable key backup. Arrange
an offline recovery/key-migration procedure before public distribution; this
version does not provide an export or key-rotation wizard.

Changing the public key requires a deliberate new application/trust release.
GitHub CLI authentication is separate from update-signing keys.

The first live publication is `mc2-205-astera-1`. The app then downloaded all
three signed payloads over public HTTPS and installed/restored them successfully
in a disposable profile. Future publications still require testing the pack first;
an updater smoke test does not certify Minecraft gameplay compatibility.

Run the mock publisher test with an isolated fixture profile, never the live pack:

```powershell
.\scripts\Test-Publisher.ps1 -FixtureProfile 'D:\path\to\synthetic-profile' `
  -TestDirectory 'D:\path\to\new-publisher-test'
```

It shadows GitHub CLI only inside the test process. No real GitHub calls occur.
