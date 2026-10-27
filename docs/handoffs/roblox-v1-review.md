# Roblox support v1 review

Adapted Star's handoff into canonical Place-ID favorites, metadata and bounded
decoded artwork. HTTPS-only parsing rejects credentials, nonstandard ports,
fake hosts and unresolved share links. No credentials, client files or account
APIs are accessed. Native WinHTTP TLS1.2/1.3 is used because the legacy Framework
transport failed Roblox TLS negotiation; certificate validation remains enabled.
Public Crossroads metadata/artwork and native library display passed. Actual
joining is not tested; protocol generation is tested without launching Roblox.
Removal changes only Astera preferences.