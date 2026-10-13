# Scout for WinGet review context

Scout is a .NET 10 terminal app. `WinGetScout.csproj` builds a cross-platform mock/CLI target and a Windows COM target; shipping binaries are Native AOT. The portable ZIP must retain the WinGet in-process COM DLLs beside `wingetscout.exe`. The standalone EXE uses the `winget.exe` CLI backend.

Use `docs/architecture.md` for the backend and UI boundaries, `docs/usage.md`
for user-visible behavior, and `docs/index.md` for the other maintained guides.

For any Terminal.Gui update, inspect changed APIs used by `src/App.cs` and `Program.cs`, especially UI thread dispatch, callbacks, timers, shutdown, keyboard input, layout, and rendering. Suggest a focused regression test or manual TUI check for each plausible incompatibility. A successful data-only test run does not establish interactive rendering parity.

For any WinGet update, keep `Microsoft.WindowsPackageManager.ComInterop` and `Microsoft.WindowsPackageManager.InProcCom` on the same version. Review `src/ComBackend.cs` for AOT and COM behavior and `src/CliBackend.cs` for output parsing. The default portable Native AOT build uses a registration-free manifest and the in-process COM DLLs; the `WingetComMode=Identity` MSIX build needs package identity and `Microsoft.Management.Deployment.winmd` for out-of-process activation. An unpackaged AOT EXE, a bare manifest without in-process routing, or an identity build without the winmd does not activate that path. Never replace indexed WinRT collection access with `foreach`: AOT cannot generate the required projected `IIterable<T>` wrapper here. Set `AcceptSourceAgreements` on each source reference before compositing; setting it on a composite reference throws `E_ILLEGAL_STATE_CHANGE`. Distinguish read-only `--comdiag` and `--comsmoke` checks from install or upgrade validation.

Keep the progress callback attached until a WinGet COM operation completes. Clearing a completed operation's `Progress` property triggered an access violation in the in-process server; `src/ComBackend.cs` now retains the callback and keeps the projection alive through completion.

Run `dotnet test --project tests/WinGetScout.Tests.csproj`. For package updates, also publish the Windows x64 and ARM64 AOT targets and confirm the COM companion files are present. Report tests and limits accurately. Suggest concrete code or tests rather than a generic dependency summary. Do not auto-merge critical dependency PRs.
