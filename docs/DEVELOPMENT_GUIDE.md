# Malody V ThemeLoader Development Guide

## 1. Scope

Malody V ThemeLoader is an install-once visual mod for the Windows x64 IL2CPP
build of Malody V. It does not modify or repack official game assets. After
installation, the user launches the original game normally.

Reference inputs:

- Source: `E:\UnityProjects\Emiria`
- Game: `E:\Program Files (x86)\Steam\steamapps\common\MalodyV`

Both inputs are read-only.

## 2. Confirmed Game Layout

- Unity player: `2022.3.62f2`
- Architecture: Windows x64
- Scripting backend: IL2CPP (`GameAssembly.dll` and
  `Malody V_Data\il2cpp_data`)
- Automatic bootstrap candidate: `WINHTTP.dll` imported by `UnityPlayer.dll`
- Main global UI resources:
  `Malody.Manager.UIGlobal`
- Editor UI atlas:
  `Malody.Edit.Manager.EditResource`
- Main song lobby background object:
  `bgCanvas/bg`
- Main song lobby background source:
  `Assets/Malody/Textures/Malody/mainlist_bg2.png`

The source contains about 1,400 game image files and 13 SpriteAtlas assets.
The main `ui` atlas references about 557 sprites and `ui_edit` about 177.

## 3. Pinned Dependencies

- BepInEx Unity IL2CPP Windows x64: `6.0.0-be.785`
- BepInEx archive SHA-256:
  `2a7cbf74d26abe4765c3e662db1721b923bac39849ebfef2ca5dc7de7e2d9b7f`
- BepInEx plugin template: `2.0.0-be.4`
- Plugin target framework: `net6.0`
- Installer target: `.NET 8`, Windows x64, self-contained, single-file

The BepInEx payload is embedded in the installer. Installation does not
download runtime dependencies.

## 4. Repository Layout

```text
AGENTS.md
docs/
  DEVELOPMENT_GUIDE.md
src/
  MalodyVThemeLoader.Plugin/
  MalodyVThemeLoader.Shared/
  MalodyVThemeLoader.ThemeBuilder/
  MalodyVThemeLoader.Installer/
  MalodyVThemeLoader.ThemeManager/
  MalodyVThemeLoader.Authorizer/
  MalodyVThemeLoader.Windows/
vendor/
  bepinex/
generated/
  game-libs/
artifacts/
  payload/
  release/
```

`vendor`, `generated`, and `artifacts` are reproducible build outputs and are
not source-of-truth code.

## 5. Installed Layout

```text
Malody V/
  winhttp.dll
  doorstop_config.ini
  .doorstop_version
  BepInEx/
    core/
    plugins/
      MalodyVThemeLoader/
        MalodyVThemeLoader.Plugin.dll
        catalog.json
  ThemeLoader/
    active-theme.txt
    settings.json
    install-manifest.json
    MalodyVThemeManager.exe
    authorization/
      consent.json
    logs/
    themes/
      CustomTheme.mtp
```

The active theme defaults to the virtual `Default`, which means "use original
game assets" and does not require or install a theme package.

## 6. Theme Format

Themes are ZIP archives with the `.mtp` extension. `theme.json` is stored at
the archive root:

```json
{
  "formatVersion": 1,
  "id": "default",
  "name": "Default",
  "version": "1.0.0",
  "author": "",
  "description": "Original Malody V visual assets.",
  "gameVersion": "6.7",
  "website": "",
  "tags": [],
  "imageRoot": "images",
  "fontRoot": "fonts",
  "live2dRoot": "live2d",
  "fontMappings": {}
}
```

Images use normalized logical paths under `images` inside the archive,
matching their path below `Assets/Malody`:

```text
images/Textures/Malody/mainlist_bg2.png
images/Textures/Malody/icon/icon_main_setting.png
```

Looping video replacements use the same logical ID with a video extension:

```text
images/Textures/Malody/mainlist_bg2.mp4
images/Textures/Malody/mainlist_bg2.webm
images/Textures/Malody/mainlist_bg2.ogv
```

When both a static image and a video exist for the same logical ID, the video
is applied to eligible UI/background components and loops without audio. Static
image replacement remains the fallback path. Video files are intended for visual
backgrounds and full-rect UI art; sliced or material-sensitive UI should keep a
static image replacement unless it has been verified in game.

Missing files are not errors. The original game asset remains assigned.

The desktop theme manager validates archive paths, duplicate entries, entry
count, expanded size, manifest format, and theme ID before import. It can show
the parsed metadata and raw `theme.json`, activate a theme, export legacy
directories as `.mtp`, remove imported themes, or perform a manifest-owned
uninstall.

The manager also owns `ThemeLoader/settings.json`. Its realtime-mode flag is a
mutable user setting and must not be overwritten by repair installs.

## 7. Runtime Flow

1. Doorstop loads BepInEx during the normal Unity startup.
2. BepInEx loads `MalodyVThemeLoader.Plugin.dll`.
3. The plugin validates the supported Unity/game environment.
4. The active theme and catalog are loaded.
   `.mtp` packages are extracted to a hash-addressed system temporary cache.
5. Atlas lookup patches return a replacement Sprite when one exists.
6. After each allowed scene load, static UI components are scanned and
   replaced.
7. A short bounded delayed rescan catches UI instantiated immediately after
   scene start.
8. If realtime mode is enabled, the scheduler watches `ThemeLoader/settings.json`,
   `ThemeLoader/active-theme.txt`, and `ThemeLoader/themes`. File changes are
   debounced, the current visual replacements are restored, and the active theme
   is rebuilt on the Unity main thread before the current allowed scene is
   rescanned.
