# Runtime overlay documentation · 2.6.1

This handoff records the narrow correction for the user's report that 2.6.0 game bounds and node names were invisible. It merges the verified behavior and evidence into `PRODUCT.md` and `DESIGN.md`. The incumbent Fluent WinForms world, paired palette, native Chinese typography, manager/installer layouts, shared controls and prior authoring workflows retain their existing authority. No new visual world or shipping raster applies. The separate finish reviewer owns the visual verdict; this document assigns no release disposition.

## Failure and correction

The installed 2.6.0 log reported `Inspector drawing stopped: Method unstripping failed`. Offline inspection of its actual interop assembly found that `Font.CreateDynamicFontFromOSFont(string, int)` exists and resolves, but its body is an exception stub. The old inspector created this font while setting up its overlay, so that call stopped both bounds and labels. The earlier 311-reference resolution check did not detect this callable-body failure.

`src/MalodyVThemeLoader.Plugin/SceneInspectorController.cs` now borrows a font from retained UI text or loaded font assets, then tries built-in fonts as local fallbacks. With no font, it retains bounds, omits labels and retries once per second. A recovered font is applied to existing label objects. Disposal destroys inspector-owned objects, meshes and material, preserving the borrowed game/built-in font. Inspector-stopping logs include the full exception rather than only its message. Bounds, selection colors and label styling are otherwise the incumbent functional overlay.

`scripts/test-interop-compatibility.ps1` checks generated method bodies as well as reference resolution. `artifacts/overlay-old-interop.log` rejects the old DLL's unguarded OS-font exception-stub call. `artifacts/overlay-new-interop.log` passes 311 game/Unity references without unguarded stubs, with one locally caught optional legacy-font fallback; it also preserves the existing Cubism, widget, result-display and UI-audio signature checks. This is offline compatibility evidence, not execution of the release plugin in Malody V.

## Capture evidence

This documentation pass opened all three final PNGs in `.impeccable/review/overlay-2.6.1`:

| Capture | Observed scope |
|---|---|
| `overlay-presented.png` | Final 640 × 480 screen from the standalone Windows Mono player. Cyan/neutral outlines, yellow selected-node bounds, CJK node labels and the existing foreground caption are visible around the synthetic UI regions. |
| `hierarchy.png` | Render-target geometry view with bounds/names, same-name sibling regions, selected-node bounds and a clipped-region fixture. |
| `node-video.png` | Media-layer view with the synthetic background, alpha-video composite, foreground caption and surrounding sibling regions preserved. This view intentionally has no inspector geometry. |

The standalone harness returns the overlay Canvas to `ScreenSpaceOverlay`, waits for `WaitForEndOfFrame`, and uses `ScreenCapture.CaptureScreenshotAsTexture` for `overlay-presented.png`. Separate edge and interior-glyph pixel counts assert that both bounds and names appear on the presented frame. The two render-target views support the surrounding geometry/media assertions; they are not substituted for this final-screen check.

`scripts/validation/HiddenDesktopPlayer.cs` gives the test player a normal window on a private Windows desktop that is never activated onto the user's desktop. This permits normal Unity screen presentation without foregrounding or controlling the user's game. A hidden-window black frame is not used as passing overlay evidence.

## Verification evidence

| Source / inspected evidence | Established result |
|---|---|
| `scripts/test-inspector-runtime.ps1 -Standalone`; `scripts/validation/InspectorRuntimeValidation.cs`, `InspectorValidationEntry.cs`, `HiddenDesktopPlayer.cs` | Builds and runs a Unity 2022.3.62f3 Windows Mono test player. Runtime UGUI is compiled from its package; the source project's editor-referencing UI assembly is not shipped into this player. |
| `artifacts/overlay-player-2.6.1.log`; committed `overlay-2.6.1/result.json` | `passed: true`, 33 checks, 640 × 480, automatic `ScreenSpaceOverlay` presentation. |
| Font assertions in `InspectorRuntimeValidation.cs` | Labels reuse the fixture's existing UI font; simulated font loss leaves inspection and bounds active while removing stale labels; retry recovers names; inspector disposal leaves the borrowed font alive. |
| Preserved runtime assertions | VP8 per-pixel alpha with 45% opacity over a retained background, preserved foreground and unrelated images, exact-node identity, hidden nodes, clipping-aware picking, overlap cycling, bidirectional selection, middle-click UI-raycast capture and ordinary-click routing, UInt16 large-hierarchy batching, projection mapping, lease expiry, disposal and invalid-decoration cleanup. |
| `artifacts/overlay-build-2.6.1.log`; `artifacts/overlay-compile-all` | Full solution Release build succeeds with zero warnings and zero errors. |
| `artifacts/overlay-old-interop.log`, `overlay-new-interop.log` | Old unguarded font binding is rejected; the corrected plugin passes the strengthened installed-interop inspection. |
| `artifacts/reference-final-2.6.1.log` | Source and stable game files pass the reference audit, with zero difference records in that audit. This is read-only metadata evidence; configuration content is outside the audit's assertion. |

No implementation tests were rerun by this documentation-only pass. The standalone player uses a synthetic scene and adapted production sources: IL2CPP arrays, delegates and constructors use their Mono equivalents, and the atomic writer uses a compatible overload. Its fixture creates its own font through Mono; that fixture call is not the corrected release inspector's font-acquisition path. The release plugin's actual interop inspection is separate evidence.

The test does not run Malody V or establish actual IL2CPP game end-to-end behavior, physical mouse delivery, physical monitor DPI, screen-reader use or audible output. The prior dark/light, normal/minimum and synthetic-scale native captures remain the evidence for the unchanged manager/installer layout. No new desktop layout or palette claim is inferred from the 640 × 480 runtime fixture. No HTML/CSS detector applies to this native surface.

## Provenance and write boundary

The logo/icon, source artwork and Default theme are unchanged. Colored regions and alpha-video imagery in these captures are synthetic validation fixtures, not shipping product artwork or Default replacements. Debug bounds and labels are functional runtime drawing.

This pass writes only `PRODUCT.md`, `DESIGN.md` and `.impeccable/review/overlay-documentation.md`. It merges narrative evidence into the existing documentation without regenerating the design system. `DESIGN.md` frontmatter and `.impeccable/design.json` remain unchanged; the sidecar's SHA-256 is `BDD8B44BE9777910B69A419CF521C26B5E451BB33B83B41E49BCB9943487AE8D`. Implementation, packaging, hashes, reference audits and the final reviewer handoff belong to the parent. The source repository and installed game are not written, launched or controlled by this documentation pass.
