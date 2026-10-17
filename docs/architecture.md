# Architecture

Scout is a .NET 10 Terminal.Gui application with a shared UI and three package
backends. The Windows build publishes as Native AOT. Start with the
[README](../README.md) for installation and the [usage guide](usage.md) for the
interface.

## Entry and backend selection

`Program.cs` handles diagnostics and startup, creates the Terminal.Gui
application, and selects an `IBackend` from `src/Backend.cs`. `--mock` uses
sample packages, `--cli` forces the `winget` command, and `--com` requests
the WinGet COM API on Windows. The Windows default tries COM and falls back to
CLI if activation fails. The active backend is reported in the app header;
`--comdiag` checks activation without starting the UI.
`--comsmoke` runs read-only COM inventory, search, details, versions, and
installer-preview checks without fallback; it returns a nonzero exit code on failure.

`IBackend` is the boundary for search, installed and upgrade lists, package
details, versions, installer previews, operations, sources, and pins.
`src/ComBackend.cs` uses the structured WinGet API, while `src/CliBackend.cs`
invokes `winget` and parses its output. `src/MockBackend.cs` supports safe UI
iteration. COM has no pin management surface in this app, so pin operations
use the CLI even when COM handles the other package work.

## UI and state

`src/App.cs` owns the Terminal.Gui window, package table, detail pane,
filters, sorting, focus, and the two-row status bar. Compact layouts omit
secondary columns while retaining the active sort in the frame title.
`src/App.Workflows.cs` handles package sets, selected plans, reviewed retries,
and contextual actions. `src/PackageActions.cs` computes visible/total/hidden
selection counts and capability-aware menu entries. `src/AppState.cs` holds the current mode,
packages, filter, selection, sort, progress, and view generations.
`src/Ui.cs`, `src/DetailPanel.cs`, and `src/WorkflowDialogs.cs` provide the
smaller views and dialogs.

Backend calls run away from the UI loop. `BackgroundTaskTracker` owns
admission, cancellation, failure collection, and shutdown of that work.
`App.DispatchAsync` queues guarded UI changes; cancellation tokens and
generation counters prevent a superseded list or detail request from
replacing newer results. Detail loading is debounced when selection moves
quickly.

`ForegroundWorkflowCoordinator` reserves package-changing workflows and CSV
exports. Cancelling preflight releases its loading lease and reservation without
waiting for a non-cooperative backend; request identity guards keep its eventual
response and cleanup from affecting a replacement check. Each batch item has a
new progress generation so delayed reports cannot overwrite the next item's
status. Completed batches refresh inventory before opening results and remove
successful package marks while keeping unsuccessful selections.

The dispatch wrapper awaits Terminal.Gui's `InvokeAsync`, which settles queued
work when the final UI session ends and operates independently of user timers.

`src/ApplicationLifetime.cs` owns terminal initialization, the UI run loop,
bounded background shutdown, and independent window/application disposal.
Failures retain their stack traces and are printed after terminal cleanup.
Direct window disposal stops UI callback admission before disposing child views.

## Workflows and local data

`src/BatchWorkflows.cs` builds reviewed install and upgrade plans.
`src/WorkflowData.cs` persists package sets, run history, and update-check
settings under `%LOCALAPPDATA%\WinGetScout`. `src/UpdateChecks.cs` runs
read-only checks and manages the optional current-user scheduled task.
`src/CsvExporter.cs` exports the visible list. CLI subprocesses run through
`src/ProcessRunner.cs`, which bounds output and owns process cleanup.

`src/RunWorkflows.cs` captures package requests and diagnostic outcomes, then
builds retry plans from those requests. Retries resolve the exact ID/source,
recheck installed/version/pin state, and pass through the normal plan review.
History retains original results; old entries without a request remain readable
but cannot be retried. The run viewer lists the latest 20 records.

`src/App.Diagnostics.cs` owns read-only diagnostic sessions and guarded copy/save
actions. `src/Diagnostics.cs` collects backend/source snapshots and writes bounded,
atomic text or source-generated JSON exports. Closing a viewer cancels its work;
late results cannot update a disposed dialog. These sessions do not reserve or
release foreground workflows, so read-only reports remain accessible during
operations, preflight, and CSV exports. Package operations retain their existing
confirmation and plan-review paths.

`src/InstallOptionPresentation.cs` describes backend defaults versus explicit
install overrides and detects known architecture/scope conflicts in installer
previews. Advanced options fetch a preview before the final confirmation; a
missing preview is disclosed instead of inferred to satisfy an override.

Mock history, sets, and checks live under `%LOCALAPPDATA%\WinGetScout\Mock`.
Mock sessions support manual update checks but cannot change scheduled tasks.
Mock upgrade results update only the session's sample inventory.

## Windows COM activation

The default `WingetComMode=InProc` Windows build uses `app.manifest` for
registration-free activation and ships `WindowsPackageManager.dll` plus
`Microsoft.Management.Deployment.InProc.dll` beside `wingetscout.exe`.
An unpackaged Native AOT executable alone cannot activate the in-box
out-of-process WinGet server; the standalone EXE therefore uses CLI.

The optional `WingetComMode=Identity` build uses
`app.identity.manifest`, MSIX package identity, and
`Microsoft.Management.Deployment.winmd` to reach the in-box server.
`packaging/build-msix.ps1` builds that package. The release and signing
plan is in [signing and distribution](signing-and-distribution.md).

The COM projection's WinRT collections must be copied by index before
using `foreach` or LINQ. Source agreements are accepted on each source
reference before a composite catalog is created. These rules are recorded
beside the implementation in `src/ComBackend.cs` and in
`.github/copilot-instructions.md`.

## Known COM limitations

Native ARM64 COM installs remain unresolved. On 2026-09-30, a native AOT COM
install of zoxide 0.10.0 with WinGet COM 1.29.380 repeatedly ended in
`0xC0000005` near completion, and the package was not installed. The same
install succeeded through the CLI backend. Read-only COM operations worked,
but do not treat them as validation of native ARM64 COM mutations.

Explicit COM architecture overrides replace WinGet's default
`AllowedArchitectures` order through indexed-vector method calls. The read-only
`--comsmoke` check verifies an explicit x64 PowerToys preview on both Windows
architectures. Advanced install confirmation and retry preflight reject a known
scope/architecture conflict in the selected installer preview. A missing preview
remains explicit and does not establish which installer execution will choose.

## Builds and releases

`WinGetScout.csproj` has a cross-platform `net10.0` target for mock and CLI
work and a Windows target that includes COM. `global.json` pins the SDK;
lock files cover the normal app, the MSIX Identity variant, and tests.
`.github/workflows/ci.yml` builds and tests, while
`.github/workflows/release.yml` publishes x64 and ARM64 assets,
checksums, and attestations. The portable ZIP keeps the COM companion
files together. See [release and WinGet updates](signing-and-distribution.md#releases-and-winget-updates)
for the community package workflow and the [website guide](website.md)
for the documentation site.
