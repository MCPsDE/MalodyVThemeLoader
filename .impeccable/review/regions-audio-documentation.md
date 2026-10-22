# Live2D regions and audio documentation

The 2.4 extension is documented in `PRODUCT.md`, `DESIGN.md` and `.impeccable/surface-brief.md`. This pass reconciles the existing Fluent 2 system with the implemented native preview, manual click regions, result-display triggers, per-animation audio and draft completion. It does not replace the visual world.

## Implementation evidence

| Source | Recorded behavior |
|---|---|
| `src/MalodyVThemeLoader.ThemeManager/Live2dAnimationEditor.cs` | Native preview/trigger split, idle/default-click/M1–M5/custom-region choices, manual drawing and naming, motion assignment, OGG/WAV audio controls, audition and validation before completion. |
| `src/MalodyVThemeLoader.ThemeManager/Live2dPreviewSurface.cs` | Default-pose rectangle drawing, normalized model coordinates, named selection overlays, last-added overlap priority, default-click fallback, real motion frames and audio playback preparation/stop. |
| `src/MalodyVThemeLoader.ThemeManager/NativeCubismPreview.cs` and `PreviewGpuContext.cs` | The selected game's existing Cubism Core, cached immutable Skia textures/shaders and a private offscreen GPU context, with CPU fallback. No running game window is used. |
| `src/MalodyVThemeLoader.ThemeManager/ThemeManagerLive2d.cs` | The workbench entry, cloned binding passed to the editor, completed draft adoption, unapplied status and complete binding commit through “保存并应用模型”. |
| `scripts/test-live2d-editor.ps1` | Disposable repository fixtures; checks frame changes, manual-only initialization/drawing, overlap/fallback, five result mappings, audio handling, persistence and invalid-region rejection. |

No body-part, HitAreas or motion-name inference is documented as assignment. A reused animation shares its audio binding; new playback stops the prior sound. Completing the editor validates and returns a draft, and closing without completion does not adopt the edited clone. The workbench remains the commit point.

## Captures and test evidence

Both `.impeccable/review/editor-dark` and `editor-light` contain `editor-desktop.png`, `editor-result.png`, `editor-minimum.png` and `editor-scale150.png`. The parent finish reviewer opened all eight and recorded their fidelity in `regions-audio-verdict.md`. This documentation pass additionally opened both desktop captures and checked the matching source. The images show the external user-supplied Murasame model, a manually named head region and a bound test OGG; the result capture selects M3. These are validation captures, not product or Default-theme artwork.

The supplied `artifacts/editor-dark.log` and `artifacts/editor-light.log` both end in PASS for native model/motion preview, manual drawing, overlap/fallback, five result bindings, OGG/WAV decode/import/playback preparation/stop, persistence and invalid-region rejection. Both report GPU rendering; the fallback is evidenced by source, not a separate CPU-mode pass. Existing PNG profile warnings in the dark log were not turned into design rules. This documentation-only pass inspected the logs and test source; it did not rerun the implementation tests.

Synthetic 150% captures establish layout stress only, not physical monitor DPI, multi-monitor behavior or assistive-technology results. Silent audio fixtures establish decoding and playback preparation/stop, not audible output. No real installed-game end-to-end result/audio playback check is established. Bitmap captures do not verify the compositor's Mica effect. Source and installed game are read-only inputs; this pass wrote only the three documentation files and this report.

## System summary

- Palette: existing paired dark/light neutral surfaces, restrained readable blue and shared solid action fills.
- Type: existing native Chinese UI family chain and point-based hierarchy; no token changes.
- Layout: large model preview beside native trigger and assignment controls, with a stable completion footer.
- Rules: preserve The State Rule and The Native Type Rule, manual authoring and truthful draft/saved/runtime status.
- Identity: exact credit, saved sun-left/moon-right manager appearance, manager-only sponsor, fixed-light installers and original Default artwork.

`git diff --check` passes, all referenced current capture/log/report paths exist, and the `DESIGN.md` frontmatter and token values match the existing version. `.impeccable/design.json` is unchanged because this is a narrow extension using existing primitives, not a system regeneration or a new reusable HTML/CSS specimen. Unrelated pre-existing system drift and unverified physical DPI, audible output and real-game integration are neither repaired nor canonized as validated behavior.
