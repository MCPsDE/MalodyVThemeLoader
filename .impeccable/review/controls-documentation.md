# Shared controls documentation · 2.5.1

This handoff records the user's corrections to numeric chrome, wrapping manager text and incomplete command labels. It updates `PRODUCT.md`, `DESIGN.md`, `.impeccable/design.json` and `.impeccable/surface-brief.md` from the finished native implementation. Version `2.5.1` is a patch correction. The incumbent palette, native Chinese typography, credit, dark-default saved manager appearance, sun-left/moon-right toggle, manager-only sponsor and fixed-light installers remain the visual authority. There is no new world, artwork or approved comp.

## Implementation evidence checked

| Source | Durable rule recorded |
|---|---|
| `NumberField.cs` | Shared themed numeric composition; preferred height starts at 32 logical pixels and grows with the font. Native parsing/keyboard live inside a clipped host; native spinner buttons are hidden and their actual reserved width falls outside the visible input. Horizontal minus/plus use existing neutral/ink states. Range, read-only and disabled states govern stepping. Focus changes the outer border to accent. |
| `NumberField.cs` input and accessibility paths | The outer Tab stop forwards to the native editor. Step buttons have increment/decrement accessible names, while the container exposes a writable SpinButton value. Layout measures formatted range samples and uses the last committed value for step state, preserving partial input until explicit validation. |
| `ActionButton.cs` | `RequiredContentSize` measures the actual localized font and DPI, adds 26 logical pixels horizontally plus explicit padding, and refreshes content minima after text/font/padding/DPI changes. Painting uses 12-logical-pixel horizontal insets, `SingleLine` and no overflow ellipsis. |
| `SingleLineLabel.cs` and `ThemeManagerLayout.cs` | The wordmark, subtitle and version measure and paint one line. The rail measures these labels and all five destination buttons, including margins and panel padding, with a 208-logical-pixel floor. Selection and DPI changes refit the rail. |
| `ButtonRow.cs` | Full button preferred widths and an 8-logical-pixel gap determine whether actions sit side by side or stack. |
| `FieldGrid.cs` | Equal field columns use separate 10-logical-pixel gap tracks; column count reduces before the widest preferred field stops fitting. |
| `ThemeManagerLive2d.cs` and `ThemeResourceEditor.cs` | Live2D size/position and resource slice/preview dimensions use `NumberField`; resource action/field groups use shared reflow helpers. |
| `Directory.Build.props`, repository `AGENTS.md` and user-global `AGENTS.md` | Patch version 2.5.1 and the persisted shared control/text-fit/release constraints. |

`DESIGN.md` retains its existing color and typography primitives. Its relevant additions are NumberField component tokens, measured rail/layout facts, and The Complete Label Rule and The Themed Number Rule. The sidecar adds only a NumberField specimen, complete-label action/navigation specimens and the corresponding narrative rules; its existing unrelated component and color metadata are preserved. Browser specimens illustrate native appearance and do not reproduce or verify WinForms parsing, text metrics, accessibility or DPI behavior.

## Capture evidence

Each of `.impeccable/review/controls-dark` and `controls-light` contains these 12 current PNGs:

- `number-field.png`
- `desktop-themes.png`, `minimum-themes.png`
- `desktop-resources.png`, `minimum-resources.png`, `scale150-resources.png`
- `live2d-minimum.png`, `live2d-scale150.png`
- `MalodyVThemeLoader.Installer-desktop.png`, `MalodyVThemeLoader.Installer-minimum.png`
- `MalodyVBepInExInstaller-desktop.png`, `MalodyVBepInExInstaller-minimum.png`

Each of `.impeccable/review/controls-animation-dark` and `controls-animation-light` contains `editor-desktop.png`, `editor-result.png`, `editor-minimum.png` and `editor-scale150.png`. The total is 32 captures. Installer files in both harness sets correctly remain light; their directory names describe the harness's requested manager appearance, not a dark installer variant.

The parent opened all 32. Four workbench captures were recaptured and reopened after the test fixture catalog was completed. This documentation pass independently opened `controls-dark/number-field.png`, `controls-light/minimum-resources.png`, `controls-dark/live2d-minimum.png` and `controls-animation-light/editor-minimum.png`. Those samples show themed horizontal numeric steps, complete rail wordmark/subtitle/version and commands, the preserved footer, and the real model/audio editor at minimum size. Assessment of the whole matrix belongs to the separate finish reviewer.

