disposition: fix

Evidence note: reviewed only `artifacts/final-ui/manager-live2d.png` (1136 × 839) plus the current DESIGN.md, docs/FLUENT_2.md, surface brief and Live2D workspace source. No new comp or unrelated surface was reviewed.

## persistence

Pass. The page follows the current Fluent 2 native workspace direction: neutral surfaces, restrained brand blue, native list selection and compact action controls. The supplied copy explicitly documents model requirements and the GIF first-frame limitation.

## fidelity

| Element | State | Evidence |
| --- | --- | --- |
| Page hierarchy | match | Title, purpose line, capability notice, model/animation split, actions and status read in the intended order. |
| Model and animation workspaces | match | Both panels have visible headings; the selected model and existing `.anim` entries are legible and the native blue selection is clear. |
| GIF limitation | match | The notice says GIF cannot create a runnable rig and imports its first frame as a texture; this is honest and aligned with DEVELOPMENT_GUIDE.md. |
| Action copy | contradicted | The 120px “导入 Live2D ZIP” button visibly ellipsizes to “导入 Live2D…” in the capture, hiding the file format from the primary action. |
| Realtime expectation | contradicted | The subtitle says the directory “will be watched,” while the status says users must enable realtime mode first; the condition is ambiguous at the point of entry. |
| Empty/unsupported state | contradicted in source | `RefreshLive2d()` reports that an editable directory theme is required, but its four local action buttons are never disabled; handlers wait for a click and then show an error dialog. |

## ceiling

The page has the right working density and avoids decorative cards, fake glass, gradients and invented artwork. The notice is appropriately concise for a technical workflow. The remaining gap is state truthfulness and action labeling, not visual direction.

## material_fixes

1. [P2 · copy/clarity] Make the realtime precondition explicit in the subtitle, for example “启用‘实时更新主题’后，目录会被监听；保存文件即可尝试热重载。” Align the wording with the actual settings label.
2. [P2 · action clarity] Ensure the archive action label is fully visible: widen the button to fit “导入 Live2D ZIP” or shorten it to “导入 ZIP” while retaining the dialog title and filter.
3. [P2 · disabled/error state] Add a shared Live2D command-state update: disable directory/ZIP/texture actions until an editable theme is selected; disable animation import until a model is selected; keep the inline state message as the recovery instruction. Reserve modal errors for unexpected failures.

## keep

Keep the model/animation split, native list focus and selection, source-aligned model-format notice, explicit GIF first-frame behavior, real-time status feedback and restrained Fluent 2 palette.
