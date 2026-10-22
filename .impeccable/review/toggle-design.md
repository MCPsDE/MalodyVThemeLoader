# Sun/moon toggle documentation

`DESIGN.md` and the schema-v2 `.impeccable/design.json` record the final native implementation in `BrandBar`, `AppearanceToggle`, `AppAppearance` and both `InstallerForm` classes. The established Fluent 2 palette, typography, artwork and workspace structure remain the visual authority.

The manager keeps its exact credit and sponsor action. Its icon-only toggle draws a sun on the left and moon on the right, defaults to dark, saves the chosen mode, toggles on click and selects light/dark with Left/Right. The button is 92 × 38 logical pixels inside a 108-pixel footer allocation including its 16-pixel right margin. The manager footer is 58 pixels high. Both installers use the existing light palette and a 40-pixel credit-only footer without appearance or sponsor controls; their process override preserves the manager preference.

The sidecar replaces the obsolete dropdown specimen with self-contained inline-vector dark/light manager footer specimens and a light installer footer. These HTML/CSS samples illustrate native geometry and states; WinForms source owns behavior, painting and accessibility. Existing color tokens remain unchanged and no tonal ramps were added.

Current native evidence is in `toggle-dark` and `toggle-light`, including manager normal/minimum layouts and saved-mode restoration. Both installers remain light under either manager preference. Synthetic 150% layout captures do not verify physical multi-monitor DPI, and `Form.DrawToBitmap` does not verify DWM Mica.

Release artifacts belong directly in `artifacts/release`; older locked executables are retained. No application source, product context or verification report was edited during this documentation pass.
