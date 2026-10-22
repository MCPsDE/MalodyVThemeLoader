---
name: Malody V ThemeLoader
description: A focused Fluent 2 Windows workspace with saved manager appearance and light installers.
colors:
  accent: "#69AFED"
  accent-light: "#0F6CBD"
  accent-fill: "#0F6CBD"
  accent-hover: "#115EA3"
  accent-pressed: "#005493"
  accent-soft: "#1B3448"
  accent-soft-light: "#E5F1FB"
  danger: "#F4958E"
  danger-light: "#C43124"
  danger-fill: "#C43124"
  danger-hover: "#A4261C"
  danger-pressed: "#8B1F17"
  success: "#7ED27E"
  success-light: "#107C10"
  canvas: "#181818"
  canvas-light: "#F5F5F5"
  surface: "#202020"
  surface-light: "#FFFFFF"
  surface-alt: "#292929"
  surface-alt-light: "#FAFAFA"
  ink: "#F5F5F5"
  ink-light: "#242424"
  muted-ink: "#B8B8B8"
  muted-ink-light: "#616161"
  line: "#444444"
  line-light: "#E1E1E1"
  control-pressed: "#333333"
  control-pressed-light: "#EDEDED"
  on-fill: "#FFFFFF"
  sponsor-start: "#642BB1"
  sponsor-middle: "#A61D58"
  sponsor-end: "#18488F"
typography:
  page-title:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "22pt"
    fontWeight: 700
  selected-title:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "20pt"
    fontWeight: 700
  section-title:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "11pt"
    fontWeight: 700
  body:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "10pt"
    fontWeight: 400
  label:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "9pt"
    fontWeight: 400
  sponsor-label:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "10pt"
    fontWeight: 700
  diagnostic:
    fontFamily: "Cascadia Mono, Consolas, Courier New, monospace"
    fontSize: "9pt"
    fontWeight: 400
rounded:
  action: "4px"
spacing:
  compact: "8px"
  control: "12px"
  group: "16px"
  column: "20px"
  section: "24px"
  page: "28px"
  outer: "32px"
components:
  button-primary:
    backgroundColor: "{colors.accent-fill}"
    textColor: "{colors.on-fill}"
    typography: "{typography.body}"
    rounded: "{rounded.action}"
    height: "36px"
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
  button-primary-pressed:
    backgroundColor: "{colors.accent-pressed}"
    textColor: "{colors.on-fill}"
  button-secondary:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.action}"
    height: "36px"
  button-secondary-hover:
    backgroundColor: "{colors.surface-alt}"
  button-secondary-pressed:
    backgroundColor: "{colors.control-pressed}"
    textColor: "{colors.ink}"
  button-danger:
    backgroundColor: "{colors.danger-fill}"
    textColor: "{colors.on-fill}"
    typography: "{typography.body}"
    rounded: "{rounded.action}"
    height: "36px"
  button-danger-hover:
    backgroundColor: "{colors.danger-hover}"
  button-danger-pressed:
    backgroundColor: "{colors.danger-pressed}"
    textColor: "{colors.on-fill}"
  button-disabled:
    backgroundColor: "{colors.surface-alt}"
    textColor: "{colors.muted-ink}"
  navigation-item:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.muted-ink}"
    height: "46px"
  navigation-item-selected:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.accent}"
  theme-row:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    height: "64px"
  theme-row-selected:
    backgroundColor: "{colors.accent-soft}"
  preview:
    backgroundColor: "{colors.surface-alt}"
    textColor: "{colors.muted-ink}"
    height: "184px"
  diagnostic-log:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.muted-ink}"
    typography: "{typography.diagnostic}"
    height: "100px"
  brand-bar:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.muted-ink}"
    typography: "{typography.label}"
    padding: "10px 24px"
    height: "58px"
  brand-bar-installer:
    backgroundColor: "{colors.canvas-light}"
    textColor: "{colors.muted-ink-light}"
    typography: "{typography.label}"
    padding: "10px 24px"
    height: "40px"
  appearance-toggle:
    backgroundColor: "{colors.surface-alt}"
    textColor: "{colors.muted-ink}"
    width: "92px"
    height: "38px"
  appearance-toggle-selected:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.accent}"
  sponsor:
    textColor: "{colors.on-fill}"
    typography: "{typography.sponsor-label}"
    rounded: "{rounded.action}"
    width: "124px"
