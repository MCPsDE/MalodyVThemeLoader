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
  number-field:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    height: "32px"
  number-field-disabled:
    backgroundColor: "{colors.surface-alt}"
    textColor: "{colors.muted-ink}"
  text-field:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    height: "34px"
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

The manager sizes its navigation rail from the actual preferred widths of the wordmark, subtitle, version and all destination buttons, including margins and panel padding, with a DPI-scaled floor (208 px). Those labels stay on one line. A flexible content area and operation-result band (58 px) sit above its footer (58 px). Its theme list and detail columns use a 32/68 split and a 20 px gap. The library fills its cell with single selection and vertical scrolling; details, Live2D, settings and maintenance scroll vertically. The footer has 24 px horizontal and 10 px vertical padding, flexible author-credit space, then fixed columns for the appearance label (44 px), toggle allocation (108 px, including the 16 px right margin) and sponsorship action (124 px). The toggle itself is 92 × 38 px.

Shared `ButtonRow` compares the complete measured button widths plus its DPI-scaled gap (8 px) with the available width, then stacks actions when needed. `FieldGrid` gives numeric fields equal-width columns with separate gap tracks (10 px) and reduces the column count before their preferred input widths stop fitting. Content growth and reflow preserve the normal font size and complete labels.

The ThemeLoader installer starts at 880 × 720 client pixels with a 760 × 660 minimum window; standalone BepInEx starts at 860 × 680 with a 740 × 620 minimum. The manager starts at 1120 × 800 with a 960 × 700 minimum. Client and minimum window dimensions are distinct. Installer content scrolls independently of bottom actions and its credit-only footer (40 px), which retains the same horizontal and vertical padding.

The Live2D workbench separates a vertically scrolling editor from a fixed action-and-status area (96 px). The editor constrains its content width to the scroll viewport, imports wrap above the side-by-side model and animation lists, and labeled idle/tap selectors precede three size/position fields. Receiving focus in the theme, motion or numeric editors requests scrolling that control into view. The fixed action row has an explicit height so wrapping measurement cannot consume the editor.

The separate animation-and-region editor starts at 1140 × 760 client pixels with a 1000 × 680 minimum window. A two-column layout gives 62% to the model preview and 38% to trigger selection and assignment, separated by a 20 px margin within 24 px outer padding. Motion browsing, “试播” and “重置” sit below the preview. The right column retains a scrolling trigger list above region actions and persistently labeled fields. A fixed footer keeps concise status and the primary “完成绑定” action reachable.

The “资源编辑” page retains the manager shell and its labeled theme selector, then a compact “资源文件” / “在线调试” mode row. Resource-file mode keeps the three-column work area (24/44/32%). The left column holds labeled search and the image/UI-audio tree; the center holds source/stretch or waveform preview above a source-usage list; the right holds scope, import, playback and slice fields. The right fields scroll independently above a fixed save allocation (54 px), so the primary action remains reachable at minimum size and synthetic scaling. Hierarchy, component and numeric field focus requests scroll-into-view. Native tree scrolling, clipped names with ellipsis and full logical-path tooltips accommodate long resource paths.

Online mode places connection, selected-bounds, node-name and hidden-node checkboxes above its concise current-scene connection status. The live tree contains only the current game scene. The game overlay draws one selected 4 px yellow frame and one 16 px white name on a dark opaque plate, padded 8/4 px and clamped inside the screen. The work area splits 40/60 between a labeled searchable live tree and independently scrolling node fields, separated by a 16 px margin. Node title and scene/component/hierarchy precede the resource-location action, background actions and preview, video actions, opacity, fit, size and slice fields. The existing `ButtonRow` and `FieldGrid` provide action and numeric reflow. Save/restore sit below the scrolling fields, followed by a fixed status allocation (46 px). Focus entering editing controls requests scroll-into-view. Resource-file settings begin with “已保存的替换”, the shared “新增场景替换” button, and the existing scope chooser; full variable scope text remains available in a tooltip. This extends the existing workspace without changing its palette, type, navigation or footer.

Current editor captures are `editor-desktop.png`, `editor-result.png`, `editor-minimum.png` and `editor-scale150.png` under both `.impeccable/review/editor-dark` and `editor-light`. They show actual model artwork, a manually drawn region, bound audio and a selected M3 result trigger, with controls reachable at normal, minimum and synthetic 150% layouts. The earlier `live2d-dark` and `live2d-light` sets retain evidence for the surrounding manager, scrolled workbench fields and fixed-light installers. Synthetic scaling does not establish physical multi-monitor DPI behavior or assistive-technology results.

