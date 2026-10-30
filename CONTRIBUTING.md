# Contributing to Astera

Start with [Maintaining Astera](docs/MAINTAINING.md), then the
[architecture map](docs/ARCHITECTURE.md).

Keep formatting-only commits separate from behavior changes. Use descriptive
names and explain ownership/threading decisions where they are not obvious.
Run focused checks for small changes; run the complete suite for broad changes
and releases. Do not weaken checks or copy old receipts to match a new build.

Preserve saves, personal configuration and rollback data. Use synthetic fixtures,
not live game folders. Never commit signing keys, login tokens, personal logs or
save files. Do not modify pinned vendor binaries without recording provenance.

The build script is the single source of truth for packaged resources. Player
patch notes describe what changed for players; implementation details belong in
developer documentation and diagnostic logs.
