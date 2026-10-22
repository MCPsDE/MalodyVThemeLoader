# Malody V ThemeLoader Repository Rules

Read `docs/PITFALLS.md` before making changes and update it when encountering a
repeatable issue. Desktop design conventions live in `DESIGN.md`.

## Objective

Build and ship a Windows x64 ThemeLoader for the IL2CPP release of Malody V.
The user installs it once into the game directory and then starts the original
game normally through Steam or `Malody V.exe`.

The loader must support:

- Static UI sprites and textures, including the song lobby background.
- Icons and backgrounds in other non-gameplay screens.
- Legacy Live2D model files and textures.
- Cubism Live2D textures, with an extension path for bundled replacement
  prefabs.
- Legacy Unity UI fonts and TextMesh Pro font assets.
- Missing theme files falling back to the original game assets.

## Read-Only Inputs

These directories are reference inputs and must never be modified:

- Source repository: `E:\UnityProjects\Emiria`
- Installed game: `E:\Program Files (x86)\Steam\steamapps\common\MalodyV`

Reading, hashing, static inspection, and temporary decompilation are allowed.
All generated files must go into this repository, `artifacts`, or a system
temporary directory.

## Runtime Architecture

- Target Unity version: `2022.3.62f2`, Windows x64, IL2CPP.
- Bootstrap: official BepInEx 6 IL2CPP Windows x64 build and Unity Doorstop.
- Automatic entry: proxy `winhttp.dll`, which the game's `UnityPlayer.dll`
  already imports.
- Plugin language: C#.
- Installer language: C# / .NET 8 Windows Forms.
- Installer output: one self-contained x64 executable with an embedded,
  offline payload.

Pin BepInEx to the version recorded in `docs/DEVELOPMENT_GUIDE.md`. Do not
silently replace it with a newer build.

## Safety Boundary

ThemeLoader changes presentation only. The user also authorized replacement of
the nine built-in UI effects and result-screen music. Keep this audio in a
separate source; do not hook music-library, chart, preview or hit-sound engines.
Never patch, call, alter, or bypass:

- `Malody.Utils.Guardian` or `GuardianImpl`
- `Malody.Play.ScoreSubmitter`
- score, judgement, replay, chart, input, multiplayer, purchase, entitlement,
  authentication, or network request logic

Do not change `MarloManage.IsBuy`, ownership state, or the selected character.
Only reskin a Live2D character the game has normally instantiated.

Avoid broad native detours when a game-specific managed method or scene scan is
available. Fail closed on unsupported game versions or ambiguous asset
matches. A theme failure must leave the original asset in place.

## Theme Identity Rules

Never identify an asset by basename alone. The source contains duplicate names
such as `icon_gold`, `icon_pexit`, and multiple Live2D `texture_00` files.

Use a normalized source-relative logical ID, for example:

`Textures/Malody/mainlist_bg2.png`

The bundled catalog maps that logical ID to runtime matching information:

- Asset kind
- Runtime object name
- Sprite texture name
- Scene restrictions
- Sprite border, pivot, pixels per unit, and original size
- Optional component hierarchy path

## Replacement Strategy

- Patch `Malody.Manager.UIGlobal.GetSprite` and
  `Malody.Edit.Manager.EditResource.GetSprite` for atlas lookups.
- Apply replacements to loaded `Image`, `RawImage`, `SpriteRenderer`,
  `TMP_Text`, and legacy `Text` components after scene loads.
- Process common dynamically instantiated UI through targeted factory hooks or
  a bounded delayed rescan.
- Do not scan or mutate gameplay scene objects by default.
- Preserve sprite border, pivot, pixels per unit, and material expectations.
- Do not replace network images, chart covers, user skins, or downloaded
  content.

Legacy Live2D file hooks may redirect expected `Resources` paths to a theme
file. Cubism support initially replaces textures on instantiated renderers.
Raw Cubism models may use the retained model/render factories through the
version-specific adapter, bound to an already-instantiated character. Complete
Unity prefab/controller replacement requires a compatible AssetBundle.

## Default Theme

The shipped `Default` theme must contain only assets read from the Malody
source repository. It is a validation and authoring theme: applying it should
look identical to the unmodified game.

Do not invent, generate, recolor, or substitute default-theme artwork.
Do not include unrelated source files. Include supported static UI images,
fonts, and Live2D resources plus attribution/license files already adjacent to
those assets where applicable.

## Installer Requirements

The installer must:

- Detect the Steam game path and allow manual selection.
- Validate `Malody V.exe`, `UnityPlayer.dll`, `GameAssembly.dll`, and
  `Malody V_Data`.
- Refuse to overwrite a pre-existing unowned `winhttp.dll`.
- Back up every existing file it replaces.
- Install atomically from a staging directory.
- Record installed files and hashes in an install manifest.
- Support repair and uninstall.
- Remove only files recorded as owned by this installer.
- Never launch the game automatically.

## Verification

### Shared UI constraints

- All numeric editors use the shared themed `NumberField`; no native white spinner chrome. Keep keyboard entry, arrow keys, focus indication, accessible increment/decrement and normal validation.
- Action and navigation labels stay complete on one line. Shared button sizing measures the actual font/DPI and painting insets; never conceal undersized actions with overflow ellipsis or wrapping. Dialog-opening ellipsis is intentional punctuation and may remain.
- Size the navigation rail from its actual labels with a sensible minimum. Let action rows reflow when their measured content does not fit. Filenames/paths may use ellipsis with full-value access; command names may not.
- Validate all affected pages, editors and installers in dark/light, minimum/normal sizes and synthetic 125/150/200% where practical. Numeric steppers and every action label are explicit inspection targets.
- Routine UI corrections increment only the patch version. This correction is 2.5.1.

Before delivery:

- Build all projects in Release mode.
- Verify the plugin references the IL2CPP/BepInEx API, not Unity editor DLLs at
  runtime.
- Validate every theme manifest and catalog path.
- Confirm the installer payload contains all required BepInEx files.
- Run installer dry-run tests against a disposable fake game directory.
- Run install, repair, and uninstall tests against that fake directory.
- Check that source and real game directories remain unchanged.
- Produce SHA-256 hashes for final artifacts.

