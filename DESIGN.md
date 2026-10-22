---
name: Malody V ThemeLoader
description: A focused Fluent 2 native Windows workspace with persistent dark and light appearances.
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
  sponsor:
    textColor: "{colors.on-fill}"
    typography: "{typography.sponsor-label}"
    rounded: "{rounded.action}"
    width: "124px"
---

# Design System: Malody V ThemeLoader

## Overview

**Creative North Star: "Fluent 2 Windows utility"**

The interface is a clear, concise working environment for Chinese-speaking Malody V players and theme authors. Neutral layers, restrained blue actions and familiar Windows controls keep attention on the next task. Dark mode is the default; a saved light option preserves the same hierarchy.

The existing logo and real theme artwork carry the product identity. A shared footer adds the exact author credit, appearance selector and user-requested multicolor sponsorship action. This document records the WinForms implementation; workflow composition remains in the surface brief.

**Key Characteristics:**

- Quiet dark and light neutral layers with restrained blue actions.
- Native Chinese typography and keyboard affordances.
- Persistent task labels and truthful operation states.
- Real source artwork with proportion-preserving previews.
- One vivid sponsorship control in the shared footer.

## Colors

The frontmatter records the implemented values in `AppVisuals`, `ActionButton` and `BrandBar`. Unsuffixed mode-dependent tokens describe the dark default; their `-light` counterparts describe light mode. Fill, hover, pressed and sponsor colors are shared across modes.

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

Page titles use `page-title`; the selected theme uses `selected-title`. Body copy and buttons use `body`, while metadata, footer credit and appearance labels use `label`. Section headings use 11 pt, with local 12 pt maintenance headings. The navigation wordmark uses 13 pt. Theme names and selected navigation are bold. Monospace is reserved for logs, raw JSON and animation filenames. No custom tracking or fixed line-height system is implemented.

**The Native Type Rule.** Keep Chinese interface text on the native UI family chain and retain point-based font sizes. System typography is the deliberate choice for this task workspace.

## Layout

Measurements are 96-DPI logical pixels except point-based typography. Forms use `AutoScaleMode.Dpi`; layout containers, docking and scroll regions accommodate resizing.

The manager has a fixed navigation width (176 px), a flexible content area and an operation-result band (58 px), above the separate shared footer (58 px). Its theme list and detail columns use a 32/68 split and a 20 px gap. The library fills its cell with single selection and vertical scrolling; details, Live2D, settings and maintenance scroll vertically. The footer has 24 px horizontal and 10 px vertical padding, flexible author-credit space, then fixed columns for the appearance label (44 px), selector (126 px including its margin) and sponsorship action (124 px).

The ThemeLoader installer starts at 880 × 720 client pixels with a 760 × 660 minimum window; standalone BepInEx starts at 860 × 680 with a 740 × 620 minimum. The manager starts at 1120 × 800 with a 960 × 700 minimum. Client and minimum window dimensions are distinct. Installer content scrolls independently of bottom actions and the shared footer.

Appearance captures cover normal and minimum dimensions, mode switching and restoration, and synthetic 150% layout stress. They do not establish physical multi-monitor DPI behavior.

## Elevation & Depth

Neutral layers, whitespace and fine rules establish grouping without custom shadows. `WindowsBackdrop` requests Windows 11 Mica and a matching dark title-bar mode when supported; failure retains solid neutral surfaces. Bitmap captures do not verify the compositor backdrop. Native frames and dialogs retain operating-system depth. Hover and pressed feedback changes immediately; no custom animated transition is implemented. The sponsorship fill is the single local gradient exception.

## Shapes

Working panels and native fields are rectangular. Shared action and sponsor buttons use the `action` corner radius, scaled by `DeviceDpi`. Secondary actions have a fine outline; primary and destructive actions use solid fills. Separators are single logical-pixel lines. Custom button painting clears corners to the nearest opaque ancestor.

## Components

### Buttons

