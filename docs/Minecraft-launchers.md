# Minecraft profiles and launch feedback

The local 0.1.40 candidate uses one Minecraft library entry. Its selector discovers
CurseForge and Prism instances; Minecraft² and other profiles retain separate
artwork. The supplied vanilla defaults are used when no profile artwork is set.
Minecraft² updates are restricted to Minecraft² profiles.

Prism starts the selected instance with its supported --dir / --launch command.
CurseForge on Overwolf receives its own generated launch-game URI directly when
installed. This avoids Prism owning the shared curseforge URL scheme, without
changing Windows associations. Launcher account files are never copied or read.

A full-window logo and six-dot waiting screen explains preparation and offers Open launcher if CurseForge needs
attention. The manager is requested in the background. A one-correction focus guard
only minimizes an automatically activated main CurseForge window from the known
host directory; input in another app or explicitly opening the manager takes
priority. Login prompts and other applications are not hidden. External launcher
window behavior remains platform-dependent.

Other supported successful launch paths show the same waiting presentation. It
uses actual process detection, not a claim that internal game loading is complete.
Roblox detection refers to its client, not proof of joining a particular experience.
Optional supplied waiting lines appear separately from operational status. Back to library hides the cover while retaining launch tracking. Dismiss
never terminates a game. A timeout asks for attention rather than reporting failure;
late starts clear stale notices when the normal running monitor detects them.

The normal release test script includes profile isolation, launch presentation,
background-window behavior, update-dialog ownership and the standard visual audits.
Native launch audits use an explicitly supplied isolated profile and are not part
of automatic release tests. Public app publication is a separate step.

References:
- https://prismlauncher.org/wiki/getting-started/command-line-interface/
- https://dev.overwolf.com/ow-native/reference/extensions/ow-extensions/
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-logicaltophysicalpointforpermonitordpi

Minecraft launch covers wait for the selected profile log/process plus a newly
visible Minecraft Java window. Windows already open at dispatch are excluded.
This detects the game window, not completion of resource loading or world entry.
Probes run off the UI thread; the existing bounded timeout and escape actions remain.