---

# Design System: Malody V ThemeLoader

## Overview

**Creative North Star: "Fluent 2 Windows utility"**

The interface is a clear, concise working environment for Chinese-speaking Malody V players and theme authors. Neutral layers, restrained blue actions and familiar Windows controls keep attention on the next task. The manager defaults to dark and saves its light or dark appearance. Both installers always use the established light palette.

The existing logo and real theme artwork carry the product identity. The manager footer adds the exact author credit, a drawn sun/moon appearance toggle and the user-requested multicolor sponsorship action. Installer footers carry the author credit alone. This document records the WinForms implementation; workflow composition remains in the surface brief.

**Key Characteristics:**

- Quiet dark and light neutral layers with restrained blue actions.
- Native Chinese typography and keyboard affordances.
- Persistent task labels and truthful operation states.
- Real source artwork with proportion-preserving previews.
- One vivid sponsorship control in the manager footer.

## Colors

The frontmatter records the implemented values in `AppVisuals`, `ActionButton`, `AppearanceToggle` and `BrandBar`. Unsuffixed mode-dependent tokens describe the manager's dark default; their `-light` counterparts describe its light option and the fixed installer appearance. Fill, hover, pressed and sponsor colors are shared across manager modes. The sun/moon control uses the existing palette.

### Primary

- **Readable Brand Blue** (`accent`): selected navigation, active-theme labels and action feedback. Its light counterpart retains the existing Fluent blue.
- **Action Blue** (`accent-fill`): primary-button backgrounds, with deeper hover and pressed fills. Filled actions keep `on-fill` text in both modes.
- **Soft Selection Blue** (`accent-soft`): selected navigation and theme rows.

### Neutral

- **Quiet Canvas** (`canvas`): main page and footer backgrounds.
- **Working Surface** (`surface`): navigation, theme library, native inputs, diagnostic logs and secondary buttons.
- **Inset Neutral** (`surface-alt`): preview ground, disabled buttons and secondary-button hover.
- **Primary Ink** (`ink`): titles and primary text.
- **Secondary Ink** (`muted-ink`): explanations, metadata and disabled controls.
- **Fine Rule** (`line`): separators and secondary-button outlines.
- **Pressed Neutral** (`control-pressed`): secondary-button pressed state.

### Feedback and sponsorship

Readable removal and success colors follow the current mode. Destructive buttons use the separate, shared `danger-fill`, hover and pressed colors with white text.

The sponsor action alone uses the three recorded sponsor colors. `SponsorButton` paints a GDI+ linear gradient at 12 degrees with stops at 0, 0.52 and 1; hover adds white at alpha 16/255 and pressed adds black at alpha 35/255. This is the explicit user-requested exception to the otherwise neutral, solid-fill interface.

**The State Rule.** Pair status color with words or an enabled, disabled or selected control state; color alone does not explain the result.

## Typography

**UI Font:** Microsoft YaHei UI, then Segoe UI Variable Text, then Segoe UI, then the system sans-serif fallback. `AppVisuals` resolves the first installed family.

**Diagnostic Font:** Cascadia Mono, then Consolas, then Courier New, then the system fallback.

Page titles use `page-title`; the selected theme uses `selected-title`. Body copy and buttons use `body`, while metadata, footer credit and the manager's “外观” label use `label`. Section headings use 11 pt, with local 12 pt maintenance headings. The navigation wordmark uses 13 pt. Theme names and selected navigation are bold. Monospace is reserved for logs and raw JSON; Live2D model and animation lists use the native body face. The sun and moon are drawn geometry, not font glyphs or emoji. No custom tracking or fixed line-height system is implemented.

