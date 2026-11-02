# Terminal.Gui update review

## November 1 follow-up: develop.183

Update current `main` from `2.5.1-develop.171` to `2.5.1-develop.183` for
[#68](https://github.com/harder/wingetscout/issues/68),
[#69](https://github.com/harder/wingetscout/issues/69),
[#71](https://github.com/harder/wingetscout/issues/71), and
[#72](https://github.com/harder/wingetscout/issues/72). Earlier local reviews
covered these updates, but the published branch still used `.171`. This change
is reconciled with the newer Scout 1.1.1 UI and .NET SDK `10.0.403` on main.

Checked every page of NuGet's registration index by publication time. `.183`,
published October 30, remains the newest listed develop build on November 1.
The numerically higher `2.6.0-develop.61` was published earlier. Package nuspec
commits identify the reviewed builds:

| Package | Source commit | Alert |
| --- | --- | --- |
| 2.5.1-develop.171 | `edaceba039833eaa4652014e77ee391182ea74f9` | Main baseline |
| 2.5.1-develop.178 | `fed5e31f8ed144a1e3515fb13d9c7de835f02fee` | #68 |
| 2.5.1-develop.181 | `f210f00f638f3968654c31d93ecbbcb814902a71` | #69 |
| 2.5.1-develop.182 | `3540e9c05c48e11a4b1102f6942d1f5c6f627b7d` | #71 |
| 2.5.1-develop.183 | `84ea67cb4b43c3e711b54dfb46d3facce078f292` | #72 |

The [exact upstream comparison](https://github.com/tui-cs/Terminal.Gui/compare/edaceba039833eaa4652014e77ee391182ea74f9...84ea67cb4b43c3e711b54dfb46d3facce078f292)
contains 12 commits. Every alerted build is included in `.183`.

### Fixes and adoption decisions

| Upstream change | Scout impact and adoption |
| --- | --- |
| [ListView selection/viewport reset, #5745](https://github.com/tui-cs/Terminal.Gui/pull/5745) | Adopt automatically. Detaching a source clears selection; clearing or replacing it resets vertical scrolling; removing collection items clamps selection and an empty collection clears it. Four regression cases use Scout's VersionPickerDialog to test detach, SetSource(null), shorter replacement, and collection shrink/clear, followed by rendered row recovery and arrow/Tab navigation. These cases failed with the earlier `.181` package and pass with `.183`. |
| Summary-row source replacements in Scout 1.1.1 | Scout's PackageListView reformats plan/result summaries on width changes by replacing its source. Two additional tests scroll the actual plan/result dialogs to their last row and narrow/shrink them. Existing selection restoration keeps that row visible and focused with `.183`; no compensating viewport workaround is needed. Retain explicit initial selections, index checks, width caching, and endpoint key handling. |
| [Begin/Run synchronization context, #5722](https://github.com/tui-cs/Terminal.Gui/pull/5722) | Adopt automatically. Init/Begin/End preserve the caller's ambient context; Run owns the toolkit context only while its loop executes. A nested Details-session test verifies caller-context preservation through initialization, Begin/End, and disposal. Retain explicit driverless dispatch pumping, guarded InvokeAsync, cancellation, and application lifetime cleanup. |
| [Hidden application shortcuts, #5758](https://github.com/tui-cs/Terminal.Gui/pull/5758) | Application-scoped MenuItem/Shortcut bindings work with closed popovers and register when App becomes available. Scout does not use those menu APIs, but the shared hotkey handler changes. A real BatchPlanDialog test verifies that Alt+I cannot approve a hidden local Install button and works when it becomes visible. Preserve Scout's global Ctrl+Q and explicit plan review. |
| [Markdown table layout, #5739](https://github.com/tui-cs/Terminal.Gui/pull/5739) | Adds shared grid geometry, row/column spans, rich cell content, nested copying, and link-focus preservation on relayout. Scout's package list uses TableView; its Details links already use Markdown and retain the newer compact-link, repaint, hover, destination, and resize coverage on main. Rich fetched documentation previews remain a separate feature; package descriptions remain plain text. |
| [Menu OpenAndSelect, #5746](https://github.com/tui-cs/Terminal.Gui/pull/5746), and [clearing stale submenu arrows, #5754](https://github.com/tui-cs/Terminal.Gui/pull/5754) | No explicit Menu, MenuBar, MenuItem, or PopoverMenu usage. Keep Scout's PackageActionDialog and ListView workflow; no API migration is required. |
| [Custom file-dialog icons/Nerd Fonts, #5727](https://github.com/tui-cs/Terminal.Gui/pull/5727) | FileSystemIconProvider methods become virtual and Nerd Fonts mappings improve. The diagnostics SaveDialog can use the updated default provider. A custom provider adds no needed behavior; actual font rendering still needs an interactive terminal. |

Reviewed startup, disposal, UI dispatch, callbacks, timers, keyboard handling,
table layout, Markdown links, and file-dialog usage against the changed runtime
files. No application API migration is needed. Existing compact-link and
rendered-layout tests also cover the current UI behavior.

Terminal.Gui's transitive dependency versions are unchanged. Both WinGet COM
packages remain on the matching latest stable `1.29.380` pair; newer available
versions are previews. xUnit remains latest stable `4.0.2`. Keep the already
merged SDK `10.0.403` and regenerate all three lockfiles; only Terminal.Gui
version/hash entries change.

### Validation

Local validation uses Linux x64 and SDK `10.0.403`:

- All 757 .NET tests pass: eight new compatibility cases plus the existing
  749-test suite. Coverage includes list source changes, summary resizing,
  nested sessions, hidden approval hotkeys, Details rendering, cancellation,
  dispatch, workflows, and parsers.
- Both Windows COM configurations compile with zero warnings/errors.
- Locked restores pass for the test project and Windows x64/ARM64 publish
  restore graphs in both COM modes.
- Graceful mock `--smoke` exits successfully within 15 seconds with empty
  stderr. All four monitor tests, the 15-file site build, and whitespace checks
  pass.

PR CI supplies the Windows x64/ARM64 portable and Identity Native AOT publishes,
checks for the in-process companion DLLs or Identity winmd, and published mock
smokes. Those jobs validate packaging and mock startup. Live Windows Terminal
input/mouse/font behavior, real COM activation, installed MSIX activation, and
package-changing operations still require an interactive Windows host and are
not established by these tests. Use read-only `--comdiag`/`--comsmoke` on that
host before relying on a COM session; native ARM64 COM mutations remain a
documented limitation.

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
