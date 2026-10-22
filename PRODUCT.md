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

## Evidence

Existing native application, source artwork, catalog, packaging scripts and disposable installer tests. User requested a modern, clear, concise interface and thorough inspection. Dark mode is the confirmed default, with a persistent light mode option. Shared author credit is `by 猫猫虫小窝(451586654)`; the multicolor `赞助支持` button opens `https://ifdian.net/a/mcpsde`. Live2D UI copy should describe tasks and results briefly, with detailed implementation limits kept in developer documentation.
