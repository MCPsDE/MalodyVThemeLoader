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

In the Live2D workbench, choose an editable theme, import a model directory or ZIP, choose a discovered model, and adjust size and position. “预览与绑定动画” opens a real Cubism model and motion preview. Draw rectangular click regions, name them, assign animations and test them on the model. Regions and motion choices are manual; body parts, HitAreas and motion names do not create automatic assignments.

Bind looping idle, clicks outside assigned regions, M1–M5 result-display events and custom regions. Each animation can carry OGG Vorbis or WAV audio, shared wherever that animation is reused. Audition plays the animation with its audio; changing playback stops the previous sound. “完成绑定” validates and returns a draft to the workbench. “保存并应用模型” commits the complete binding and activates the theme. Restore removes the model replacement; concise status distinguishes unapplied edits, saved configuration and the game's reported loading result.

## Evidence

Existing native application, source artwork, catalog, packaging scripts and disposable installer tests. User requested a modern, clear, concise interface and thorough inspection. The manager defaults to dark and saves the chosen appearance through a two-position sun/moon toggle. Both installers always use light appearance and have no appearance or sponsor controls. Author credit is `by 猫猫虫小窝(451586654)`; the manager's multicolor `赞助支持` button opens `https://ifdian.net/a/mcpsde`. Live2D UI copy should describe tasks and results briefly, with detailed implementation limits kept in developer documentation. Release artifacts go directly into `artifacts/release`.

The 2.4 editor evidence is in `.impeccable/review/editor-dark` and `editor-light`, with native preview, manual-region, result-display and audio checks logged in `artifacts/editor-dark.log` and `artifacts/editor-light.log`. The preview uses the selected game's existing Cubism Core with cached Skia rendering, GPU acceleration when available and a CPU fallback. Captures show an external user model, not new Default artwork. Synthetic 150% layout and silent audio fixtures do not establish physical monitor DPI, audible output or real installed-game result/audio playback.
