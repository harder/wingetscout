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

`IBackend` is the boundary for search, installed and upgrade lists, package
details, versions, installer previews, operations, sources, and pins.
`src/ComBackend.cs` uses the structured WinGet API, while `src/CliBackend.cs`
invokes `winget` and parses its output. `src/MockBackend.cs` supports safe UI
iteration. COM has no pin management surface in this app, so pin operations
use the CLI even when COM handles the other package work.

## UI and state

`src/App.cs` owns the Terminal.Gui window, package table, detail pane,
filters, sorting, focus, and status bar. `src/App.Workflows.cs` handles
package sets and install planning. `src/AppState.cs` holds the current mode,
packages, filter, selection, sort, progress, and view generations.
`src/Ui.cs`, `src/DetailPanel.cs`, and `src/WorkflowDialogs.cs` provide the
smaller views and dialogs.

Backend calls run away from the UI loop. `BackgroundTaskTracker` owns
admission, cancellation, failure collection, and shutdown of that work.
`App.DispatchAsync` queues guarded UI changes; cancellation tokens and
generation counters prevent a superseded list or detail request from
replacing newer results. Detail loading is debounced when selection moves
quickly.

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

On ARM64, choosing x64 for Microsoft.PowerToys in advanced install still showed
an ARM64 installer in the COM preview. `ApplyInstallSettings` appends the
requested architecture to WinGet's existing `AllowedArchitectures` vector
(`src/ComBackend.cs`), leaving ARM64 first; the x64 override may therefore be
ignored. Do not assume the requested architecture is honored.

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
