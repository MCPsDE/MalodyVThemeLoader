# Appearance design documentation

The scoped refresh preserves the Fluent 2 Windows utility identity and records the implemented dark default, saved light option, exact author credit, sponsorship action and concise Live2D copy.

Updated records:

- `DESIGN.md`: exact dark/light palette, separate text accents and button fills, per-variant pressed colors, four task destinations, shared footer and independent appearance preference storage.
- `.impeccable/design.json`: matching narrative and ten illustrative native-control specimens, paired across appearances where their colors differ. Removed fabricated tonal ramps and obsolete light-only samples.

Sources: `AppVisuals.cs`, `AppAppearance.cs`, `AppearancePreferences.cs`, `ActionButton.cs`, `BrandBar.cs`, `WindowsBackdrop.cs` and `ThemeManagerLayout.cs`. Product decisions were taken from `PRODUCT.md` and the surface brief. Installer dimensions were checked against both installer form sources.

Evidence: `appearance-dark/` and `appearance-light/` contain normal, minimum, synthetic 150%, empty/search, settings, maintenance, Live2D and mode-switch/restoration captures. The normal manager pair was inspected while documenting; the finish review owns the broader UI verdict.

Verification: the sidecar parses as JSON, all frontmatter token references resolve, its narrative matches `DESIGN.md`, and `git diff --check` passes for the documented files.

Limits: HTML/CSS specimens illustrate native control colors and states; they are not a second runtime implementation. No browser detector applies to WinForms. Synthetic scaling does not verify physical DPI or multi-monitor behavior, and bitmap captures do not verify DWM Mica. No artwork was added or changed. No new palette ramps, animation, UI concepts or runtime APIs were invented.
