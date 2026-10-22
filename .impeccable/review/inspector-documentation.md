# Online inspector documentation · 2.6

This handoff records the online scene inspector and exact-node background/video authoring as an extension of the incumbent Fluent WinForms Operate workspace. It merges justified implementation facts into `PRODUCT.md`, `DESIGN.md` and `.impeccable/design.json`. The paired palette, point-based native Chinese typography, exact credit, dark-default saved manager appearance, sun-left/moon-right toggle, manager-only sponsor and fixed-light installers remain the visual authority. No new identity, concept seed, approved comp or shipping raster applies.

## Implementation evidence checked

| Source | Extension recorded |
|---|---|
| `ThemeResourceEditor.Inspector.cs` | Shared “资源文件” / “在线调试” mode row. Runtime image selection opens the catalog resource at its scene, hierarchy, component and exact node address. Navigation resolves unsaved drafts. |
| `SceneInspectorPane.cs` | Leased connection and truthful connection status; live searchable hierarchy with hidden-node option; independent bounds/name options; node location and background preview; image/video import and removal; opacity, fit, size and slice fields; stable save/restore and concise status outside the scrolling editor. Shared buttons, NumberField, ChoiceBox and TextField reuse existing tokens. |
| `Windows/TextField.cs` and its resource-editor callers | A borderless native TextBox inside a themed working-surface frame replaces exposed native text borders in resource search, hierarchy entry and online search. Focus uses the readable accent rule. Minimum height is 34 logical pixels or native preferred height plus 12; horizontal insets are 7 pixels. Native typing, selection and keyboard behavior remain in the editor, with frame metrics refreshed for font/DPI changes. |
| `Plugin/SceneInspectorController.cs` | Main-thread snapshots and local lease; bounds/names over visible UI; middle-click picking with overlap cycling and bidirectional selection; clip-aware debug drawing; bounded UInt16 mesh batches; expiry and failure remove debug geometry. |
| `Plugin/InspectorInputFence.cs` and the inspector overlay | A transparent loader-owned Image receives UI raycasts while the middle button is held and for the brief release window. It keeps picking from reaching panels' close handlers, then passes ordinary UI raycasts through. No game input-dispatch method is patched. |
| `Plugin/SceneNodeLocator.cs` and `Shared/SceneInspector.cs` | Loaded allowed scenes and persistent objects, including hidden nodes. RectTransform/Canvas determines decoration eligibility. Exact escaped-name addresses include same-name sibling index/count; instance IDs are session-only. Ambiguous or changed identity fails closed. Snapshots report truncation at the collection limits. |
| `Plugin/NodeDecorationService.cs` | Background/video layers follow target geometry, visibility and lifecycle without replacing foreground content or intercepting input. VP8 alpha and independent opacity, fit, explicit/automatic dimensions and image slicing use existing media controls. Preparing or failed video retains the background; hidden targets pause, and disposal releases media. |
| `Shared/WebmVideo.cs` and `docs/SCENE_INSPECTOR.md` | VP8 WebM validation with a concise VP9-to-VP8 correction; no video audio. Optional node rules preserve format version 1, atomic save, exact-node restore and ZIP media persistence. Local transport uses client/runtime/request identity and a renewable five-second lease. |

`DESIGN.md` retains its existing color, typography, spacing and shape primitives. The only new primitive component record is `text-field`, using existing palette/type tokens and the implemented minimum height. It records the mode-row, online layout, node-media states and TextField treatment without promoting task-specific composition into a new global visual world. The sidecar adds only paired TextField and mode-row specimens; existing color/type metadata, narrative and unrelated specimens are preserved. HTML specimens illustrate native appearance; they do not replace or validate WinForms metrics, accessibility or behavior.

## Capture evidence

Each of `.impeccable/review/inspector-dark` and `inspector-light` contains these eight native `Form.DrawToBitmap` PNGs:

- `disconnected.png`
- `node-draft.png`
- `node-media.png`
- `picked-resource.png`
- `video-settings.png`
- `minimum.png`
- `minimum-video.png`
- `scale150.png`

`.impeccable/review/inspector-runtime/hierarchy.png` and `node-video.png` add the isolated Unity render pair, for 18 captures total. The parent opened the initial 18 before the finish handoff. This documentation pass initially sampled dark disconnected, light node media, dark minimum-video, light synthetic 150%, and runtime node-video. The final correction regenerated all 16 native captures and the runtime pair. This pass reopened the final light `node-media.png` and confirmed the persistent search label, preserved shell/footer, themed fields, stable save/restore and empty-node background state. The separate finish reviewer owns the full-matrix visual verdict.

