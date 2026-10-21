---
name: Malody V ThemeLoader
description: A focused Fluent 2 native Windows workspace for installing and managing Malody V themes.
colors:
  accent: "#0F6CBD"
  accent-hover: "#115EA3"
  accent-pressed: "#005493"
  accent-soft: "#E5F1FB"
  danger: "#C43124"
  danger-hover: "#A4261C"
  danger-pressed: "#8B1F17"
  success: "#107C10"
  canvas: "#F5F5F5"
  surface: "#FFFFFF"
  surface-alt: "#FAFAFA"
  ink: "#242424"
  muted-ink: "#616161"
  line: "#E1E1E1"
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
    backgroundColor: "{colors.accent}"
    textColor: "{colors.surface}"
    typography: "{typography.body}"
    rounded: "{rounded.action}"
    height: "36px"
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
  button-secondary:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.action}"
    height: "36px"
  button-secondary-hover:
    backgroundColor: "{colors.surface-alt}"
  button-danger:
    backgroundColor: "{colors.danger}"
    textColor: "{colors.surface}"
    typography: "{typography.body}"
    rounded: "{rounded.action}"
    height: "36px"
  button-danger-hover:
    backgroundColor: "{colors.danger-hover}"
  button-pressed:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.ink}"
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
---

# Design System: Malody V ThemeLoader

## Overview

**Creative North Star: "Fluent 2 Windows utility"**

The interface is a clear, concise working environment for Chinese-speaking Malody V players and theme authors. Neutral surfaces, restrained brand blue actions and familiar Windows controls keep attention on the next task. Brand appears through the existing logo and real theme artwork.

The native Windows typography, frame, dialogs, scrollbars and focus cues are intentional. This system records the implemented WinForms application; the surface brief owns its workflow composition and the disclosed concept-workflow deviation.

**Key Characteristics:**

- Quiet light surfaces with a restrained brand blue accent.
- Native Chinese typography and keyboard affordances.
- Persistent task labels and truthful operation states.
- Real source artwork with proportion-preserving previews.

## Colors

Fluent brand blue provides action and selection within a pale neutral workspace. The frontmatter is the token record extracted from `AppVisuals` and `ActionButton`.

### Primary

- **Brand Blue** (`accent`): primary actions, active-theme text and successful ThemeLoader operations.
- **Deep Brand Blue** (`accent-hover`): primary-button hover.
- **Soft Brand Blue** (`accent-soft`): selected navigation and list rows; pressed buttons.
- **Removal Red** (`danger`): destructive ThemeLoader removal and invalid-theme feedback.
- **Deep Removal Red** (`danger-hover`): destructive-button hover.

### Neutral

- **Quiet Canvas** (`canvas`): main page backgrounds.
- **Working White** (`surface`): navigation, theme library, diagnostic logs and secondary buttons.
- **Inset Neutral** (`surface-alt`): preview ground, disabled buttons and secondary-button hover.
- **Deep Ink** (`ink`): titles, primary text and pressed-button text.
- **Secondary Ink** (`muted-ink`): explanations, metadata, diagnostic logs and disabled controls.
- **Fine Rule** (`line`): separators and secondary-button outlines.

**The State Rule.** Pair status color with words or an enabled, disabled or selected control state; color alone does not explain the result.

## Typography

**UI Font:** Microsoft YaHei UI, then Segoe UI Variable Text, then Segoe UI, then the system sans-serif fallback. The first installed family is resolved by `AppVisuals`.

**Diagnostic Font:** Cascadia Mono, then Consolas, then Courier New, then the system fallback.

Page titles use `page-title`; the selected theme uses `selected-title`. Section headings use `section-title`, body copy and buttons use `body`, and secondary metadata uses `label`. Bold body text identifies theme names and selected navigation; compact search labels may also be bold. The existing navigation wordmark is a local 13 pt treatment. Monospace is reserved for logs and raw JSON. Text has no custom tracking or fixed line-height system.

**The Native Type Rule.** Keep Chinese interface text on the native UI family chain and retain point-based font sizes. System typography is the deliberate choice for this task workspace.

## Layout

Measurements in the frontmatter are 96-DPI logical pixels, except point-based typography. Forms use `AutoScaleMode.Dpi`; layout containers, docking and scroll regions accommodate resizing. The spacing rhythm includes compact control gaps, regular text margins, column gaps and generous section boundaries.

The manager uses a fixed logical navigation width (176 px), a flexible content area and a bottom operation-result band (58 px). The theme workspace divides its list and detail area at 32/68 percent, with a 20 px column gap. The library uses a single-selection native ListBox with horizontal scrolling disabled; rows fill its available width. Theme details, settings and maintenance scroll vertically.

