## verdict

1. Resolved — exact-control scope selection and reload. Both resources-control.png captures now show Canvas/Panel/Target, Image, the distinct 69 × 74 replacement and a 4 px left margin after reading the scope, rather than the scene replacement. The visible “读取此范围” and “取消定位” actions establish an explicit transition. ScopePending gates import, slice editing, preview and Save/Restore controls; CommitScope validates, resolves any dirty resource draft, reloads persisted rules and loads the selected rule. Clean same-key SetTheme calls Workspace.Reload, and Applied refreshes the library selection. The inspected test assertions cover manual reopening of the correct file/margin, cancellation, external same-key refresh and canonical directory selection after legacy ZIP materialization; both editor logs pass. Dirty-draft retention is supported by the implementation, not a separately exercised assertion for every branch.
2. Resolved — light preview visibility. The recaptured light resources-images.png and resources-minimum.png visibly retain the pale source border and stretched result over the medium neutral checker; dark image views remain legible. ResourcePreviewSurface.Checker uses #666666/#808080 for both source and stretched previews, while drawing the original bitmap and alpha unchanged.
3. Resolved — persisted workflow and design documentation. PRODUCT.md now records the resource tree, scope-read/cancel transition, image inheritance, slices, audio, drafts, refresh and materialized directory workflow. DESIGN.md records all five destinations, 24/44/32 editor layout, independent scrolling, fixed Save and local neutral preview checker. The 2.5 brief and resources-documentation.md agree with the reviewed implementation and retain the synthetic-DPI, silent-audio and isolated-Unity-versus-installed-game limits.

## remaining

Clear. All ten required recaptures were opened and remain valid. No regression introduced by this fix batch was identified in the supplied evidence. Physical monitor DPI, assistive-technology behavior, audible device output and installed IL2CPP execution remain outside this evidence.

Ship covers the scored fixes, not the whole surface.

disposition: ship