## Verification evidence

| Script / logs inspected | Established result |
|---|---|
| `scripts/test-ui-constraints.ps1`, `scripts/assert-ui-fit.ps1`; `artifacts/controls-dark.log`, `controls-light.log` | Both end in PASS. The actual shared widget handles minus/plus, native parse/commit, Up key, partial `-` preserved through layout, range-disabled steps, disabled control and a writable accessible value. Geometry checks hide native spinner buttons, separate the input from themed steps, fit numeric range text, and fit full action/navigation labels. All five manager pages and both installers are checked at normal/minimum, synthetic 125/150/200% and 125% font size. |
| `scripts/inspect-ui.ps1`; `artifacts/ui-2.5.1-composed-dark.log`, `ui-2.5.1-composed-light.log` | Logs record reaching idle/tap/size through scrolling and real Tab navigation, with field rectangles inside the viewport. This complements widget/API checks with the composed workbench's focus path and bounds. |
| `scripts/test-live2d-editor.ps1`, adapted as `artifacts/test-animation-controls.ps1`; `artifacts/animation-controls-dark.log`, `animation-controls-light.log` | Both end in PASS for native model/motion frame changes, manual regions, overlap/fallback, all five result bindings, OGG/WAV decode/import/playback preparation/stop, persistence and invalid-region rejection. The temporary harness redirects captures into the controls-animation directories and invokes `Assert-UiFits` before each capture; the committed script itself does not contain that addition. |
| `artifacts/resource-controls-regression.log` | PASS for source audio/use catalog, tree editing, independent scopes, slice preview/dragging, atomic validation, OGG/WAV/MP3, audition/stop, persistence, inheritance and real-time activation. |
| `artifacts/model-controls-regression.log` | PASS for Murasame dependencies, all 12 motions, interpolation, rollback, custom root, Unicode import, overwrite/restore and the form's Apply/Restore/status behavior. |

No implementation tests were rerun by this documentation-only pass. Existing PNG iCCP warnings occur in the logs, while the relevant assertions end in PASS; original Default artwork is not modified to silence image-profile warnings.

Synthetic scaling and font stress do not establish physical monitor or multi-monitor DPI behavior. The accessible role/name/value API checks are not screen-reader validation. Silent audio fixtures establish preparation/stop, not audible output. Native bitmap captures do not verify compositor Mica, and none of these tests establishes an installed IL2CPP game run. No HTML/CSS detector applies to WinForms. No new raster is shipped: image evidence retains existing Default source-artwork and external user-model provenance.

## Rule persistence

The repository's `AGENTS.md` records the shared NumberField requirement, full one-line action/navigation labels, content measurement/reflow, variable-data full-value access, the light/dark size/scale matrix and patch-release policy. `C:/Users/Administrator/.codex/AGENTS.md` has the broader corresponding “UI Controls and Text Fit” guidance. Its pre-existing PowerShell patch invocation and runtime compatibility sections remain present. The global file cites the official guidance at `https://developers.openai.com/codex/guides/agents-md/` for the user-global location. These are persisted Codex instruction files; no actual system prompt was changed.

This handoff wrote only the five assigned documentation paths and did not write to the source repository or installed game. It does not claim release packaging, hashes or installer lifecycle checks that belong to the parent's delivery verification.

## System summary

- Palette: existing paired neutral layers, readable blue accents, shared solid action fills and the manager-only sponsor gradient.
- Type: native Chinese UI family chain and existing point-based hierarchy; labels remain full size and single line.
- Numeric controls: one shared themed editor with native input behavior and theme-painted horizontal steps.
- Layout: measured rail and command minima, shared action/field reflow, scrollable content and stable task actions.
- Rules: The State Rule, The Native Type Rule, The Complete Label Rule and The Themed Number Rule; manager/installer identity remains intact.

No unrelated pre-existing drift was repaired or canonized. Physical DPI, screen-reader use, audible output, Mica and installed-game integration remain outside this evidence's verified scope.

Documentation validation: JSON parsing and scoped consistency checks pass; the existing color/typography metadata and unrelated specimens are unchanged. `git diff --check` passes for the five assigned paths. All 32 listed captures and the cited current control/regression logs exist.