**The Native Type Rule.** Keep Chinese interface text on the native UI family chain and retain point-based font sizes. System typography is the deliberate choice for this task workspace.

## Layout

Measurements are 96-DPI logical pixels except point-based typography. Forms use `AutoScaleMode.Dpi`; layout containers, docking and scroll regions accommodate resizing.

The manager has a fixed navigation width (176 px), a flexible content area and an operation-result band (58 px), above its footer (58 px). Its theme list and detail columns use a 32/68 split and a 20 px gap. The library fills its cell with single selection and vertical scrolling; details, Live2D, settings and maintenance scroll vertically. The footer has 24 px horizontal and 10 px vertical padding, flexible author-credit space, then fixed columns for the appearance label (44 px), toggle allocation (108 px, including the 16 px right margin) and sponsorship action (124 px). The toggle itself is 92 × 38 px.

The ThemeLoader installer starts at 880 × 720 client pixels with a 760 × 660 minimum window; standalone BepInEx starts at 860 × 680 with a 740 × 620 minimum. The manager starts at 1120 × 800 with a 960 × 700 minimum. Client and minimum window dimensions are distinct. Installer content scrolls independently of bottom actions and its credit-only footer (40 px), which retains the same horizontal and vertical padding.

The Live2D workbench separates a vertically scrolling editor from a fixed action-and-status area (96 px). The editor constrains its content width to the scroll viewport, imports wrap above the side-by-side model and animation lists, and labeled idle/tap selectors precede three size/position fields. Receiving focus in the theme, motion or numeric editors requests scrolling that control into view. The fixed action row has an explicit height so wrapping measurement cannot consume the editor.

The separate animation-and-region editor starts at 1140 × 760 client pixels with a 1000 × 680 minimum window. A two-column layout gives 62% to the model preview and 38% to trigger selection and assignment, separated by a 20 px margin within 24 px outer padding. Motion browsing, “试播” and “重置” sit below the preview. The right column retains a scrolling trigger list above region actions and persistently labeled fields. A fixed footer keeps concise status and the primary “完成绑定” action reachable.

Current editor captures are `editor-desktop.png`, `editor-result.png`, `editor-minimum.png` and `editor-scale150.png` under both `.impeccable/review/editor-dark` and `editor-light`. They show actual model artwork, a manually drawn region, bound audio and a selected M3 result trigger, with controls reachable at normal, minimum and synthetic 150% layouts. The earlier `live2d-dark` and `live2d-light` sets retain evidence for the surrounding manager, scrolled workbench fields and fixed-light installers. Synthetic scaling does not establish physical multi-monitor DPI behavior or assistive-technology results.

## Elevation & Depth

Neutral layers, whitespace and fine rules establish grouping without custom shadows. `WindowsBackdrop` requests Windows 11 Mica and a matching dark title-bar mode when supported; failure retains solid neutral surfaces. Bitmap captures do not verify the compositor backdrop. Native frames and dialogs retain operating-system depth. Hover and pressed feedback changes immediately; no custom animated transition is implemented. The sponsorship fill is the single local gradient exception.

## Shapes

Working panels and native fields are rectangular. Shared action and sponsor buttons use the `action` corner radius, scaled by `DeviceDpi`. The appearance toggle uses a pill track and a rounded selected half, each rounded to half its own height. Secondary actions have a fine outline; primary and destructive actions use solid fills. Separators are single logical-pixel lines. Custom button painting clears corners to the nearest opaque ancestor.

## Components

### Buttons

Shared `ActionButton` controls have primary, secondary and destructive variants. The factory height is 36 px; theme actions commonly use a 42 px minimum, installer actions 44 px and navigation 46 px. Text has a DPI-scaled horizontal drawing inset (12 px) and ellipsis when space runs out.

