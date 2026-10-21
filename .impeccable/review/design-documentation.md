# Design documentation handoff

Documentation refresh completed from the final native WinForms source on 2026-10-21. Existing palette, typography, light surfaces, native affordances and task hierarchy were preserved.

## Sources checked

- `PRODUCT.md`, `.impeccable/surface-brief.md` and the incumbent `DESIGN.md`.
- `AppVisuals.cs` and `ActionButton.cs`: reused colors, resolved font families, type sizes, spacing patterns, button geometry and paint states.
- `ThemeManagerLayout.cs`, `ThemeManagerForm.cs` and `ThemePreviewSurface.cs`: navigation, library/detail layout, fixed-height owner-drawn ListBox with horizontal scrolling disabled, persistent search label, empty selection, metadata visibility, commands and real-artwork preview. The manifest action retains its full label with a local minimum width.
- ThemeLoader and standalone BepInEx `InstallerForm.cs`: path diagnosis, scrollable content, logs, stable actions and operation states.
- The first finish-review report. Its screenshots were evidence for the build thread; this documentation handoff does not claim a fresh screenshot, physical DPI or interaction test.

## Artifact contract

`DESIGN.md` now carries implementation-derived token frontmatter and the eight canonical sections. `.impeccable/design.json` uses schema version 2 as an extension-only sidecar, with color/type metadata, narrative and eight illustrative component samples. Token primitive arrays are no longer duplicated in the sidecar.

The sidecar's OKLCH ramps are synthesized documentation swatches requested by the document format. They are not additional application colors and do not prescribe a new palette. Its self-contained HTML/CSS component specimens visualize native patterns in the documentation panel; they do not replace WinForms controls or establish browser rendering as native ground truth.

No custom shadow, animated transition or breakpoint tokens were invented. The empty extension arrays describe their absence. Native system typography is an intentional task-workspace choice. The workflow's missing concept-seed round remains disclosed in the surface brief; this handoff does not fabricate a seed or claim a native exemption.

## Scope and verification boundary

Only `DESIGN.md`, `.impeccable/design.json` and this handoff were changed by the documenter. No application code, product brief, source artwork or installed game files were edited. No image generation was used. Existing logo/icon assets and source-derived theme previews supply the artwork.

The source-based check verifies shared token values, token references, sidecar parsing, narrative consistency and canonical section order. Existing normal/minimum-size captures and synthetic 150% stress are described within their limits. No HTML/CSS detector applies to the native application, and no physical multi-monitor DPI test is claimed.

Not canonized or repaired: the standalone BepInEx path-state control's local DarkGreen/DarkRed colors remain a one-off implementation detail, excluded from shared palette tokens. This documentation task does not expand into application changes or reopen finish-review findings.