9. Legacy Live2D load methods redirect to theme files when present.
10. Cubism renderers receive replacement textures after their prefab is
   instantiated.

Gameplay scene scanning is disabled by default.

## 8. Images

Replacement Sprites preserve:

- Original border
- Original pivot
- Original pixels per unit
- Original mesh type where practical
- Original name for compatibility

The catalog uses source-relative IDs. Runtime matching uses a combination of
Sprite name, source texture name, dimensions, scene name, and optional
hierarchy path.

Network images, chart covers, user skins, screenshots, and downloaded content
are outside the catalog.

## 8.1 Dynamic Videos

Dynamic visual replacements use Unity `VideoPlayer` and `RenderTexture`, with
audio disabled and looping enabled. Supported theme-side extensions are:

- `.mp4`
- `.webm`
- `.ogv`

The runtime resolves video files by catalog logical ID, never by basename
alone. For example, `Textures/Malody/mainlist_bg2.png` can be replaced by
`images/Textures/Malody/mainlist_bg2.mp4`.

`Image` components receive a temporary `RawImage` video layer on the same
object while the original `Image` is disabled. `RawImage` components receive
the video render texture directly. `SpriteRenderer` components receive the
video render texture through their material. On realtime reload or failure,
the registry restores original sprites, textures, fonts, Cubism textures, and
video bindings before applying the next theme.

## 9. Fonts

TextMesh Pro fonts contain SDF atlas and material data and cannot be treated as
plain image files.

The robust path is a theme-specific AssetBundle created by the ThemeBuilder
with a compatible Unity editor. The runtime maps original font asset names to
replacement `TMP_FontAsset` objects.

Default font sources may be generated locally for authoring and validation,
but are not included in the release payload. Until a compiled replacement font
bundle is present, the runtime keeps original fonts.

## 10. Live2D

Legacy models use centralized `Resources` access through
`Malody.Live2d.FileManager` and `PlatformManager`. Theme files can replace:

- Model JSON
- MOC bytes
- Motion bytes
- Expression JSON
- Physics JSON
- Textures

The logical directory structure must mirror `Assets/Malody/Resources/Live2d`.

Newer Cubism Maid/Chris models are imported Unity Prefabs. The first runtime
implementation replaces their textures only. Full model and Animator
replacement uses a compatible AssetBundle and must not bypass ownership.

## 11. Anti-Cheat Boundary

The inspected source uses `GuardianImpl` for score submission signing. No
explicit module blacklist or ban-reporting path was identified, but an
in-process loader is detectable in principle.

ThemeLoader must never patch score, judgement, replay, input, ownership, login,
or networking code. Version mismatches fail closed. The project cannot promise
zero account risk without explicit approval from the game developer.

## 12. Building

High-level build:

```powershell
.\scripts\prepare-dependencies.ps1 `
  -GameRoot 'E:\Program Files (x86)\Steam\steamapps\common\MalodyV'

dotnet build MalodyVThemeLoader.slnx -c Release

.\scripts\build-release.ps1 `
  -SourceRoot 'E:\UnityProjects\Emiria'
```

The preparation script downloads the pinned BepInEx archive, verifies its
SHA-256, creates an isolated shadow game directory, runs only long enough to
generate the real IL2CPP interop assemblies, and copies compile-time references
into `generated/game-libs`. It does not modify the real game directory.

The release script generates only the lightweight runtime catalog, assembles
the offline payload, publishes the self-contained theme manager, embeds
everything in the installer, and writes release hashes. It does not package
the local Default authoring theme or original game resources.

## 13. Device Authorization And Agreement

The installer, theme manager, and runtime plugin all validate the current
device against the allow list embedded from
`src/MalodyVThemeLoader.Shared/allowed-devices.json`.

The disposable CLI authorizer is rebuilt with:

```powershell
.\scripts\build-authorizer.ps1
```

It writes the device code, authorization value, and a salted PBKDF2 allow-list
entry to `authorization-value.txt`, then schedules deletion of only its own
executable. See `docs/AUTHORIZATION.md`.

The installer requires three agreement passes. Each installer pass waits
60 seconds and requires scrolling to the end. Every theme-manager launch also
shows three agreement passes, but those passes have no delay and can be
advanced immediately. There is no separate checkbox confirmation. A signed
device-bound consent receipt is written into the installation. The plugin fails
closed before installing any patches if either device authorization or consent
validation fails.

This is a local deterrent against casual copying. A determined user who
controls the machine can patch client-side checks; the mechanism must never be
described as unbreakable or as a substitute for permission from rights
holders.

## 14. Installer Testing

Installer tests use a disposable directory containing placeholder copies of:

- `Malody V.exe`
- `UnityPlayer.dll`
- `GameAssembly.dll`
- `Malody V_Data`

Required test cases:

- Fresh install
- Repair after deleting an owned file
- Upgrade over an older owned installation
- Refusal when an unowned `winhttp.dll` exists
- Uninstall preserving unrelated files
- Failed install rollback

Do not use the real game directory for automated tests.

## 15. Release Contents

The final release contains:

- Self-contained installer executable
- SHA-256 checksum file
- User-facing `README.txt`, including required third-party notices

The installer payload contains BepInEx, the plugin, catalog, self-contained
theme manager, terms, notices, and install metadata. It does not contain a
Default theme package, original game resources, the authorizer, repository
instructions, development documentation, debug symbols, build-machine paths,
or compiler-generated XML API documentation.
