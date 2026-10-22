# Live2D 2.3 documentation record

The Live2D workbench extends the incumbent Fluent 2 Windows utility. It introduces no new visual world, color primitives, artwork or approved comp. The shipped documenter role and `reference/document.md` guided this narrow merge; the existing product and design decisions remain authoritative.

## Persistence updated

| Record | Change grounded in the implementation |
| --- | --- |
| `DESIGN.md` | Records the scrolling binding editor, fixed save/restore/status area, current captures, native list typography and palette-aware ChoiceBox states. Existing frontmatter tokens, type ramp, identity, named rules and footer constraints are preserved. |
| `.impeccable/design.json` | Adds one paired dark/light illustrative ChoiceBox specimen using existing palette values. Native C# behavior and metrics remain authoritative. Existing specimens and narrative are preserved. |
| `.impeccable/surface-brief.md` | Adds the 2.3 theme/import/model/motion/fitting workflow and current evidence paths to the existing Operate brief. |
| `PRODUCT.md` | Adds the narrow Live2D authoring workflow and distinguishes saving from runtime loading feedback. |

## Source truth checked

| Source | Observed behavior |
| --- | --- |
| `src/MalodyVThemeLoader.ThemeManager/ThemeManagerLive2d.cs` | Editable-theme selector; imports before model and animation lists; looping idle and tap/return-to-idle choices; percentage fitting fields; separate scroll editor and fixed 96-logical-pixel footer; explicit page column and viewport width constraints; focus-entry scroll requests; save/activate, restore and current runtime status. Legacy selections disable Cubism binding fields. |
| `src/MalodyVThemeLoader.ThemeManager/Live2dWorkspace.cs` | Recursively discovers `.model3.json` descriptors and model animation files; imports directory/ZIP resources and saves model/motion/fitting bindings. |
| `src/MalodyVThemeLoader.ThemeManager/ThemeManagerLayout.cs` | Preserves the four task destinations, manager shell, normal/minimum dimensions, result band and manager branding. |
| `src/MalodyVThemeLoader.Windows/ChoiceBox.cs` | Preserves the native dropdown, selection, keys and accessibility; paints the closed field, chevron and dropdown options from the shared palette, including focus and disabled text. |
| `src/MalodyVThemeLoader.Windows/AppAppearance.cs` and `AppVisuals.cs` | Reuses the established dark/light palette and native UI family. Palette propagation handles numeric child text without treating foreground ink as a background token. |
| `scripts/inspect-ui.ps1` | Captures native forms with `DrawToBitmap`; checks scroll-parent containment, idle-to-tap-to-size native focus traversal and those fields' viewport rectangles before reached-field captures. |

## Current visual evidence

Both `.impeccable/review/live2d-dark` and `live2d-light` contain the following nonempty files. Current correction captures were checked on 2026-10-22 UTC; the dark reached-field set was refreshed at 04:54 and the light set at 04:53.

| Capture in each palette directory | Evidence |
| --- | --- |
| `manager-live2d.png` | Normal workbench shows theme choice, four import/replacement actions, model and animation lists, motion/fitting fields and fixed actions. |
| `manager-live2d-minimum.png` and `manager-live2d-minimum-fields.png` | Top and scrolled states separate the editor's available space from fixed save/restore/status controls. Reached fields retain their right edges in both palettes. |
| `manager-scale150.png` and `manager-scale150-fields.png` | Synthetic 150% top and scrolled states show an intact theme choice, motion choices, all three fitting fields and editor scrollbar, while fixed actions remain visible. |
| `manager-switched.png` and `manager-restored.png` | Captured appearance switching retains the selected Murasame theme and the manager's author credit, sun-left/moon-right control and sponsor action. |
| `installer-desktop.png` and `bepinex-desktop.png` | Both installers retain light surfaces and credit-only footers across the capture sets. |

The exact credit remains `by 猫猫虫小窝(451586654)`. Only the manager has the saved dark-default appearance toggle and `赞助支持` action targeting `https://ifdian.net/a/mcpsde`. No new shipping raster was created by this documentation pass.

## Evidence limits and drift

The source and current captures resolve the previous documentation's outdated Live2D copy, lists-before-import order, omitted binding editor/ChoiceBox and older toggle evidence paths. The prior `live2d-verdict.md` persistence finding requires the finish reviewer to assess these updated records; this document does not replace that verdict.

No additional pre-existing drift was identified in the sampled shared palette, type and identity records, and no unrelated system repair was made. Synthetic form scaling does not establish physical DPI or multi-monitor behavior. Bitmap captures do not verify DWM Mica, assistive-technology behavior or successful in-game model application. The harness checks idle/tap/size traversal; it is not a complete keyboard or screen-reader audit. Static ChoiceBox HTML specimens illustrate colors and states only.
