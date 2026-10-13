# Using Scout for WinGet

For installation and a quick start, see the [README](../README.md#install).
Press `?` in Scout for the current in-app shortcut reference. This guide
explains the main workflows and where their results are stored.

## Navigate and find packages

The **Search**, **Installed**, and **Upgrades** tabs share a package list and
detail pane. Use `←`/`→` or `1`/`2`/`3` to switch tabs, `↑`/`↓` or
`j`/`k` to move through rows, and `Tab` to move focus between list and
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

On Search or Upgrades, `Space` marks a row and `a` toggles all visible
rows. `B` reviews selected Search results before installation; `U`
reviews selected upgrades before running them. The review shows items
that cannot be run and why. An install or upgrade never starts merely
because a row was marked.

`o` opens the package homepage and `c` opens release notes when the
selected package provides those links. `e` exports the visible rows to
CSV. `t` chooses a theme; `L` opens recent run results. `Esc` cancels
the current input or operation as appropriate, and `q` quits.

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