The final source and regenerated capture use “搜索节点名称或路径” above the TextField, independently of its placeholder and accessible name. Both final native-editor logs end in PASS. The parent owns the verdict handoff; this document does not assign a review disposition.

## Verification evidence

| Script / inspected evidence | Established result |
|---|---|
| `scripts/test-scene-inspector.ps1`; `artifacts/inspector-editor-dark.log`, `inspector-editor-light.log` | Both end in PASS for live tree/search/hidden nodes, empty-node image and VP8 alpha-video authoring, opacity/fit, invalid-codec preservation, exact resource navigation, same-name sibling isolation, real-time activation, restore, ZIP and geometry. The capture matrix covers both appearances, normal/minimum and synthetic 150%. |
| `artifacts/resource-regression-2.6.log` | PASS for the surrounding image/UI-audio catalog, tree, global/scene/component rules, slice preview/dragging, atomic validation, OGG/WAV/MP3 preparation/stop, persistence, inheritance and activation. |
| `artifacts/inspector-full-build.log` | Release solution build succeeds with zero warnings and zero errors. |
| `artifacts/interop-2.6.log` | Static release compatibility validation resolves 311 game/Unity references, nine Cubism signatures, three widget targets, three result-display targets, three UI-audio targets, nine effect slots and the volume adapter against the installed interop assemblies. This is reference/signature validation, not an installed-game run. |
| `scripts/test-inspector-runtime.ps1`, `scripts/validation/InspectorRuntimeValidation.cs`; `artifacts/inspector-runtime.log`; committed `inspector-runtime/result.json` | Isolated Unity 2022.3.62f3 play mode passes 28 checks. Production source adaptations replace IL2CPP array/delegate wrappers and constructors plus a Mono-incompatible atomic-write overload; the release plugin is not run in this project. |
| Runtime pixel and hierarchy assertions | A real VP8 alpha video produces frames with opaque and transparent pixels. The UI composites it at 45% opacity over the retained background, preserves existing foreground/other sprites, and inserts non-intercepting layers behind content. Hidden-node discovery, exact sibling isolation, RectMask2D-aware picking, overlap cycling, bidirectional selection, bounds/names, empty surrounding pixels, 4,000-node UInt16 mesh batching, projection coordinate mapping, lease expiry, disposal and failed-decoration cleanup are asserted. |
| Runtime UI-raycast assertions | Actual `EventSystem.RaycastAll` with a middle-button pointer hits the loader-owned `InspectorInputFence` first during capture. After the release window, a left-button pointer reaches the original Image. The harness primes capture programmatically; it does not send a physical OS middle-button click or operate the installed game's UIHalfPanel. |

No implementation tests were rerun by this documentation-only pass. The runtime harness calls selection, capture and projection paths programmatically; it does not establish physical mouse-input delivery or installed-game end-to-end use. The native editor tests establish authored state, persistence and geometry, while the isolated runtime test establishes actual decoding, pixels and UI-raycast routing in that project. Static release interop validation is separately recorded above; none of these results establishes operation of the installed IL2CPP game.

Synthetic 150% is layout stress, not a physical monitor or multi-monitor DPI check. No screen reader was tested. Native bitmap captures do not verify compositor Mica. Video audio is intentionally disabled, and the prior silent audio regression fixtures do not establish audible output. No HTML/CSS detector applies to this WinForms surface.

## Asset provenance and write boundary

The product logo/icon and original Default/source artwork are unchanged. The inspector background and VP8 alpha WebM, colored runtime regions and debug geometry are synthetic validation fixtures, not shipping raster artwork or replacement Default assets. Runtime screenshots record those fixtures. This extension adds no newly generated shipping raster requiring an artwork origin.

This handoff writes only `PRODUCT.md`, `DESIGN.md`, `.impeccable/design.json` and `.impeccable/review/inspector-documentation.md`. The parent owns `.impeccable/surface-brief.md`, implementation, release packaging, hashes, read-only input checks and installer lifecycle verification. No source-repository or installed-game file is written by this documentation pass. Pre-existing unrelated drift is not repaired or canonized.

The parent's directory audit retains concurrent external plugin/theme differences. This documentation makes no claim that the entire installed game directory is unchanged; scoped source/core/loader verification belongs to the parent's delivery record.

Documentation validation passes: the sidecar parses as JSON; its existing extensions, narrative and every pre-existing specimen match the committed content; only the two scoped specimens and generation timestamp are added. All 18 captures, cited logs and the passing 28-check runtime result exist. `git diff --check` passes for the assigned paths.
