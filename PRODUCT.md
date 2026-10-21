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

Existing native application, source artwork, catalog, packaging scripts and disposable installer tests. User requested a modern, clear, concise interface and thorough inspection. The light desktop layout is the implementation default pending optional user preference.