Resource-editor captures are `resources-images.png`, `resources-minimum.png`, `resources-audio.png`, `resources-scale150.png` and `resources-control.png` in both `.impeccable/review/resources-dark` and `resources-light`. They show existing source artwork, slice editing, a loaded exact-control scope and silent audio fixtures. The documentation evidence and Unity-versus-installed-game limits are recorded in `.impeccable/review/resources-documentation.md`.

The 2.5.1 correction captures are under `.impeccable/review/controls-dark`, `controls-light`, `controls-animation-dark` and `controls-animation-light`. Shared geometric checks cover all five manager pages and both fixed-light installers at normal/minimum sizes, synthetic 125/150/200% and 125% font size; composed inspection checks real Tab entry and scroll bounds. The exact capture list, logs and evidence limits are in `.impeccable/review/controls-documentation.md`. Synthetic scaling remains layout stress; API-level accessibility checks do not establish screen-reader behavior.

The 2.6 captures under `.impeccable/review/inspector-dark` and `inspector-light` each cover disconnected, node draft, saved media, picked resource, video settings, minimum, minimum-video and synthetic 150% layouts. The isolated Unity `inspector-runtime` pair records functional debug geometry and actual alpha-video layering using synthetic validation art. Native editor, resource-regression, Release build, 311 resolved game/Unity references and 28 runtime checks are recorded in `.impeccable/review/inspector-documentation.md`; physical input delivery, physical DPI, screen-reader use and the installed IL2CPP game remain outside that evidence.

The 2.6.1 overlay-only evidence is in `.impeccable/review/overlay-2.6.1`. Its `overlay-presented.png` records the final `ScreenSpaceOverlay` frame in a standalone Windows Mono test player, with bounds and name-glyph pixel assertions; `hierarchy.png` and `node-video.png` retain the geometry and media-layer views. All 33 checks pass, including font loss/recovery and preservation of borrowed game fonts. The private test desktop is never activated onto the user's desktop. The existing manager/installer capture matrix remains the layout evidence; this correction changes runtime font handling only. The adapted test player and stronger static exception-stub check do not establish an installed IL2CPP game run. Exact sources, logs and limits are in `.impeccable/review/overlay-documentation.md`.

## Elevation & Depth

Neutral layers, whitespace and fine rules establish grouping without custom shadows. `WindowsBackdrop` requests Windows 11 Mica and a matching dark title-bar mode when supported; failure retains solid neutral surfaces. Bitmap captures do not verify the compositor backdrop. Native frames and dialogs retain operating-system depth. Hover and pressed feedback changes immediately; no custom animated transition is implemented. The sponsorship fill is the single local gradient exception.

## Shapes

Working panels and native fields are rectangular. Shared action and sponsor buttons use the `action` corner radius, scaled by `DeviceDpi`. The appearance toggle uses a pill track and a rounded selected half, each rounded to half its own height. Secondary actions have a fine outline; primary and destructive actions use solid fills. Separators are single logical-pixel lines. Custom button painting clears corners to the nearest opaque ancestor.

## Components

### Buttons

Shared `ActionButton` controls have primary, secondary and destructive variants. The factory height is 36 px; theme actions commonly use a 42 px minimum, installer actions 44 px and navigation 46 px. Preferred and minimum sizes measure the actual localized font at the current DPI, adding horizontal drawing allowance (26 px) and the control's explicit padding. Painting uses horizontal insets (12 px), a single text line and no overflow ellipsis. Text, font, padding and DPI changes refresh the content minimum.

Pressed states follow each variant: deeper blue for primary, deeper red for destructive and pressed neutral for secondary. Disabled controls use alternate surface and muted ink. Primary and destructive text stays white while enabled. Native dotted keyboard focus is inset from the edge. Preserve accessible names and native button behavior.

**The Complete Label Rule.** Action and navigation labels stay complete on one line. Measure their actual font, DPI and painting insets, then grow or reflow containers; never hide an undersized command with wrapping, clipping, smaller text or overflow ellipsis. Intentional dialog-opening ellipsis may remain.

### Inputs and navigation

