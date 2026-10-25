# Using Scout for WinGet

For installation and a quick start, see the [README](../README.md#install).
Press `?` in Scout for the current in-app shortcut reference. This guide
explains the main workflows and where their results are stored.

This guide describes v1.1.1. See the [release notes](releases/1.1.1.md) for
the compact layouts, simpler Details, and rendering improvements.

A one-cell `✓` in the left marker column identifies
marked Search and Upgrades packages; `◆` identifies the current marked row.
The current unmarked row uses `●`, and package
names no longer reserve selection padding. Package Details also omits the Product
code list. It prioritizes descriptions, uses compact clickable link labels, and
wraps long IDs. Help and review dialogs also use the available width more
effectively. The action menu's **More links…** entry provides keyboard access to
support, privacy, purchase, and documentation links. Review/result rows include
source or ID only when needed to distinguish packages with the same name.
See the [UX audit](ux-audit-2026-10-24.md) for the checked surfaces and limits.

## Navigate and find packages

The **Search**, **Installed**, and **Upgrades** tabs share a package list and
detail pane. Use `←`/`→` or `1`/`2`/`3` to switch tabs, `↑`/`↓` or
`j`/`k` to move through rows, `PageUp`/`PageDown` to scroll a page, and
`Home`/`End` to jump to the first or last row. Moving beyond either end keeps
focus in the list. Use `Tab`/`Shift+Tab` to move focus between list and
detail. You can also click a tab or package row and scroll with the mouse.

In Search, `/` or `s` opens a package query. In Installed or Upgrades, it
filters the current list. `f` cycles the configured package sources and `r`
refreshes the view. `S` cycles sort fields and direction; sortable table
headers can also be clicked. `P` cycles the pin filter in Installed or
Upgrades.

In narrow windows, the list omits the ID column and, when necessary, the
Upgrades source column so package names and versions remain visible. Full IDs
and sources stay in Package Details; widening the window restores the columns.
The list title shows the active sort field and direction, even if its column is hidden.
Changing themes or cycling pin filters keeps the highlighted package when it
is still visible. Help supports arrow keys, PageUp/PageDown, and Home/End.

## Install, upgrade, and inspect

Select a row to see its metadata. `i` starts an install; `I` chooses a
specific version when the backend can list versions, or lets you enter one
when it cannot. `A` opens advanced install settings and `d` downloads an
installer without installing it. `u` upgrades the highlighted package, or reviews
all marked packages in Upgrades. In Search, `i` also reviews all marked packages
when there is a selection. `x` uninstalls the highlighted package.
On a supported COM backend, `V` verifies an install
and `R` repairs it. `p` pins or unpins a package.
Pinned packages must be unpinned before upgrading. If pin status is unavailable,
refresh successfully before starting an upgrade.

## Advanced install options

Advanced options explain whether each setting is a default or an explicit
override. **Review** fetches the installer preview before the final confirmation;
it does not install anything. The confirmation separates requested options from
the selected installer. A known architecture or scope conflict blocks the install
so you can change the option. If no preview is available, the confirmation says
that WinGet will resolve the options during execution. The COM backend's default
mode is Silent; the CLI leaves the mode to WinGet.

## Select and run a batch

For a typical upgrade batch:

1. Open Upgrades with `3` and mark packages with `Space`.
2. Press `u` or `U` to review the entire selection. Check the hidden-selection
   count if you have filtered the list or changed sources.
3. Inspect ready/skipped reasons. Enter or double-click opens a package's Details;
   close Details to return to the plan.
4. Use Tab to reach **Upgrade (N)** and approve it to run the ready packages.
5. Watch the dedicated progress row, then review results. The inventory refreshes
   while results are open. Use recent runs (`L`) for diagnostics or a reviewed retry.

On Search or Upgrades, `Space` marks a row and `a` toggles all visible
rows. `B` reviews selected Search results before installation; `U`
reviews selected upgrades before running them. The review shows items
that cannot be run and why. An install or upgrade never starts merely
because a row was marked.
In a plan, Enter or a double-click opens the highlighted package's details.
Use Tab to reach the action buttons, then approve **Install (N)** or
**Upgrade (N)** to run the N ready packages. Skipped packages are excluded.

When packages are marked, the footer shows `u Upgrade N` or `i Install N`, and
the first action-menu entry reviews the whole selection. The separate
**Upgrade highlighted package** / **Install highlighted package** menu entries
act only on the cursor row. All paths still require confirmation or plan approval.
Batch progress shows the current package number, total, and backend phase on a
dedicated status row. Results list every success, failure, and skipped package;
inventory refresh starts as soon as the batch finishes, while results are open.
Successful items are deselected; failed or cancelled items stay marked for review.

List headings show how many rows are visible out of the results loaded for the
current source/search. Selection counts include hidden rows and packages retained
from previous searches. `a` changes only visible rows; `B` and `U` review the entire
selection, including hidden packages.
Press `Esc` outside an input or active check/operation, or choose **Clear selection**
in Actions, to clear every marked package, including hidden ones. Sorting keeps the
highlight on the same package, and install/upgrade reviews list visible packages in their
current order before any hidden selections.
Plans refresh catalog or upgrade inventory before review. Removed sources,
missing packages, and ambiguous matches are skipped individually. Failed inventory
or catalog lookups cannot authorize an operation.
`Esc` cancels a pending plan check. Switching tabs or sources, or refreshing the
inventory, also discards that check so a late result cannot open an outdated review.

## Contextual actions and other controls

Press `m` or `Shift+F10`, or right-click a package row, to open its contextual
actions. The menu reflects the current tab, backend capabilities, and known pin
state. Package-changing actions still open their usual confirmation or review.

`o` opens the package homepage and `c` opens release notes when the
selected package provides those links. `e` exports the visible rows to
CSV. `t` chooses a theme; `L` opens recent run results. `Esc` cancels
the current input or operation as appropriate, and `q` quits. `Ctrl+Q` quits from
fields or any nested dialog, including Details inside a plan.

## Run history, diagnostics, and retries

In recent runs, open a run and choose **Details** (or press Enter on a package)
to inspect timing, source, versions, install options, backend, CLI exit codes,
and diagnostic output (up to 16,384 characters per package). Use arrow keys,
Page Up/Down, Home/End, or scrollbars to navigate the details. **Retry package**
uses the highlighted row. **Retry unfinished (N)** includes retryable failed and
skipped packages, including packages cancelled or not started in an interrupted batch.
Retries recheck the exact package/source, installed state, available version,
and pins as applicable, then show a review with ready and skipped entries.
**Details** in the review shows the full request, including advanced options.
Nothing runs until you approve that review. Original results stay in history.
Pin changes and older entries without a recorded request have no retry action.
An explicit version is skipped if the backend cannot confirm its availability.

### Copy or save a report

Use **Copy**, **Save text**, or **Save JSON** in a run's diagnostic view to keep
the displayed package's report. Save lets you choose a file and asks before
replacing an existing file. Cancelling the save dialog or a failed save leaves the original file
and history intact. Clipboard errors stay in the viewer so you can save instead.
Saved reports remain available while another operation, review, or CSV export is
in progress; opening or closing a report does not cancel that work.

## Backend and source diagnostics

Press `D` or choose **Backend and source diagnostics** in the action menu.
The panel shows the active backend and version, process architecture, supported
verify/repair actions, any recorded COM fallback reason, active source filter,
and the most recent inventory load error in this session.
The `D` shortcut also works while a foreground workflow is in progress.

Source checks query each configured source for `Microsoft.PowerToys`. The report
distinguishes a successful query with no matches from a failed query or failed
source discovery. These are read-only catalog checks; they do not establish that
installation will succeed. **Refresh** repeats the checks; closing the panel
cancels outstanding work. **Copy**, **Save text**, and **Save JSON** use the
current completed report. No package changes are performed by the panel.

## Saved sets and update checks

On Search, `g` opens saved package sets so you can save, load, or delete a
set of package choices. In Upgrades, `C` checks for updates now or
configures a daily local check. The first successful scheduled check
establishes a baseline; later checks can notify you when an unpinned
upgrade changes or a check fails. Checks only read package state. Disable
the schedule from the same dialog to remove its Windows task.

Scout stores sets, recent runs, check results, and schedule settings in
`%LOCALAPPDATA%\WinGetScout`. Keep the installed or extracted executable
at the same path if you use scheduled checks, because the task records
that path.

Mock sessions use `%LOCALAPPDATA%\WinGetScout\Mock` for their local data.
**Check now** remains available there, but enabling, changing, and removing a
daily task is disabled. The scheduled `--check-updates` command rejects explicit
mock mode before reading or writing real-session settings or check results.

## Which backend is running?

The header shows **COM**, **CLI**, or **Mock**. The complete portable ZIP
tries COM on Windows and falls back to CLI if it cannot activate. The
standalone EXE uses CLI because it lacks the companion COM files. Both
support core package operations; COM can add richer details, version
lists, installer previews, live progress, and supported verify or repair
actions. Pin management uses the CLI with either backend.

For a Windows activation check, run `wingetscout.exe --comdiag` from the
complete published folder. Developers can see the backend boundaries in
the [architecture guide](architecture.md).

## Troubleshoot a workflow

- **Marks seem to be missing:** Read the selected/hidden count. Filtering, changing
  sources, or searching again can hide marked packages without clearing them.
  Review the full selection, or use **Clear selection** in Actions to start over.
- **An upgrade is unavailable:** Pinned packages must be unpinned first. If pin
  state is stale or unavailable, refresh successfully before reviewing an upgrade.
- **A batch only partly completed:** Inspect its results in `L`. Use **Retry
  unfinished (N)** to recheck retryable requests; the new review explains any skips.
- **A list or source failed to load:** Open `D`, refresh the read-only checks, and
  copy or save the completed report. A source returning no matches is different
  from a query failure. A source check does not prove installation will succeed.
- **Text looks different between terminal hosts:** Windows Terminal is recommended.
  Terminal palette and color-support settings affect rendering; `NO_COLOR` or
  `TERM=dumb` in the launching environment can reduce the palette. Try `t` for
  another theme and include the terminal host when reporting a display problem.
