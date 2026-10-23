# 2.6.2 update strip documentation

The 2.6.2 update strip extends the incumbent native WinForms Fluent 2 workspace. `PRODUCT.md`, `DESIGN.md` and the 2.6.2 scope in `.impeccable/surface-brief.md` remain the authority for the product and visual system. This ordinary extension introduces no new visual world, concept seed, approved comp, palette or shipping raster. `Directory.Build.props` records product version **2.6.2**; the displayed **2.6.3**, **37%** and **37.0 / 100.0 MiB** are synthetic update fixtures.

## Implementation evidence checked

| Source | Evidence |
|---|---|
| `src/MalodyVThemeLoader.ThemeManager/UpdateStrip.cs` | Two-column phase/detail and content-sized cancel/retry action above a shared linear track. It reuses the working surface, primary/muted ink, danger ink and shared button factory. Known progress shows detail; verification omits byte claims; failure hides the track and offers “重试更新”. |
| `src/MalodyVThemeLoader.ThemeManager/ThemeManagerLayout.cs` | An auto-sized update row sits above operation feedback. Downloads leave the workspace enabled. Closing cancels the update; preparation waits for theme/editor operations and honors the existing unsaved-edit confirmation before allowing the manager to close. No-update results hide the strip. |
| `src/MalodyVThemeLoader.ThemeManager/UpdateService.cs` | Received/total bytes determine percentage and MiB; missing or nonpositive totals leave percentage indeterminate. Download, verification and preparation have distinct phase messages. The service binds the installer filename to the newer release version, checks SHA-256 before launch, supports cancellation/timeouts, rejects incomplete downloads and cleans unsuccessful staging. Hashing runs off the UI thread. |
| `src/MalodyVThemeLoader.Windows/LinearProgress.cs` | The established line and accent-fill colors form a flat six-pixel track. Known percentage is clamped to 0–100; unknown percentage animates a quarter-width segment. Native accessibility exposes the ProgressBar role, “更新进度” name and percentage or processing value. The timer stops when hidden/disabled and is disposed with the control. |
| `src/MalodyVThemeLoader.Windows/BrandBar.cs` | “外观” uses a shared single-line label in an auto-sized column. Exact credit `by 猫猫虫小窝(451586654)`, saved sun-left/moon-right appearance, `赞助支持` and `https://ifdian.net/a/mcpsde` remain. The installer branch returns after the credit and retains its fixed-light convention. |

## Captures and verification

Both `.impeccable/review/update-dark` and `update-light` contain seven native `Form.DrawToBitmap` captures, for 14 total:

| Capture | Full-window dimensions | State |
|---|---|---|
| `desktop-download.png` | 1138 × 847 | Known-length download at normal size. |
| `minimum-download.png` | 960 × 700 | Known-length download at minimum size. |
| `minimum-unknown.png` | 960 × 700 | Received MiB with an unknown total; no invented percentage. |
| `minimum-verify.png` | 960 × 700 | Verification without byte claims. |
| `minimum-failure.png` | 960 × 700 | Timeout and complete retry action. |
| `scale150-download.png` | 1698 × 1102 | Synthetic 150% layout stress. |
| `font125-download.png` | 960 × 700 | Minimum size with synthetic 125% fonts. |

This documentation pass verified all 14 files and dimensions and opened the dark minimum failure and light 125% font captures. They show complete update/appearance actions, the preserved footer and the update band above operation feedback. `.impeccable/review/update-2.6.2-verdict.md` records the independent finish review of all 14 captures with `disposition: ship` and no material fixes. Surrounding scrollable theme content was checked for integration, not re-reviewed as a redesign.

The inspected `scripts/test-update-ui.ps1` supplies synthetic progress/result records, isolates appearance preferences, disables live update checks, asserts full-label geometry and checks the accessible `37%` value. `artifacts/update-ui-dark-2.6.2.log` and `update-ui-light-2.6.2.log` both end in PASS for download, unknown length, verification, retry and the normal/minimum/font/scale matrix. `artifacts/constraints-dark-2.6.2.log` and `constraints-light-2.6.2.log` separately pass shared numeric typing, keys, ranges and accessible values plus complete action/navigation labels across five manager pages and both installers at normal/minimum sizes, synthetic 125/150/200% scaling and 125% fonts.

`scripts/validation/UpdateValidation.cs` and `artifacts/update-service-2.6.2.log` record **48 update/heartbeat assertions**. Fake HTTP and launch boundaries exercise known/unknown lengths, progress monotonicity, hash mismatch, missing checksum asset, truncated body, cancellation, declined installation, failed launch, current version, HTTP failure and malformed metadata. The heartbeat fixture survives a blocked UI, respects another live manager, remains disconnected after that owner expires and stops after disposal. The verdict's 47 assertions reflect its earlier read; the final service fixture added the disconnect/foreign-owner regression without changing the reviewed visuals. These fixtures do not launch a downloaded installer or establish a real GitHub download.

`artifacts/inspector-runtime-2.6.2.log` separately reports `passed: true` and **4,842 checks** in an isolated Windows Unity Mono player, with automatic `ScreenSpaceOverlay` presentation at 640 × 480. This is plugin/runtime regression evidence, separate from the update UI and its 48 service assertions. No implementation tests or builds were rerun by this documentation pass.

## System summary and limits

- Palette: established paired neutral canvas/surfaces, readable blue, solid accent fill and semantic danger ink.
- Type: incumbent native Chinese UI family chain and point-based hierarchy; phase/detail reuse nine-point text helpers.
- Layout: compact phase → measured bytes → linear progress above operation feedback, with a complete right-hand action.
- Rules: The State Rule, The Native Type Rule, The Complete Label Rule and The Themed Number Rule remain in force.
- Identity: saved manager dark/light appearance, exact credit and sponsor, fixed-light credit-only installers and original Default artwork.

Still captures establish appearance and geometry, not indeterminate animation quality, physical monitor DPI, multi-monitor behavior, compositor Mica, screen-reader use, live network timing or an installed IL2CPP game/update run. Service tests exercise injected boundaries; manager waiting and unsaved-edit integration were inspected in source rather than fully driven through a real installer. No HTML/CSS detector applies to native WinForms. No new shipping raster needs provenance; the PNGs are validation captures of existing controls and fixtures. This pass wrote only this record and the compact surface-brief evidence, without modifying the read-only source repository or installed game.

Recorded drift was not canonized or repaired: `DESIGN.md` still describes a fixed 44-pixel appearance-label column, while current `BrandBar` uses content sizing; the existing sidecar's inspector specimen retains its historical 28-check description. These descriptive/evidence records do not justify changing incumbent tokens or broadening an ordinary extension into a system refresh. `DESIGN.md` and `.impeccable/design.json` were preserved byte-for-byte, with SHA-256 `51A8308F8DF56EAD449EC0824F69E7849B0258AA85F5E14D2485A59282D7DDB6` and `BDD8B44BE9777910B69A419CF521C26B5E451BB33B83B41E49BCB9943487AE8D`, respectively.