Text entry retains native text boxes. Resource search, hierarchy entry and online node search use the shared `TextField` frame, which removes the inner native border and paints the working surface, primary ink and fine outer rule. Focus changes that rule to readable accent. Its minimum height starts at 34 logical pixels and grows with the editor's preferred height plus 12 px; the native editor is vertically centered with 7 px horizontal insets. Font and DPI changes recalculate the frame while native typing, selection and keyboard behavior remain in the editor. Other path and theme-search inputs retain their existing native treatment. Persistent “搜索主题”, “搜索资源” and “搜索节点名称或路径” labels identify search independently of placeholders.

Navigation contains five destinations: 主题库, 资源编辑, Live2D, 设置 and 维护. Its selected row uses soft selection blue, readable accent text and bold weight. `SingleLineLabel` measures and paints the rail's wordmark, subtitle and version without wrapping. Native inputs, lists, combo boxes and scrollable controls receive the current Windows theme hint; platform-owned dialogs retain Windows behavior.

`NumberField` is the shared numeric editor for Live2D fitting and resource slice/preview dimensions. Its preferred height starts at 32 logical pixels and grows with the font. It retains native parsing and arrow-key input inside a clipped host, hiding the native spinner buttons and reserving their actual internal width outside the visible input. Two theme-painted horizontal minus/plus buttons use the working surface, hover/pressed neutrals and primary ink; disabled steps use alternate surface and muted ink. Focus changes the outer rule to readable accent. Range, read-only and disabled states govern stepping. The outer Tab stop forwards focus to the editor; named increment/decrement controls and a writable SpinButton accessible value remain available. Layout measures range samples rather than reading `Value`, preserving incomplete input until an explicit commit.

**The Themed Number Rule.** All numeric editors use the shared themed control without exposed native white spinner chrome. Preserve typing, arrow keys, focus, accessible stepping, validation, range and disabled behavior.

`ChoiceBox` retains the native ComboBox dropdown, selection, keyboard handling and accessibility. It paints the rectangular closed field with working surface, primary ink and a fine rule; focus changes the rule to readable accent and disabled text becomes secondary ink. The chevron is drawn geometry. Dropdown options use working surface and primary ink, with soft selection blue and readable accent for selection, ellipsis for long text and the native focus rectangle. Live2D theme, motion and texture choices use this component, including in palette-aware dialogs. It adds no new palette or motion treatment.

### Theme library and preview

The owner-drawn native ListBox has 64 px rows, one selected item, no horizontal scrollbar and ellipsis for long text. Each row shows a bold theme name, then active-state text or author/version. Selection uses soft blue and retains keyboard focus. Double-click applies the selected theme.

Real previews preserve aspect ratio against the alternate surface. Missing preview, invalid theme and empty search have distinct messages. With no selection, metadata is suppressed, recovery guidance appears and theme actions are disabled. “查看 theme.json” has a local minimum width (164 px); raw manifests open in a native read-only, scrollable dialog.

### Resource tree, preview and editing

The resource tree groups images by their logical directory paths and lists UI audio by event, keeping separately editable events even when they share a source file. It retains native hierarchy and keyboard selection, uses ellipsis instead of horizontal scrolling, and gives logical paths in tooltips. The selected resource name and import status preserve the author-facing filename; hash-based storage paths remain an implementation detail.

Image previews fit the source and chosen stretch size into their available areas. Both image areas use a fixed mid-gray transparency checker (local rendering values `#666666` and `#808080`, 8 px tiles in a 16 px pattern) in either appearance. This functional alpha backdrop keeps pale borders visible without recoloring the artwork or changing the application palette. Dashed slice guides use the existing accent; four labeled `NumberField` controls provide the same pixel-margin edits as dragging. Known RawImage targets hide slice controls. Audio previews use the alternate surface, a fine-rule baseline, an accent waveform and duration/sample-rate/channel metadata, with explicit “试听” and “停止” actions.

Persistently labeled scene, hierarchy and component fields establish the loaded image scope. Double-clicking a source usage reads it directly. Manually changing hierarchy or component exposes “读取此范围” and “取消定位”; import, slice editing and save stay disabled until the requested scope is read. Reading reloads that scope's exact or inherited file, mode, margins and preview after resolving any resource draft. Cancelling returns to the loaded scope while preserving its draft. Source usages are a partial authoring aid, not a full scene preview.

The fixed “保存并实时应用” action is enabled when a selected resource has edits and its scope is loaded. Busy operations disable conflicting controls; save-time validation errors retain edits and give concise corrective feedback. “恢复此范围” is enabled only for an exact saved override and removes that override to restore inheritance. Clean same-theme refreshes reload persisted rules; dirty edits remain local. Applying a materialized ZIP refreshes the library's directory identity. Saved/activated status does not certify that the installed game has rendered or played the replacement.