Pressed states follow each variant: deeper blue for primary, deeper red for destructive and pressed neutral for secondary. Disabled controls use alternate surface and muted ink. Primary and destructive text stays white while enabled. Native dotted keyboard focus is inset from the edge. Preserve accessible names and native button behavior.

### Inputs and navigation

Path and search fields remain native text boxes. A persistent “搜索主题” label identifies search independently of its name/author placeholder. Navigation contains four destinations: 主题库, Live2D, 设置 and 维护. Its selected row uses soft selection blue, readable accent text and bold weight. Native inputs, lists, combo boxes and scrollable controls receive the current Windows theme hint; platform-owned dialogs retain Windows behavior.

`ChoiceBox` retains the native ComboBox dropdown, selection, keyboard handling and accessibility. It paints the rectangular closed field with working surface, primary ink and a fine rule; focus changes the rule to readable accent and disabled text becomes secondary ink. The chevron is drawn geometry. Dropdown options use working surface and primary ink, with soft selection blue and readable accent for selection, ellipsis for long text and the native focus rectangle. Live2D theme, motion and texture choices use this component, including in palette-aware dialogs. It adds no new palette or motion treatment.

### Theme library and preview

The owner-drawn native ListBox has 64 px rows, one selected item, no horizontal scrollbar and ellipsis for long text. Each row shows a bold theme name, then active-state text or author/version. Selection uses soft blue and retains keyboard focus. Double-click applies the selected theme.

Real previews preserve aspect ratio against the alternate surface. Missing preview, invalid theme and empty search have distinct messages. With no selection, metadata is suppressed, recovery guidance appears and theme actions are disabled. “查看 theme.json” has a local minimum width (164 px); raw manifests open in a native read-only, scrollable dialog.

### Manager footer and appearance

The credit is exactly `by 猫猫虫小窝(451586654)`. Beside the “外观” label, a single icon-only button shows the sun on the left and moon on the right. Clicking anywhere on the button toggles the current mode; Left selects light and Right selects dark. Enter and native button activation toggle it. The accessible name is “暗黑模式”, the role is CheckButton, checked means dark, and its description reports the current mode and next action. Switching updates initialized manager forms and hidden pages while preserving the selected game theme and manager settings.

`AppearanceToggle` draws the sun and crescent with GDI+ geometry and anti-aliasing. Its outlined track uses alternate surface, hover uses pressed neutral, the selected half uses soft selection blue, and the selected icon uses readable accent. The other icon uses muted ink. A dotted focus rectangle appears for keyboard focus. Feedback is immediate; no sliding animation is implemented.

The choice is stored atomically in `%LocalAppData%\MalodyVThemeLoader\appearance.json`, independently of game settings. Missing or unreadable preferences default to dark. Saving succeeds before the application palette changes; failure restores the toggle and reports an error. Test runs can isolate this directory through `MALODY_THEMELOADER_PREFERENCES`.

The sponsor action is labeled exactly “赞助支持” and opens `https://ifdian.net/a/mcpsde` through the default browser. It uses a bold label, white text, the documented multicolor fill, immediate hover/pressed overlays and dotted keyboard focus. It adds no animation or artwork.

### Installer footer and appearance

Both installers initialize light appearance before creating palette-dependent controls and use `BrandBar(showManagerActions: false)`. Their footer contains only the exact author credit. It has no appearance label, sun/moon button or sponsorship action. The light override is confined to the installer process and never writes the manager's saved appearance.

### Live2D and operation feedback

“Live2D 工作台” starts with a persistently labeled editable-theme choice, then wrapping actions for importing a model directory or ZIP, importing animation and replacing texture. Recursive discovery supplies the model list; the neighboring animation list follows the selected model. A concise model summary reports its name, texture count and animation count. Legacy selections retain file replacement actions and disable the Cubism binding fields.

