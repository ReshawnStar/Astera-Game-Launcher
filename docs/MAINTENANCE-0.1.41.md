# Maintenance candidate 0.1.41

This is a source/readability and performance maintenance candidate. The public
application update channel remains 0.1.40. No app release was promoted for this pass.

## Readability and handoff

- Expanded the original 149 C# files with Roslyn; exact tokens and syntax-node
  structure were verified unchanged during formatting. Formatting is a separate
  commit from behavior changes. The current 158 files pass the style check.
- Separated startup, the main Form, banner rendering, native window access and
  launch policy into named owners. Built-in persisted game IDs retain their values.
- Added MAINTAINING.md, ARCHITECTURE.md and CONTRIBUTING.md. The old README is
  archived; current instructions distinguish public releases from candidates.
- IDE builds now call the authoritative build script. This was verified with
  dotnet msbuild, not just XML inspection. No new app runtime dependency was added.
- Added named focused tests and failure collection. Focused/incomplete receipts
  cannot pass the normal publication gate; a real focused receipt was rejected.

## Performance/resource changes

ProcessSnapshot owns all Process wrappers returned by an enumeration. Early
matches and exceptions now release the entire snapshot. This affects game checks
and installation safety guards; it does not stop games or cache safety decisions.

An opaque launch cover now pauses banner rendering and sidebar GIF timers.
Loading dots remain active. Returning to Library resumes the visible animation.
The isolated 350ms samples recorded 4 visible renders, 0 covered renders and 3
resumed renders. This is not a whole-machine CPU benchmark.

The 12-second animated-banner measurement averaged 62.50 painted frames/sec,
with a 57.09 minimum during resizing/maximizing. Mean render cost was 2.00ms,
p95 2.60ms; peak working set approximately205MiB and machine CPU approximately3.75%.
These measure UI paints/rendering on this PC, not DWM/hardware presentation. The
strict minimum60 FPS audit did not pass; do not label that threshold achieved.

## Validation and limits

Candidate SHA256:
39C85EB535058C54CB97D1A13B80E698A50B8B3C0CB87BAB489CAD37E5DE67E8

The consolidated full run completed with 300 passing grouped checks and one
failed provider: app-update-flow timed out. That provider's 9 checks passed in
isolated runs before and after the timeout. Thus all309 grouped checks have been
covered successfully across runs, but there is no wholly passing normal full-run
receipt for this candidate. Do not merge those facts into a fabricated full pass.

Earlier native capture attempts were obscured or could not map physical pixels;
the targeted visual run subsequently passed63 groups without app-code changes.
The transition check was also compared with public0.1.40 and passed independently.
The update-window timeout remains an intermittent validation issue, not a claimed
fixed bug. To debug, run the named app-update-flow check on an untouched desktop.
Before app publication, obtain a normal complete passing full-run receipt.

Ten dedicated updater checks passed on the exact candidate. Defender found no
threats; historical detections remain unresolved and scans do not guarantee future
classification. No antivirus settings, login files, player saves or public feeds
were changed. Developer builds do not need a private signing key; signing and
publisher integration remain owner-controlled.

Developer evidence locations (under the organized audit storage):

- astera-readability-2026-10-30: formatting/style proof and cleanup receipt.
- astera041-focused:16 groups, including runtime lifetime/hidden-animation checks.
- astera041-remaining:130 passing groups, with the initial transition failure retained.
- astera041-visual-checks:63 passing groups on the same candidate.
- astera041-consolidated: full-run result with the explicit update-flow timeout.
- astera041-update-window-recheck:9 passing groups after the timeout.
- astera041-updater:10 dedicated updater checks.
- astera041-performance: measured result, including the unsuccessful60 FPS floor.

Only two non-running intermediate builds were pruned after path/reparse checks.
Current/prior candidates, source, unique evidence and rollback records were retained.
