disposition: fix

Missing inputs: a QUALITY BAR card and a corroborating concept seed or documented native code-led exemption. No approved comp is required for this code-led build. Physical DPI, hover and keyboard interaction were not captured; focus and loading were assessed from the provided source.

## persistence

Pass: PRODUCT.md, DESIGN.md, the surface brief and design tokens exist and describe the rendered Windows utility. All 14 named captures exist, contain the expected surfaces and are reviewable. The 150% captures establish synthetic layout stress only. FORM does not record a concept seed; clarify the applicable native code-led route before closing the design record.

## fidelity

| Element | State | Evidence |
| --- | --- | --- |
| TYPE | match | Chinese Windows UI typography, clear page/section/body levels; the native utility brief explicitly authorizes Microsoft YaHei UI / Segoe UI. |
| MATERIAL | match | Flat working surfaces, fine rules and a real source-derived preview; no simulated physical material or decorative artwork. |
| GROUND | match | The visible neutral canvas, white surfaces and restrained green action color match DESIGN.md and the token file. |
| Installation topology | match | Directory, inline environment diagnosis, log and stable primary action; installer-reuse.png explicitly explains retaining existing BepInEx. |
| Manager topology | match | Persistent navigation, library/detail split, ZIP import/export, separated settings and maintenance. |
| Narrow library | contradicted | manager-minimum.png shows a horizontal list scrollbar for a single short Default row; full-width rows should fit the column. |
| Empty and error states | contradicted | manager-empty-search.png describes an absent preview as a theme without artwork and still offers an enabled manifest link; installer-invalid.png displays English validation inside a Chinese flow. |
| Search discovery | adaptation | AccessibleName and PlaceholderText exist in ThemeManagerLayout.cs, but the cue is absent in every supplied empty-field bitmap; a persistent label would make the task clear independently of native cue rendering. |
| Shared controls | contradicted | Black corner wedges are visible around many buttons in all three apps, against the promised quiet native finish. |

## ceiling

The Windows utility world is appropriately restrained. Native window framing, file dialogs, checkboxes, list keyboard selection, focus rectangles and common Ctrl+F/Ctrl+O/F5 commands are present in source. The remaining finish gap is reliable control painting and truthful state presentation; added decoration, animation or display typography would not improve this workflow. No external QUALITY BAR card was supplied.

## material_fixes

1. [P2 · contract/persistence] FORM has no corroborating seed: record the actual concept seed if that route ran, or document the explicit Windows code-led route exemption; do not invent a seed after the fact.
2. [P2 · floor/controls] Resolve the black button corners across the three applications: ActionButton.OnPaint clears to Parent.BackColor while Stack panels are transparent; paint against the nearest opaque ancestor or use native transparent background painting, then confirm the same captures have clean corners.
3. [P2 · FORM/adaptivity] Fit the theme-list column after its final layout and on resize so manager-minimum.png has no horizontal scrollbar; retain vertical detail scrolling and all theme actions at the supported minimum.
4. [P2 · truth/states] Give the zero-result selection a truthful empty state: suppress preview-missing language and empty metadata, and hide or disable “查看 theme.json” when no theme is selected; its current unconditional click opens an empty modal.
5. [P2 · STORY/error recovery] Translate known directory-validation failures into Chinese and tell the user to select the folder containing Malody V.exe; installer-invalid.png currently shows only “The selected directory does not exist.” in muted text.
6. [P2 · FIRST VIEWPORT/discovery] Add a persistent compact “搜索主题” label beside or above the field, retaining its existing AccessibleName and cue; the supplied first viewport otherwise presents an unexplained blank input and relies on a distant footer to explain it.
7. [P2 · floor/loading] In the standalone BepInEx installer, SetBusy(true) must set visible “正在安装/卸载…” feedback and a busy indicator or wait cursor before work begins; the current source only disables inputs and clears the log, leaving the previous ready/result text visible until completion.

## keep

Keep the directory-first install flow, explicit BepInEx reuse explanation, real Default preview, immediate ZIP actions, restrained green hierarchy, separate maintenance page, and absence of agreements or device restrictions.
