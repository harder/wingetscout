# Using Scout for WinGet

For installation and a quick start, see the [README](../README.md#install).
Press `?` in Scout for the current in-app shortcut reference. This guide
explains the main workflows and where their results are stored.

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

## Install, upgrade, and inspect

Select a row to see its metadata. `i` starts an install; `I` chooses a
specific version when the backend can list versions, or lets you enter one
when it cannot. `A` opens advanced install settings and `d` downloads an
installer without installing it. `u` upgrades and `x` uninstalls the
highlighted package. On a supported COM backend, `V` verifies an install
and `R` repairs it. `p` pins or unpins a package.

Advanced options explain whether each setting is a default or an explicit
override. **Review** fetches the installer preview before the final confirmation;
it does not install anything. The confirmation separates requested options from
the selected installer. A known architecture or scope conflict blocks the install
so you can change the option. If no preview is available, the confirmation says
that WinGet will resolve the options during execution. The COM backend's default
mode is Silent; the CLI leaves the mode to WinGet.

On Search or Upgrades, `Space` marks a row and `a` toggles all visible
rows. `B` reviews selected Search results before installation; `U`
reviews selected upgrades before running them. The review shows items
that cannot be run and why. An install or upgrade never starts merely
because a row was marked.

List headings show how many rows are visible out of the results loaded for the
current source/search. Selection counts include hidden rows and packages retained
from previous searches. `a` changes only visible rows; `B` and `U` review the entire
selection, including hidden packages.
Plans refresh catalog or upgrade inventory before review. Removed sources,
missing packages, and ambiguous matches are skipped individually. Failed inventory
or catalog lookups cannot authorize an operation.

Press `m` or `Shift+F10`, or right-click a package row, to open its contextual
actions. The menu reflects the current tab, backend capabilities, and known pin
state. Package-changing actions still open their usual confirmation or review.

`o` opens the package homepage and `c` opens release notes when the
selected package provides those links. `e` exports the visible rows to
CSV. `t` chooses a theme; `L` opens recent run results. `Esc` cancels
the current input or operation as appropriate, and `q` quits.

In recent runs, open a run and choose **Details** (or press Enter on a package)
to inspect timing, source, versions, install options, backend, CLI exit codes,
and diagnostic output (up to 16,384 characters per package). Use arrow keys,
Page Up/Down, Home/End, or scrollbars to navigate the details. **Retry selected**
also handles cancelled packages; **Retry failed** includes only failed entries.
Retries recheck the exact package/source, installed state, available version,
and pins as applicable, then show a review with ready and skipped entries.
**Details** in the review shows the full request, including advanced options.
Nothing runs until you approve that review. Original results stay in history.
Pin changes and older entries without a recorded request have no retry action.
An explicit version is skipped if the backend cannot confirm its availability.

Use **Copy**, **Save text**, or **Save JSON** in a run's diagnostic view to keep
the displayed package's report. Save lets you choose a file and asks before
replacing an existing file. Cancelling the save dialog or a failed save leaves the original file
and history intact. Clipboard errors stay in the viewer so you can save instead.

## Backend and source diagnostics

Press `D` or choose **Backend and source diagnostics** in the action menu.
The panel shows the active backend and version, process architecture, supported
verify/repair actions, any recorded COM fallback reason, active source filter,
and the most recent inventory load error in this session.

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