Shared `ActionButton` controls have primary, secondary and destructive variants. The factory height is 36 px; theme actions commonly use a 42 px minimum, installer actions 44 px and navigation 46 px. Text has a DPI-scaled horizontal drawing inset (12 px) and ellipsis when space runs out.

Pressed states follow each variant: deeper blue for primary, deeper red for destructive and pressed neutral for secondary. Disabled controls use alternate surface and muted ink. Primary and destructive text stays white while enabled. Native dotted keyboard focus is inset from the edge. Preserve accessible names and native button behavior.

### Inputs and navigation

Path and search fields remain native text boxes. A persistent “搜索主题” label identifies search independently of its name/author placeholder. Navigation contains four destinations: 主题库, Live2D, 设置 and 维护. Its selected row uses soft selection blue, readable accent text and bold weight. Native inputs, lists, combo boxes and scrollable controls receive the current Windows theme hint; platform-owned dialogs retain Windows behavior.

### Theme library and preview

The owner-drawn native ListBox has 64 px rows, one selected item, no horizontal scrollbar and ellipsis for long text. Each row shows a bold theme name, then active-state text or author/version. Selection uses soft blue and retains keyboard focus. Double-click applies the selected theme.

Real previews preserve aspect ratio against the alternate surface. Missing preview, invalid theme and empty search have distinct messages. With no selection, metadata is suppressed, recovery guidance appears and theme actions are disabled. “查看 theme.json” has a local minimum width (164 px); raw manifests open in a native read-only, scrollable dialog.

### Shared footer and appearance

The credit is exactly `by 猫猫虫小窝(451586654)`. The “外观” selector offers “暗黑模式” and “亮色模式”; its accessible name is “外观模式”. Switching updates initialized open forms and hidden pages while preserving the selected game theme and manager settings.

The choice is stored atomically in `%LocalAppData%\MalodyVThemeLoader\appearance.json`, independently of game settings. Missing or unreadable preferences default to dark. Saving succeeds before the UI changes; failure restores the selector and reports an error. Test runs can isolate this directory through `MALODY_THEMELOADER_PREFERENCES`.

The sponsor action is labeled exactly “赞助支持” and opens `https://ifdian.net/a/mcpsde` through the default browser. It uses a bold label, white text, the documented multicolor fill, immediate hover/pressed overlays and dotted keyboard focus. It adds no animation or artwork.

### Live2D and operation feedback

The Live2D page uses concise task copy: “管理当前主题的模型、动画和纹理。” Model and animation lists sit above wrapping import and replacement actions. Status text reports selection, completion or errors; detailed format limits stay in developer documentation. The image picker describes GIF handling as “GIF 首帧”.

Installers pair directory/environment diagnosis, a read-only log, operation feedback and stable bottom actions. Busy operations disable conflicting actions and prevent closing; completed or failed operations restore valid actions. The manager reports results in its bottom band. Destructive tasks remain under maintenance and use confirmation dialogs.

### Artwork

Use the existing embedded logo and icon. Default theme previews and exports come from read-only source artwork. Appearance changes add no generated or substituted artwork.

## Do's and Don'ts

### Do:

- Do use the paired appearance tokens and keep readable accent text separate from solid button fills.
- Do keep the exact author credit, appearance labels, sponsor label and destination.
- Do keep labels visible and provide distinct empty, invalid, busy and disabled states.
- Do preserve keyboard focus, native behavior and Ctrl+F, Ctrl+O and F5 shortcuts.
- Do use real theme artwork and preserve its aspect ratio.
- Do keep Live2D interface copy focused on tasks and results.

### Don't:

- Don't extend the sponsor gradient into other actions or workspace surfaces.
- Don't use generated or substituted artwork for the Default theme.
- Don't style unavailable actions as enabled or expose empty metadata as a selected theme.
- Don't treat synthetic 150% captures as physical DPI verification or bitmap captures as Mica verification.