### Online scene inspector and node media

The mode row reuses shared action buttons; the selected mode uses soft selection blue and readable accent. A disconnected inspector shows the next connection/picking action and disables media editing until a suitable node and editable theme exist. Connection feedback distinguishes waiting, connected theme/node count, competing manager and an incomplete hierarchy. The tree preserves native expansion and selection, marks hidden nodes with “隐藏” and muted ink, and searches names and hierarchy while retaining ancestors. Runtime debug bounds and names are functional solid-color geometry; yellow marks the selected node. Bounds and names can be toggled independently without introducing new application-chrome tokens.

Runtime labels borrow a retained game or built-in font. If none is available, bounds continue while names are omitted and font lookup retries; recovered fonts restore labels. Inspector disposal releases its own geometry and material while preserving borrowed font assets. This fallback preserves the established overlay colors and label treatment without adding a font or token to the desktop visual system.

Tree selection and game picking address the same node. Middle-click selects a visible UI region and repeated clicks cycle overlaps; a catalog-matched image opens its resource scope with a saved exact-node address. UI containers without an image can add a background and VP8 alpha WebM. Nodes without a UI region remain in the tree with a concise explanation and disabled decoration controls. Long node names, paths and media names use ellipsis only with full values available in tooltips; commands retain the complete-label rule.

A transparent loader-owned UI hit layer receives inspection middle-clicks and the short release window, keeping game panels from treating a pick as a close action. It becomes transparent to UI raycasts after capture, so ordinary left-click interaction resumes. This adds no visible control or application-chrome token. The isolated Unity validation checks actual `EventSystem.RaycastAll` routing to this layer and subsequent routing to the original UI; physical mouse delivery and installed-game input remain separate verification limits.

The background preview reuses the existing source/stretch and slice-guide language. Opacity, width, height and margins use `NumberField`; fit uses `ChoiceBox`. Persistent labels state percent, pixels and “0 为自动” where relevant. VP9 import reports a VP8 conversion action and preserves the prior draft. Video has no desktop motion preview or audio playback; the inspector reports the game's decoration state. A preparing or failed video keeps the background visible, and saved layers follow the target while preserving its existing foreground content. These are media effects in the game, not animated application chrome.

Save commits a validated node draft and enables live application; restore removes only the selected node decoration. Unchanged state, no selection, invalid UI region and busy work govern action availability. Leaving an unsaved draft requires a discard choice, and failed validation retains edits. Save/activation feedback remains distinct from game-reported applied, preparing or failed status. Closing the leased connection clears debug geometry while saved decorations persist. Exact identity, codec and transport details belong in `docs/SCENE_INSPECTOR.md`, outside the task flow.

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

Use the existing embedded logo and icon. Default theme previews and exports come from read-only source artwork. The editor captures show an external user-supplied Murasame model for validation, not artwork shipped in Default. The online-inspector background/video and runtime geometry captures use synthetic validation fixtures only; those images and videos are not new shipping artwork. Appearance changes add no generated or substituted artwork.

## Do's and Don'ts

### Do:

- Do use the paired appearance tokens and keep readable accent text separate from solid button fills.
- Do keep the exact author credit in all three applications and the “外观” label, sun-left/moon-right control, sponsor label and destination in the manager.
- Do keep both installers light with credit-only footers and preserve the saved manager appearance.
- Do keep labels visible and provide distinct empty, invalid, busy and disabled states.
- Do preserve keyboard focus, native behavior and Ctrl+F, Ctrl+O and F5 shortcuts.
- Do use real theme artwork and preserve its aspect ratio.
- Do keep Live2D interface copy focused on tasks and results.
- Do use shared numeric, single-line text, button measurement and reflow helpers; inspect CJK labels in both appearances, normal/minimum sizes and relevant synthetic font/scaling cases.
- Do keep full filenames and paths available when their variable display text uses ellipsis.

### Don't:

- Don't extend the sponsor gradient into other actions or workspace surfaces.
- Don't use generated or substituted artwork for the Default theme.
- Don't style unavailable actions as enabled or expose empty metadata as a selected theme.
- Don't treat synthetic 150% captures as physical DPI verification or bitmap captures as Mica verification.
- Don't expose native white numeric spinner chrome or hide action/navigation text with wrapping, clipping, smaller text or overflow ellipsis.
