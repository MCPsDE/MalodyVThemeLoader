# Malody V ThemeLoader

<!-- impeccable:product-schema 1 -->

## Platform

Windows x64 desktop, native WinForms.

## Users and purpose

Malody V players install a visual theme loader once, then launch the original game normally. Theme authors import, export and edit ZIP themes, including the original Default resources.

## Stack and constraints

C#; .NET 8 WinForms installer and manager; BepInEx 6 IL2CPP plugin targeting .NET 6. Offline self-contained installation. Chinese interface. Existing compatible BepInEx is reused. No agreements, forced reading or device authorization. Missing theme resources fall back to game assets. Source and installed reference game are read-only during development.

## Workflows

Select game directory, inspect environment, install or repair. Browse themes, preview, activate, import/export ZIP and manage runtime settings. Maintenance actions are separate from everyday theme selection. No automatic game launch.

In “资源编辑”, choose an editable theme and find an image or UI-audio event through the directory tree and labeled search. Image authors compare the source and stretched preview, choose a global, scene or exact-control scope, and import a replacement. The same source image can have independent files and slice borders in different scenes or controls. Exact-control rules inherit from scene rules, then global rules, then the theme's original image or game asset. Double-clicking a source usage reads its scope directly; manually entered paths and component types require “读取此范围” before import or save. “取消定位” retains the loaded scope and its resource draft. Static usage listings cover direct serialized scene references; they do not enumerate every dynamic prefab or provide a full game-scene preview.

Optional nine-slice editing offers draggable guides, four pixel margins and a target-size preview; known RawImage targets show the whole texture instead. UI audio supports nine built-in SoundManager events and result-screen music_finish, with OGG Vorbis, WAV and MP3 import, waveform preview and audition/stop. Filenames remain readable while stored replacement paths use content hashes. Runtime playback uses separate AudioSources and the original UI-effect volume; result music uses its caller's volume. Library music, song previews, chart audio and hit sounds remain outside this workflow.

Imports and slice changes remain drafts until “保存并实时应用” commits them, activates the theme and enables real-time loading. Navigation confirms discarding unsaved changes; failed validation keeps the draft. “恢复此范围” removes only that rule and restores inheritance. A clean F5 or same-theme refresh reloads external rule changes, while a dirty draft is retained. Editing an older ZIP materializes a directory, and activation refreshes the theme library to that directory.

In the Live2D workbench, choose an editable theme, import a model directory or ZIP, choose a discovered model, and adjust size and position. “预览与绑定动画” opens a real Cubism model and motion preview. Draw rectangular click regions, name them, assign animations and test them on the model. Regions and motion choices are manual; body parts, HitAreas and motion names do not create automatic assignments.

Bind looping idle, clicks outside assigned regions, M1–M5 result-display events and custom regions. Each animation can carry OGG Vorbis or WAV audio, shared wherever that animation is reused. Audition plays the animation with its audio; changing playback stops the previous sound. “完成绑定” validates and returns a draft to the workbench. “保存并应用模型” commits the complete binding and activates the theme. Restore removes the model replacement; concise status distinguishes unapplied edits, saved configuration and the game's reported loading result.

## Evidence

Numeric editors, action labels and navigation must follow the established appearance in every state. The shared `NumberField` retains typing, arrow keys, validation, range and disabled behavior while replacing exposed native white spinner chrome with themed minus/plus controls. Commands and navigation stay complete on one line at the actual localized font and DPI; shared measurement and layout helpers grow or reflow their containers. Intentional dialog-opening ellipsis is punctuation; variable paths and filenames may shorten only with full-value access. Routine UI corrections use a patch release; this correction is 2.5.1.

Existing native application, source artwork, catalog, packaging scripts and disposable installer tests. User requested a modern, clear, concise interface and thorough inspection. The manager defaults to dark and saves the chosen appearance through a two-position sun/moon toggle. Both installers always use light appearance and have no appearance or sponsor controls. Author credit is `by 猫猫虫小窝(451586654)`; the manager's multicolor `赞助支持` button opens `https://ifdian.net/a/mcpsde`. Live2D UI copy should describe tasks and results briefly, with detailed implementation limits kept in developer documentation. Release artifacts go directly into `artifacts/release`.

The 2.4 editor evidence is in `.impeccable/review/editor-dark` and `editor-light`, with native preview, manual-region, result-display and audio checks logged in `artifacts/editor-dark.log` and `artifacts/editor-light.log`. The preview uses the selected game's existing Cubism Core with cached Skia rendering, GPU acceleration when available and a CPU fallback. Captures show an external user model, not new Default artwork. Synthetic 150% layout and silent audio fixtures do not establish physical monitor DPI, audible output or real installed-game result/audio playback.

The 2.5 resource-editor captures are in `.impeccable/review/resources-dark` and `resources-light`; native editor checks pass in `artifacts/resource-editor-dark.log` and `artifacts/resource-editor-light.log`. `artifacts/resource-render-test/result.json` and `scene-slices.png` record actual Unity Sprite creation and rendering in an isolated validation project using adapted production image sources. This establishes scene/control texture selection, slice borders, provenance, pivot/PPU and invalid/missing fallbacks within that project, not an installed IL2CPP game run. Synthetic 150% captures remain layout stress, and silent audition fixtures do not establish audible device output. Detailed formats and evidence limits are in `docs/RESOURCE_AUTHORING.md` and `.impeccable/review/resources-documentation.md`.

The 2.5.1 control evidence is in `.impeccable/review/controls-dark`, `controls-light`, `controls-animation-dark` and `controls-animation-light`. `artifacts/controls-dark.log` and `controls-light.log` pass shared numeric input and full-label geometry checks across all five manager pages and both installers at normal/minimum sizes, synthetic 125/150/200% scaling and 125% font size. Composed inspection logs additionally exercise real Tab navigation from motion selection to numeric entry and scroll bounds; animation and resource/model regression logs preserve the surrounding workflows. These checks establish API-level accessibility behavior, not screen-reader validation or physical monitor DPI. `.impeccable/review/controls-documentation.md` records the exact evidence and the persisted project/global `AGENTS.md` rules; it does not claim a change to Codex system prompts.
