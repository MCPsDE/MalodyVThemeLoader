## verdict

1. **Resolved — editor clipping and reachability.** Refreshed `manager-scale150.png` captures in both `live2d-dark` and `live2d-light` show the complete theme selector, chevron and editor scrollbar inside the page. Their `manager-scale150-fields.png` and `manager-live2d-minimum-fields.png` captures visibly contain both motion selectors and all three fitting fields above the fixed Save/Restore actions. Source constrains the page/content width and scrolls editors into view on focus entry. `artifacts/ui-dark.log` and `ui-light.log` each record successful viewport containment and native idle → tap → size focus traversal at minimum size and synthetic 150%. Normal and switched captures retain readable fields, Murasame selection and the established footer.
2. **Resolved — persisted design records.** DESIGN.md now records import-before-list order, model/motion/fitting bindings, independent scrolling, fixed actions/status, ChoiceBox states and current top/scrolled capture paths. The surface brief records the 2.3 extension and evidence limits; PRODUCT.md records the workflow and saved-versus-loaded distinction. The paired ChoiceBox specimen in `.impeccable/design.json` identifies native implementation and metrics as authoritative. `.impeccable/review/live2d-documentation.md` records the narrow documentation merge.

Regressions introduced by the fix batch: none visible in the required refreshed captures. Numeric values remain readable in both palettes and after switching.

## remaining

Clear for the two scored fixes. Ship covers these scored fixes only. Synthetic 150% scaling and native bitmap captures do not establish physical DPI, assistive-technology behavior or successful in-game application; the supplied keyboard check covers idle → tap → size rather than a complete accessibility audit.

disposition: ship
