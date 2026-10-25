# Unreleased

- Update Terminal.Gui to `2.5.1-develop.171`. Its only upstream change improves
  test assertions; runtime source and transitive dependencies are unchanged.
  See the [dependency review](../terminal-gui-update-review.md#october-24-follow-up-develop171)
  for source comparison and compatibility checks.
- Reclaim unused Source-column width for package names. Keep versions visible
  across the supported terminal sizes without expanding the Source column.
- Put descriptions before secondary metadata and use compact, clickable link
  labels. Omit repeated author/version values, copyright, opaque family names,
  and installed/upgrade tag lists; Search keeps a short tag summary.
- Wrap long identifiers and paths without dropping their tails. Make Help wrap
  vertically at 80×24, keep review/result reasons visible, separate review buttons,
  and clarify visible-row marks and review-before-execution instructions.
- Replace synthetic mock metadata with a frozen snapshot of 23 real catalog
  entries, including 18 installed packages and 14 available upgrades. Remove
  duplicate search identities and preserve installed versus catalog versions.
  See the [UX audit](../ux-audit-2026-10-24.md) and
  [fixture provenance](../../src/MockData/README.md).
- Use a single-cell check in the existing marker column for batch selections in
  Search and Upgrades, giving the four reserved name cells back to package names.
- Keep empty-list messages readable after filtering a populated list. Distinguish
  the current marked row with `◆`, and keep Source hidden in narrow Upgrades panes
  until version columns have enough space.
- Offer secondary package links through **More links…** in the keyboard action
  menu. Preserve literal link labels, original destinations, and backend errors.
  Disambiguate duplicate names in review/results only when needed and align their
  status labels. See the [PR review follow-up](../pr65-review-follow-up.md).
- Remove the Product code list from Package Details. These installer identities
  support package detection and maintenance rather than everyday package browsing.
  See the [interface review](../product-code-ui-review.md) for the comparison.
- Update Terminal.Gui from `2.5.1-develop.89` to `2.5.1-develop.170` for Windows
  supplementary Unicode input, Markdown partial redraw, and nested-dialog
  shutdown fixes. Details links use the updated Markdown renderer.
- Add rendered Details-link compatibility coverage for partial redraws,
  scrollbar hover, resizing, and scrolling.

See the [dependency review](../terminal-gui-update-review.md) for upstream
capabilities, adoption decisions, source commits, and validation limits.
Published 1.1.0 downloads are unchanged.
