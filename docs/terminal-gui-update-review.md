# Terminal.Gui update review

## October 24 follow-up: develop.171

For [issue #64](https://github.com/harder/wingetscout/issues/64), update from
`2.5.1-develop.170` to `2.5.1-develop.171`. The complete NuGet registration
index, including its paged entries, identifies `.171` as the newest listed
develop publication at review time (October 22, 2026, 19:47 UTC). No open
Dependabot update PR was present. Do not choose the numerically higher
`2.6.0-develop.61` over this newer publication.

Package nuspec repository commits establish the exact comparison:

- `.170`: `3f65bcb5cd5b4d080d3f0e5201d95f347384c371`.
- `.171`: `edaceba039833eaa4652014e77ee391182ea74f9`.

The former is an ancestor of the latter. The [complete comparison](https://github.com/tui-cs/Terminal.Gui/compare/3f65bcb5cd5b4d080d3f0e5201d95f347384c371...edaceba039833eaa4652014e77ee391182ea74f9)
contains exactly one commit, [upstream PR #5741](https://github.com/tui-cs/Terminal.Gui/pull/5741).
It changes only `Tests/UnitTests.Legacy/DriverAssert.cs` and
`Tests/UnitTestsParallelizable/Views/TreeViewTests.cs`. The assertion helper
now pads each expected screen row to the detected frame width, measuring
display columns, and the TreeView tests drop their local padding helper.

**Adoption decision:** there are no new runtime features, fixes, or APIs in
this package's source range. The entire `Terminal.Gui` source tree has the
same Git tree ID, `35fed84ec6181832d0c47bf07959096d323f2736`, at both commits.
Startup, UI-thread dispatch, callbacks, timers, shutdown, keyboard input,
layout, progress handling, and Markdown rendering therefore require no
Scout migration. Upstream's test helper is not a shipped application API;
do not add it to Scout or loosen Scout's rendered alignment assertions.
The earlier adoption decisions below remain applicable, including keeping
inline output and global native selection out of the fullscreen UX.

Regenerated portable, Identity, and test lockfiles change only Terminal.Gui's
requested/resolved version and content hash. The transitive graph is unchanged;
the WinGet COM pair remains `1.29.380`. There is no new runtime behavior to
justify an additional regression test; existing rendered compatibility and
UX regression suites exercise the package in Scout.

Validation on Windows ARM64:

- All 749 .NET tests passed, with no failures or skips, including rendered
  Details links, empty states, selection markers, narrow layouts, modal
  workflows, dispatch, and cancellation coverage.
- Portable Native AOT publishes passed for `win-arm64` and `win-x64`, both
  with the in-process COM companion DLLs. Both published executables passed
  graceful `--smoke` with exit 0 and empty stderr (x64 via Windows emulation).
- ARM64 `--comdiag` passed on main and threadpool MTA threads, each reporting
  three catalogs. No package-changing operations were performed.
- All four dependency-monitor tests passed. The site build and whitespace
  validation passed.
- A dedicated `.171` mock process was launched in Windows Terminal. The
  computer-use tool exposed no targetable terminal window, so no GUI input
  was sent; the dedicated process was cleaned up through the shell. Native
  keyboard/mouse/resize behavior, conhost, and installed MSIX activation
  remain unverified. In-process rendering tests and mock smoke do not
  establish those results. Identity AOT publishes were not repeated.

## October 21 review: develop.170

Reviewed October 21, 2026. This is an unreleased source update; published Scout
1.1.0 downloads have not changed.

## Package selection and tracked issues

Update `Terminal.Gui` from `2.5.1-develop.89` to `2.5.1-develop.170` and
regenerate the portable, Identity, and test lockfiles. NuGet's complete
registration index was checked by publication time, including every page;
`.170` was the newest listed develop build at review time. Package nuspec
repository commits establish source order independently of version numbers.

| Package | Nuspec repository commit | Tracking issue |
| --- | --- | --- |
| 2.5.1-develop.89 | `37a7e518855b075103fc7b059820cb17d606f928` | Current baseline |
| 2.5.1-develop.105 | `b54aebb9ab169752278c819790ea68015c2884f7` | [#53](https://github.com/harder/wingetscout/issues/53) |
| 2.5.1-develop.118 | `d82d84210fc8c3e59311d2324e92faff192391db` | [#60](https://github.com/harder/wingetscout/issues/60) |
| 2.5.1-develop.119 | `08fa2a56a96f98aca43862a7991b95818232d226` | [#61](https://github.com/harder/wingetscout/issues/61) |
| 2.5.1-develop.134 | `e75658f926870f8d27f3020d537c60a3de2a5ec6` | [#62](https://github.com/harder/wingetscout/issues/62) |
| 2.5.1-develop.170 | `3f65bcb5cd5b4d080d3f0e5201d95f347384c371` | Selected update |

All five older commits are verified ancestors of `.170`. The update covers
all four alerts; close them when the dependency change is merged. No issue
comments or state changes were submitted during this local review.

The [complete upstream comparison](https://github.com/tui-cs/Terminal.Gui/compare/37a7e518855b075103fc7b059820cb17d606f928...3f65bcb5cd5b4d080d3f0e5201d95f347384c371)
contains 81 commits, including merge, documentation, and review-fix commits.
The runtime changes fall into the groups below.

## New capabilities and fixes

| Upstream changes | Scout impact and adoption decision |
| --- | --- |
| [Windows VT Unicode input, #5721](https://github.com/tui-cs/Terminal.Gui/pull/5721): reads UTF-16 with `ReadConsoleW`, preserves supplementary characters, and stops synthesizing Ctrl+Z on wake/empty reads | Adopt through the dependency update. Benefits search/filter and dialog text entry, with no application API change. Driverless key tests cannot establish native console Unicode behavior. |
| [Markdown partial redraws, #5731](https://github.com/tui-cs/Terminal.Gui/pull/5731): renders during viewport clearing and preserves content on scrollbar hover and dirty-region repaint | Adopt immediately. `DetailPanel` already creates Markdown children for homepage, release notes, and documentation links. Added an ANSI-buffer compatibility test using those actual children. |
| [Markdown rendering and Markdig, #5737](https://github.com/tui-cs/Terminal.Gui/pull/5737): nested emphasis and link metadata, hard line breaks, definition lists, alert labels/copy, escaped table cells, table wrapping/width caching, and literal fallback after parser depth failures | Existing link rendering benefits automatically. Rich release-note or documentation previews are a useful future feature, but require fetching/content limits and a separate UI design. Keep package descriptions as plain text because backends do not promise Markdown. Upstream also adds Native AOT Markdown coverage. |
| [Inline output, #5715](https://github.com/tui-cs/Terminal.Gui/pull/5715): ordered awaitable `PrintAboveAsync`, literal/styled lines, session cancellation, preflight sizing, bounded queues, and output failure handling | Useful for a future compact command/progress interface with a persistent terminal transcript. Scout currently runs fullscreen, so do not call this API from its existing progress callbacks. It requires ANSI Inline mode and rejects unsupported controls/raster state. |
| Nested modal rendering and shutdown in #5715 | Adopt automatically. Upstream disposal unwinds nested run loops and settles session-owned work. Retain Scout's lifetime tracking, stale-request checks, and awaited `InvokeAsync`; the library cannot decide whether a backend response still belongs to the current workflow. |
| [Native terminal selection, #5723](https://github.com/tui-cs/Terminal.Gui/pull/5723): `app.Mouse.IsMouseDisabled` also disables ANSI terminal reporting, releases grabs, clears hover/hold state, and handles toggle races | A useful future explicit copy/selection mode, especially for package IDs and diagnostic text. Do not disable mouse globally: Scout uses row clicks, contextual menus, links, and wheel scrolling. Existing copy/export actions remain available. |
| [Inline shrink, #5724](https://github.com/tui-cs/Terminal.Gui/pull/5724), and [resize preservation, #5734](https://github.com/tui-cs/Terminal.Gui/pull/5734): live-region shrink/regrowth, transcript preservation, cursor ownership recovery, and coalesced resize bursts | Available for a future inline mode. These are not fixes to Scout's fullscreen table layout; keep its existing width-cache and resize handling. |
| [TreeView grapheme rendering, #5733](https://github.com/tui-cs/Terminal.Gui/pull/5733): avoids exceptions while drawing multi-rune Unicode graphemes | No current TreeView usage. Shared string column measurement is refactored to the same grapheme helper without changing normal width semantics. Retain Scout's table resize/selection coverage. |

## Compatibility and dependency findings

- No source migration is needed for Scout's used APIs. Reviewed startup and
  disposal in `Program.cs`/`ApplicationLifetime.cs`, guarded `InvokeAsync`,
  timers, keyboard routing, table/layout handling, mouse behavior, and
  `DetailPanel` Markdown links. The package table implementation has no upstream
  changes in this range.
- `TableData` cells are now explicitly Markdown inline source rather than
  plain display strings, including escapes/entities. Scout does not consume
  Markdown `TableData`; its package table uses a separate table source.
- Transitive updates include Markdig `1.4.0` to `1.4.2` and
  Microsoft.Extensions.Configuration.Json `10.0.12` to `10.0.13`, with the
  latter's file-provider/configuration dependency graph moving accordingly.
  Both COM packages remain on the matching stable `1.29.380` pair.
- The dependency monitor already traverses all NuGet registration pages and
  selects by publication time. Its four tests pass; this review found no
  monitor fix necessary. Source ancestry still needs human review before a bump.

## Validation

- Baseline `.89`: all 700 .NET tests passed on Windows ARM64.
- `.170`: all 701 .NET tests passed, including the new rendered Details-link
  compatibility test and existing modal, cancellation, dispatch, navigation,
  selection, and resize suites. The new test compares ANSI screen-buffer cells
  after partial repaint and scrollbar hover, then checks links after resize and
  scrolling back to the top.
- Windows ARM64 and x64 portable Native AOT publishes succeeded. Both contain
  `WindowsPackageManager.dll` and `Microsoft.Management.Deployment.InProc.dll`.
  Both published executables passed graceful `--smoke` with exit 0 and empty stderr.
- ARM64 and x64 Identity Native AOT publishes and mock smokes passed, each
  with exit 0, empty stderr, and the required projection winmd present.
  This packaging check is separate from live package-identity activation.
- Portable ARM64 `--comdiag` passed: main-thread and threadpool activation
  each returned three catalogs. Read-only COM inventory/search (`--comsmoke`)
  was not repeated for this Terminal.Gui-only change.
- Live Windows Terminal input/rendering validation was attempted, but the
  computer-use tool returned no targetable terminal window even though the
  dedicated Scout process/window existed. No GUI input was sent. The test
  process was stopped through the shell after inspection proved unavailable.
- Native Unicode typing, interactive terminal resize/mouse behavior, conhost,
  installed MSIX activation, and real COM install/upgrade/uninstall were not
  validated. Mock smoke and ANSI-buffer tests do not establish those results.

Suggested follow-up on an interactive Windows host: open the published mock
build, type supplementary Unicode into the filter, navigate and hover links,
resize narrow/wide, open nested dialogs and exit with Ctrl+Q. Repeat per host
whose behavior is required before release.
