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
    logs/
    themes/
      CustomTheme/
        theme.json
```

The active theme defaults to the editable `Default/` directory, generated unchanged from the source resources. If the directory is removed, virtual Default falls back to original game assets. Default can be exported as ZIP or replaced through the manager.

## 6. Theme Format

Themes are stored as directories under `ThemeLoader/themes/<id>/` for direct editing and realtime reload. ZIP is the interchange format for import and export; legacy `.mtp` archives remain readable for migration. `theme.json` is stored at
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
count, expanded size, manifest format, and theme ID before extracting an import into an editable directory. It can show
the parsed metadata and raw `theme.json`, activate a theme, export legacy
directories as `.zip`, remove imported themes, or perform a manifest-owned
uninstall.

The manager also owns `ThemeLoader/settings.json`. Its realtime-mode flag is a
mutable user setting and must not be overwritten by repair installs.

## 7. Runtime Flow

1. Doorstop loads BepInEx during the normal Unity startup.
2. BepInEx loads `MalodyVThemeLoader.Plugin.dll`.
3. The plugin validates the supported Unity/game environment.
4. The active theme and catalog are loaded.
   Editable theme directories are read directly from `ThemeLoader/themes/<id>/`. Legacy ZIP files are extracted to a hash-addressed system temporary cache only when a directory is not present.
5. Atlas lookup patches return a replacement Sprite when one exists.
6. After each allowed scene load, static UI components are scanned and
   replaced.
7. A short bounded delayed rescan catches UI instantiated immediately after
   scene start.
8. If realtime mode is enabled, the scheduler watches `ThemeLoader/settings.json`,
   `ThemeLoader/active-theme.txt`, and `ThemeLoader/themes` recursively. File changes are
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

Default font sources are included in the source-derived Default directory for
authoring. Until a compiled replacement font bundle is present, the runtime
keeps original fonts.

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

Newer Cubism Maid/Chris models are imported Unity Prefabs. Version 2.3 adds
raw Cubism model construction using the retained `CubismMoc.CreateFrom`,
`CubismModel.InstantiateFrom` and render/mask controllers in Malody 6.7.
The higher-level `CubismModel3Json.ToModel` importer is stripped from IL2CPP;
calling it from the editor SDK would give a misleading successful editor test.
`Cubism67Api.cs` binds managed delegates by retained SDK accessor method names,
so generated property aliases do not become source or runtime dependencies.

The workspace discovers `.model3.json` recursively, verifies moc3 and texture
dependencies (including references containing backslashes), and stores imports
under distinct model directories. `live2dRoot/replacement.json` binds a full
relative model path and idle/tap motion paths to `displayed-character`. Imports
validate before staging and replacing an existing model directory. External
motions are offered for assignment without modifying the game's Animator.
The original character must already have been instantiated by
`WidgetLive2dControl`; the loader finds model components under its public
container, excludes its own replacement and rejects ambiguous targets. It never selects a
character or touches ownership. It constructs and fits a sibling visual, then
hides original renderers only after successful creation. Reload/restore destroys
the replacement before releasing moc/textures and restores renderer visibility.
The existing pointer-click handler continues running; a postfix triggers only
the replacement's visual animation. No gameplay input hooks are added.

Motion3 linear, Bezier (with time inversion), stepped and inverse-stepped curves
drive parameters, part opacity and model opacity; idle loops and tap fades back
to idle. Foreign controller scripts, audio, expression controllers and physics
JSON are not executed/imported by this path. These files may remain available
for authoring, but playback does not promise their original app's behavior.
Legacy replacements still use the original Resources paths. A compatible
AssetBundle remains the extension path for complete Unity prefabs/controllers.
`ThemeLoader/live2d-status.json` records waiting/applied/error for the manager;
this output is outside the watched themes subtree to prevent reload loops.

Version 2.3.1 removes direct references to obfuscated private widget fields.
The plugin accepts `-p:GameInteropRoot=<interop-directory>` when building;
release builds use the actual target cache instead of an older host snapshot.
`test-interop-compatibility.ps1` resolves every compiled game/Unity member
reference against the actual target interop directory without running the game.
The release build runs this gate and packages that validated interop cache.
An uncaught Live2D update fault now stops that service and writes an error status
instead of logging the same failure every frame; reapplying the theme retries.
Importing a ZIP with duplicate normalized entries uses the last entry, which
matches ordinary overwrite semantics and handles Unicode Live2D archives.

A PNG/JPG replacement is copied to the selected existing texture path. A GIF is
decoded to its first frame and copied as PNG. The tool deliberately does not claim
to generate a Live2D rig: a valid rig requires model JSON, moc data, parameter
bindings and compatible runtime metadata. Adding or replacing game Animator logic
is outside the visual-only safety boundary.

## 11. Visual Scope

ThemeLoader only replaces visual resources. Never patch score, judgement, replay,
input, ownership, login or network logic. Unsupported versions fail closed.

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
the offline payload and source-derived Default directory, publishes the self-contained theme manager, embeds everything in the installer, and writes release hashes.

## 13. Existing BepInEx and unrestricted startup

The installer, manager and plugin have no agreement, device-code, allow-list or
consent checks. The CLI is available without a test-mode environment variable.

The installer checks an existing Doorstop x64 bootstrap, enabled IL2CPP entry,
CoreCLR and BepInEx 6 assemblies. It checks the plugin's assembly references and
required API types against the installed assemblies without executing them.
Compatible externally installed runtimes are reused; only ThemeLoader files
enter its install manifest. Repair retains that separation. An incompatible
unowned environment is diagnosed and left untouched. Environments previously
owned by ThemeLoader are also reused when compatible: their ownership is retained,
existing runtime files and configuration stay untouched, and missing owned files
are restored. Incompatible owned environments can be repaired from the pinned payload.

`--check <game-dir>` performs a dry-run environment check. `--install`,
`--verify` and `--uninstall` operate on the same explicit directory.

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

The installer payload contains the pinned BepInEx, plugin, catalog, self-contained
theme manager, source-derived Default directory, third-party notices and install metadata.
It excludes device authorization, agreements, repository instructions, development
documentation, debug symbols and compiler-generated XML API documentation.

## 16. Desktop design and regression checks

`PRODUCT.md` and `DESIGN.md` describe the native Windows interface. Shared colors,
typography, buttons and layout helpers live in `MalodyVThemeLoader.Windows`.
Theme previews read bounded images from ZIPs, release streams immediately, and
release old bitmaps when the selection changes. Theme switching, import/export,
search, settings and maintenance remain independent flows.

Run release scripts with PowerShell 7:

```powershell
pwsh -NoProfile -File scripts/build-release.ps1
pwsh -NoProfile -File scripts/test-installer.ps1 -InstallerDll artifacts/installer-build/bin/MalodyVThemeLoader.Installer/release_win-x64/MalodyVThemeLoader.Installer.dll
pwsh -NoProfile -File scripts/test-theme-manager.ps1
pwsh -NoProfile -File scripts/test-runtime-reuse.ps1
pwsh -NoProfile -File scripts/inspect-ui.ps1
```

See `docs/PITFALLS.md` before changing install, theme or UI code.

## 17. Desktop appearance and copy

The manager uses the full `BrandBar` and `AppAppearance` from the Windows project.
A fresh manager starts dark; its two-position `AppearanceToggle` draws a sun on
the left and a moon on the right. Click toggles, left/right keys select light/dark,
and Space/Enter retain button activation. It exposes an accessible checked state.
Colors update without rebuilding the library or changing the game theme.
The preference is an atomic JSON file at
`%LOCALAPPDATA%/MalodyVThemeLoader/appearance.json`, separate from game settings.
`MALODY_THEMELOADER_PREFERENCES` redirects only that directory for isolated tests.
Invalid or missing preferences fall back to dark. Failed writes retain the current
selection and report a save error.

The manager footer contains the exact credit and a `赞助支持` button. Its fixed
URL opens only after a click; the multicolor fill is the user's explicit exception
to the otherwise neutral Fluent surfaces. Native window themes and DWM follow the
selected appearance where supported; older systems retain native scrollbars.
Artwork is never recolored. Installers call `UseLightAppearance()` before creating
colored controls and use `BrandBar(showManagerActions: false)`. They always render
light, with credit only, and never write the manager's appearance preference.

Live2D screens show concise actions and results. Model format details stay here;
the GIF picker calls its actual behavior `GIF 首帧`. Compound motion suffixes
are matched with `EndsWith`, not `Path.GetExtension`, so `.motion3.json` and
`.mtn.bytes` appear correctly in the animation list.

Validate preference persistence in two separate processes:

```powershell
pwsh -NoProfile -File scripts/test-appearance.ps1 -Phase Write
pwsh -NoProfile -File scripts/test-appearance.ps1 -Phase Reload
pwsh -NoProfile -File scripts/test-appearance.ps1 -Phase Installers
```

Release builds use `artifacts/release` by default. They preserve older installer
executables that may still be open, overwrite the current version, and reset only
the generated Default directory. SHA256SUMS lists the current installer and README.