Installer content scrolls independently of its bottom actions. The ThemeLoader installer starts at 880 × 720 client pixels with a 760 × 660 minimum window; the standalone BepInEx installer starts at 860 × 680 with a 740 × 620 minimum. The manager starts at 1120 × 800 with a 960 × 700 minimum. These are WinForms source dimensions: client sizes and window minimum sizes are distinct.

The existing evidence covers normal and minimum dimensions plus synthetic 150% layout stress. It does not establish physical multi-monitor DPI behavior.

## Elevation & Depth

The implemented application has no custom shadows or gradients. Windows 11 uses the system Mica backdrop; older systems use the same neutral token fallback. White working surfaces, pale inset areas, whitespace and fine rules establish grouping. Native Windows frames and dialogs retain operating-system depth. Hover and pressed feedback changes color immediately; no custom animated transition is implemented.

## Shapes

Working panels and native fields are rectangular. Shared action buttons use a gently rounded outline through the `action` radius, scaled by `DeviceDpi`. Secondary actions have a fine outline; primary and destructive actions use solid fills. Separators are single logical-pixel lines. Button corners clear to the nearest opaque ancestor so transparent layout stacks preserve the surrounding surface.

## Components

### Buttons

Shared `ActionButton` controls use primary, secondary and destructive variants. The common factory height is represented by `button-primary`; installer actions use 44 px and navigation uses its own row height. Text has a horizontal drawing inset (12 px, DPI-scaled) and ellipsis when space runs out.

Hover follows the variant tokens. Every pressed variant uses `button-pressed`; disabled variants use `button-disabled`. Keyboard focus uses the native dotted focus rectangle inset from the control edge. These are immediate paint states. Preserve accessible action names and native button behavior.

### Inputs and native controls

Path and search fields remain native text boxes. A persistent “搜索主题” label identifies search independently of its placeholder; the placeholder explains name/author matching. Paths have adjacent validation text with a recovery action. File dialogs, checkboxes, scrollbars and message boxes retain Windows behavior rather than acquiring a separate custom skin.

### Navigation

A white navigation column holds the existing logo, product name, three task destinations and version. Rows are left-aligned, with soft brand-blue fill, brand-blue text and bold weight when selected. Theme selection, settings and maintenance remain distinct destinations.

### Theme library and preview

The native ListBox uses fixed-height owner drawing (64 px), single selection and vertical scrolling, with IntegralHeight disabled so it fills its layout cell. Each two-line row shows the bold theme name, then active-state text or author/version. Long text uses ellipsis. Selection uses soft brand blue; keyboard focus remains visible. A double-click applies the selected theme.

The preview fits real artwork within its available bounds, preserves aspect ratio and uses the inset neutral as its letterbox ground. Missing preview artwork, invalid themes and a search with no results have different messages. With no selection, metadata is suppressed, recovery guidance is visible and theme actions, including “查看 theme.json”, are disabled. The manifest action has a local minimum width (164 px) to retain its full mixed-language label. Raw manifests open in a native read-only, scrollable dialog.

### Operation feedback

Installers pair inline directory/environment diagnosis, a read-only diagnostic log, an operation-result label and stable bottom actions. ThemeLoader installation uses a marquee progress bar while busy. The standalone BepInEx installer shows an explicit installing or removing message and a wait cursor. Busy operations disable conflicting controls and prevent closing the window; completed or failed operations restore valid actions.

The manager uses its bottom band for operation feedback and a wait cursor during theme changes. Destructive tasks are located under maintenance and use confirmation dialogs.

### Artwork

Use the existing embedded logo and icon. Default theme previews and exports come from the read-only source artwork. The implementation adds no generated raster artwork or invented theme metadata.

## Do's and Don'ts

### Do:

- Do keep the shared palette, native type chain and point-based text sizing consistent.
- Do keep labels visible and provide distinct empty, invalid, busy and disabled states.
- Do keep theme rows within the list width and retain vertical scrolling at supported minimum sizes.
- Do preserve keyboard focus, accessible names and Ctrl+F, Ctrl+O and F5 shortcuts.
- Do use real theme artwork and preserve its aspect ratio.

### Don't:

- Don't add gradients, decorative cards or an oversized empty branding panel to this working utility.
- Don't use generated or substituted artwork for the Default theme.
- Don't style unavailable actions as enabled or expose empty metadata as a selected theme.
- Don't treat synthetic 150% captures as physical DPI verification.

## External visual reference

The system follows [Microsoft Fluent 2](https://fluent2.microsoft.design/) color roles and [Windows Mica materials](https://learn.microsoft.com/windows/apps/design/signature-experiences/materials). The mapping and implementation boundary are recorded in `docs/FLUENT_2.md`.