The workbench labels looping idle motion and tap motion that returns to idle, adds “预览与绑定动画” before the size and position percentages, and retains “保存并应用模型” as its primary fixed-footer action with “恢复游戏模型” secondary. Saving commits the model, fitting values, idle/tap choices, custom regions, result motions and per-animation audio, enables real-time reload and activates that theme. Restore removes the replacement binding. Actions are enabled according to the selected theme and model rather than presented as universally available.

The local status distinguishes unapplied edits, saved configuration, waiting for the game, loader-version guidance and a runtime loading error. A saved configuration is not presented as a loaded model: runtime feedback must match the theme and model and be no older than the binding. Detailed format limits stay in developer documentation. The texture picker describes GIF handling as “GIF 首帧”.

Installers pair directory/environment diagnosis, a read-only log, operation feedback and stable bottom actions. Busy operations disable conflicting actions and prevent closing; completed or failed operations restore valid actions. The manager reports results in its bottom band. Destructive tasks remain under maintenance and use confirmation dialogs.

### Live2D animation and region editor

Real Cubism model and motion frames fill the alternate-neutral preview ground. “框选区域” restores the default pose and changes the pointer to a crosshair; dragging shows an accent dashed rectangle and Esc cancels. Saved regions have a subtle accent fill and a visible name. The selected region uses the accent outline; other outlines use muted ink. Region selection and preview focus remain visible in both appearances. These overlays reuse the existing palette and introduce no new artwork.

The native trigger list contains “待机 · 循环”, “点击未绑定区域”, M1–M5 result choices and user-created regions. Only custom regions enable the name field and deletion. Users draw, name and assign each region manually; body parts, HitAreas and animation names never supply automatic bindings. Clicking the model tests the selected hit region, with later-added overlapping regions taking priority and uncovered clicks using the default click assignment. Result selection briefly explains playback once followed by idle.

“播放动画” uses the existing ChoiceBox. Audio appears immediately below it as an unbound prompt or a bound filename, with “添加音频” and “移除音频” actions. The picker accepts OGG Vorbis and WAV; adding audio requires an assigned animation, and removal requires an existing audio binding. Audio belongs to the animation, so reusing a motion shares its audio. “试播绑定动画” auditions motion and sound together; switching playback, resetting or closing stops the prior sound. Motion browsing below the preview remains separate from assigning a trigger.

“完成绑定” validates the edited clone and returns a draft. The workbench marks it “尚未应用” and directs the user to “保存并应用模型”; closing without completion does not adopt the edited clone. Errors stay in the concise status field, and detailed format explanations stay out of the task flow. The native preview uses the selected game's Cubism Core, cached Skia textures and an independent GPU context when available, with a lower-resolution CPU fallback; it does not use the running game window. The supplied native tests establish frame changes and audio preparation/stop, while silent fixtures and captures do not establish audible output or real-game result/audio playback.

### Artwork

Use the existing embedded logo and icon. Default theme previews and exports come from read-only source artwork. The editor captures show an external user-supplied Murasame model for validation, not artwork shipped in Default. Appearance changes add no generated or substituted artwork.

## Do's and Don'ts

### Do:

- Do use the paired appearance tokens and keep readable accent text separate from solid button fills.
- Do keep the exact author credit in all three applications and the “外观” label, sun-left/moon-right control, sponsor label and destination in the manager.
- Do keep both installers light with credit-only footers and preserve the saved manager appearance.
- Do keep labels visible and provide distinct empty, invalid, busy and disabled states.
- Do preserve keyboard focus, native behavior and Ctrl+F, Ctrl+O and F5 shortcuts.
- Do use real theme artwork and preserve its aspect ratio.
- Do keep Live2D interface copy focused on tasks and results.

### Don't:

- Don't extend the sponsor gradient into other actions or workspace surfaces.
- Don't use generated or substituted artwork for the Default theme.
- Don't style unavailable actions as enabled or expose empty metadata as a selected theme.
- Don't treat synthetic 150% captures as physical DPI verification or bitmap captures as Mica verification.
