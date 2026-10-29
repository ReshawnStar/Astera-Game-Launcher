# Editing Astera Discover

Edit `content/discover-catalog.json`. This is showcase metadata, not an installer manifest.

Each project has a stable lowercase ID, title, category, summary, description,
requirements, notes, availability (`available` or `coming-soon`), and artwork.
Featured projects rotate on the banner. `Screenshots` is an optional list of images.
Don't publish placeholder screenshots or invented patch notes.

Existing Library routes are fixed: minecraft-squared = 0, star-dash = 1,
ftl-multiverse = 2. Other projects use LibraryKind -1, with Coming soon or an
HTTPS OfficialUrl. This cannot run commands or silently download/install a game.
Enabling a new managed-game installer requires a reviewed client integration.

For artwork use one of the existing embedded Resource names, or an HTTPS Url
and exact SHA-256 Hash. Don't supply both. Remote images are verified and cached,
limited to 8 MiB/12 megapixels/4096 pixels per axis; at most 32 distinct remote
images per catalog. Display copies are reduced to at most 1200x700.

Prepare a signed catalog locally with scripts/Prepare-DiscoverCatalog.ps1,
passing the tested Astera EXE and a new OutputFile. It validates, signs, and
verifies against the app's publisher key. Review before publishing the resulting
envelope as channels/discover.json on main. No private key goes into GitHub.
Preparing a catalog does not upload anything.

Astera checks the catalog on first Discover visit. Offline it uses a verified
cached catalog or the bundled one. Downloads still use the project's existing
verified install workflow and normal review steps; catalog browsing installs nothing.
Gacha Nebula + uses the reviewed managed ZIP provider. Its Package metadata pins Version, Bytes, ZIP Hash, LauncherHash and GameHash. An empty Url offers a user-selected verified ZIP; a signed HTTPS Url enables the normal download path. Play invokes the protective helper with --play. Existing Recovery and personal exports are preserved; same-version altered packages and downgrades are refused. New managed installers require reviewed client code.

Artwork positioning: CatalogImage supports optional Fit ('cover' or 'contain') and X/Y alignment in the range 0..1. A project's optional HeroArtwork overrides card art in featured/detail banners. Missing settings keep existing catalogs compatible; Gacha and Last Light default to showing the full artwork. The supplied Last Light logo is a local fallback for its currently empty live artwork entry.
