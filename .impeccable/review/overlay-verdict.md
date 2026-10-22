disposition: ship

## persistence

Pass for the 2.6.1 runtime-overlay correction. This is a fresh full review of the user's report that bounds and names do not appear over the game; the earlier 2.6.0 ship verdict is not evidence that this behavior worked.

Read the project rules and pitfalls, `PRODUCT.md`, `DESIGN.md`, the 2.6.1 surface-brief correction, `docs/SCENE_INSPECTOR.md`, the craft floor, the affected controller and validation sources, and the supplied build/runtime/interop logs. The completed `overlay-documentation.md` handoff and scoped PRODUCT/DESIGN diff record the cause, fallback, evidence and limits. The workflow prerequisite now correctly names 2.6.1 or newer and a restart. The manager's waiting message receives only the same-length `2.6.0` → `2.6.1` prerequisite correction; its control and layout are unchanged. The subsequent Release build also records zero warnings and errors. DESIGN frontmatter is preserved; the unchanged design sidecar hashes to `BDD8B44BE9777910B69A419CF521C26B5E451BB33B83B41E49BCB9943487AE8D`.

This correction extends the incumbent Operate surface without a new visual world, approved comp, concept selection or shipping raster. The existing Fluent manager/installer system remains authoritative. No HTML/CSS detector applies to the native Unity drawing path. The reference-audit log reports zero differences within its stated source/stable-game scope; configuration content is outside that assertion.

## fidelity

Opened all three required 640 × 480 captures in `.impeccable/review/overlay-2.6.1`: `overlay-presented.png`, `hierarchy.png` and `node-video.png`. Each shows its named state. The black background is the intentional synthetic scene ground, with intact fixture regions and foreground content; these are valid captures rather than blank player frames. The final-screen capture is separately identified from the camera-target comparisons.

| Element / promise | Status | Evidence |
| --- | --- | --- |
| Bounds and names over presented content | match | `overlay-presented.png` visibly places outlines and Chinese node names over the sibling regions, container and clipped-region fixture; the selected right sibling retains yellow bounds/name. The end-of-frame screen test asserts edge pixels and interior glyph pixels separately. |
| TYPE | adaptation | The correction brief explicitly authorizes retained game/built-in fonts because the former OS-font binding throws in the actual interop. CJK labels are present in the final capture, while production keeps the incumbent 13-pixel label treatment. No desktop typography token changes. |
| MATERIAL | match | Solid debug lines and plain labels remain functional Unity geometry. Existing neutral/component colors, yellow selection, line widths and transparent host overlay are preserved; no decorative substitute or new raster appears. |
| GROUND | match | The authorized ground is the host game's existing content, not a new palette. The final capture preserves the synthetic black field, colored regions and foreground caption. `node-video.png` retains the background/video composite and foreground without inspector geometry. |
| Font loss and recovery | match | Controller lookup failures stay local; missing fonts omit names while bounds continue, and lookup retries once per second. The runtime assertions cover continued inspection/bounds, removed stale labels and recovered names. |
| Font ownership and cleanup | match | The inspector destroys its overlay, meshes and material, preserving the borrowed font. The runtime cleanup assertion confirms that the fixture's original UI font survives. |
| Existing inspection/media behavior | match | The 33-check result retains exact-node separation, clipping and overlap selection, bidirectional selection, raycast routing, UInt16 batching, projection, lease expiry, video alpha/opacity and cleanup checks. `hierarchy.png` and `node-video.png` support those surrounding visual states. |

The source removes OS-font creation from overlay setup. `overlay-old-interop.log` rejects the old unguarded exception-stub call; `overlay-new-interop.log` accepts 311 game/Unity references with one existing locally caught optional font fallback explicitly recorded. That optional call is not represented as an available drawing API. `overlay-build-2.6.1.log` records the full Release solution with zero warnings and errors.

## ceiling

Reached for this bounded correction. The debug layer supplies the requested visible bounds and names while retaining the incumbent design and host content. Additional decoration, motion, manager layout changes or broad polish would exceed the brief.

The strongest presentation evidence is a genuine automatic `ScreenSpaceOverlay` frame in an isolated Windows Mono player, normally presented on a private desktop that is never activated onto the user's desktop. The harness uses adapted production sources and runtime UGUI. Actual-game logs and offline interop inspection establish the diagnosed stripped binding, not an end-to-end run of the corrected plugin inside Malody V. Physical mouse delivery, monitor DPI and screen-reader behavior remain outside this review's evidence. The unchanged desktop layouts retain their earlier capture scope.

## material_fixes

None within the reviewed overlay-correction scope.

## keep

Keep solid bounds/names, yellow selection, independent visibility options, retained-font borrowing and retry, font-independent bounds, exact-node identity, UI-only inspection, ordinary-click routing, lease cleanup, media fallback and the explicit distinction between isolated-player evidence and actual IL2CPP game execution; preserve all incumbent manager/installer controls, layout, palette, logo and Default artwork.
