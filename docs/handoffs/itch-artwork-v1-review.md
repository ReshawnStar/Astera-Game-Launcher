# itch artwork library v1 — integration review

Input: Astera_Itch_Artwork_Library_v1.zip supplied by Star, 2026-10-27.

Adopted: bounded plain/compressed receipt reading, camel/snake cover aliases,
preferred still covers, validated img.itch.zone/img.itch.io hosts, static PNG
normalization, direct/shallow root discovery, corrupt receipt resilience and
selection cancellation. Existing IDs, launch handoffs and user artwork priority
remain authoritative. Cache budget is64 MiB per provider; images are on demand.

The optional RSS catalogue is not an installed-game database and was not built
or embedded. No title guessing, login, live itch database access, butlerd client,
silent install/update flow or extra catalogue UI was added. Existing streamed
limits and validated redirects remain; supplied code was adapted, not copied.

Checks include an actual two-frame GIF normalized to a static PNG, still-cover
preference, plain/direct receipt discovery, corrupt receipt skip and stale-result
rejection. Live cover rendering uses an isolated copy of the existing receipt.