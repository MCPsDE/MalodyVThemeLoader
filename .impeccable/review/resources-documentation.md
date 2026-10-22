# Resource editor documentation

The 2.5 resource workflow is recorded in `PRODUCT.md`, `DESIGN.md` and `.impeccable/surface-brief.md`. This pass addresses persistence fix 3 in `resources-verdict.md` and reconciles the corrected scope-selection and preview behavior with the existing Fluent 2 WinForms system. It preserves the incumbent frontmatter, palette, typography and manager/installer identity.

## Implementation evidence checked

| Source | Recorded behavior |
|---|---|
| `ThemeManagerLayout.cs` and `ThemeManagerResources.cs` | Five navigation destinations, labeled theme choice, resource-page composition and refresh after save/activation. |
| `ThemeResourceEditor.cs` | Labeled search, image/UI-audio tree, source/usage viewer, 24/44/32% columns, independently scrolling right fields, fixed Save, scope-read/cancel actions, numeric slice alternatives and focus scrolling. |
| `ThemeResourceEditor.Actions.cs` | Manual scope commit and validation; exact/inherited file, mode, border and preview reload; blocked import/save for uncommitted scope; cancellation retaining the loaded draft; clean same-key reload; draft/save/restore/audition status; directory identity after ZIP materialization. |
| `ResourceTreeView.cs` | Native hierarchy and keyboard selection with ellipsis, focus rectangle and horizontal scrolling disabled. |
| `ResourcePreviewSurface.cs` | Proportion-preserving source/target fitting, fixed neutral alpha checker, draggable accent slice guides, actual stretch rendering and waveform metadata. Checker values are local image-preview rendering, not a replacement application palette. |
| `ThemeResourceWorkspace.cs` | Safe theme-relative paths, readable display filenames, hash storage, fresh rule reads, border validation and atomic resource-document publication. |
| `SourceUsageCatalog.cs` and `docs/RESOURCE_AUTHORING.md` | Direct serialized scene-use discovery; nine distinct UI events plus result music; authoring format and explicit dynamic-usage limits. |
| `ThemeAudioPatches.cs` and `ThemeAudioService.cs` | Narrow SoundManager UI-event/result hooks, separate AudioSources, existing UI-effect volume and caller result volume, original-audio fallback and result stop on scene exit. |

The same source image can have separate files and slice borders by scene or exact control. Restore removes the current exact rule and returns to inheritance. Static source usages do not enumerate dynamic factories, nested prefabs or every script assignment. The tool previews resources and stretch behavior, not a complete running-game scene.

## Captures and test evidence

All ten current capture paths exist: `resources-images.png`, `resources-minimum.png`, `resources-audio.png`, `resources-scale150.png` and `resources-control.png` under each of `.impeccable/review/resources-dark` and `resources-light`. This documentation pass opened the light normal image capture and dark exact-control capture. They show actual existing source artwork, visible transparency ground, the three-column workflow, five navigation destinations, loaded scope, stable Save and preserved manager footer. Parent finish review owns assessment of the complete ten-image set.

This pass inspected `scripts/test-resource-editor.ps1` and both `artifacts/resource-editor-dark.log` and `artifacts/resource-editor-light.log`, which end in PASS. The script checks source catalog completeness, independent global/scene/control rules, manual scope reopening and cancellation, clean same-key external reload, slice dragging, invalid-save protection, OGG/WAV/MP3 decode, silent audition preparation/stop, persistence, inheritance, real-time activation and a materialized legacy ZIP's canonical directory selection. Dirty-draft retention on same-key refresh is evidenced by the implementation; the inspected script does not independently exercise every dirty-draft branch. No implementation tests were rerun by this documentation-only pass.

`artifacts/resource-render-test/result.json` reports `passed: true` for actual Unity source Sprite creation, independent scene textures, provenance, exact-control scope, borders, pivot/PPU, rendered slice meshes, gameplay exclusion and invalid/missing fallbacks. The corresponding `scene-slices.png` exists. The inspected render script builds an isolated Unity 2022.3 validation project using production image sources with IL2CPP byte-array/logging/API compatibility adaptations. Its Mono-compatible replacement for an uncalled file-write overload is not the shipped atomic writer; desktop tests cover that writer. This is Unity validation, not execution of the installed IL2CPP game or runtime audio hooks.

Synthetic 150% captures establish layout stress, not physical monitor DPI, multi-monitor behavior or assistive-technology results. Silent audio fixtures establish preparation and stop, not audible device output. Bitmap captures do not verify compositor Mica. No HTML/CSS detector applies to native WinForms. This pass operated only inside the repository and wrote the four assigned documentation paths plus the expressly authorized navigation specimen in `.impeccable/design.json`; it did not modify the read-only source or installed game.

## System summary

- Palette: existing paired neutral surfaces, readable blue accents and shared solid action fills; fixed gray alpha checker is local to image preview.
- Type: incumbent native Chinese UI family chain and point-based hierarchy, with no frontmatter changes.
- Layout: native resource tree, central source/stretch or waveform preview, independently scrolling scope fields and fixed Save.
- Rules: The State Rule and The Native Type Rule remain in force; draft, scope-read and saved/activated states stay explicit.
- Identity: exact credit, saved sun-left/moon-right manager appearance, manager-only sponsor, light-only installers and original Default artwork.

The extension added the fifth navigation destination, so `.impeccable/design.json` now synchronizes its existing Task navigation specimen: the description says five destinations, and both dark/light HTML lists add “资源编辑” immediately after “主题库”. The follow-up changes only those two JSON fields; every CSS declaration, font, token and other specimen remains unchanged. This is factual synchronization of the implemented extension, not a visual-system redesign. Physical DPI, audible output and installed-game integration are not canonized as verified behavior.

`git diff --check` passes for all five assigned paths. The entire `DESIGN.md` frontmatter retains its before/after SHA-256 `71902113B4ABCF4EFCE911A5323267D3E72FF75BE85A6FA0B2AFE63A469D6D38`. The synchronized `.impeccable/design.json` SHA-256 is `70CEA448E725F1E32AB0778A1836EA0F5E1B0D2E5541958238C5EFFA4D0D4F78`. All ten named capture files, both editor logs and the Unity result/image paths exist; the follow-up generated no captures or product changes.
