# Steam artwork library v1 — integration review

Input: Astera_Steam_Artwork_Library_v1.zip supplied by Star, 2026-10-27.

Adopted into the existing code: manifest App IDs remain authoritative; local
artwork/custom overrides stay first; missing icons use store portraits/header;
selected-game public requests cancel on switching; actual images are decoded,
resized and bounded before atomic cache writes. Public cache is capped at64 MiB.

Existing Astera transport retains streamed byte limits, per-redirect HTTPS host
validation, cancellation and two-request concurrency limits. The supplied
service reads whole response bodies before enforcing its byte limit and relies
on JPEG magic bytes rather than full decoding; it was not copied verbatim.

The optional SteamSpy catalogue/36-title seed are not runtime dependencies and
were not generated or embedded. Installed manifests already provide exact IDs;
no user API keys or bulk downloaded artwork are included. Steam install/update/
Play handoff remains unchanged. No TTL settings UI was added by this pass.