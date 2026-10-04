# Scout for WinGet review context

Scout is a .NET 10 terminal app. `WinGetScout.csproj` builds a cross-platform mock/CLI target and a Windows COM target; shipping binaries are Native AOT. The portable ZIP must retain the WinGet in-process COM DLLs beside `wingetscout.exe`. The standalone EXE uses the `winget.exe` CLI backend.

For any Terminal.Gui update, inspect changed APIs used by `src/App.cs` and `Program.cs`, especially UI thread dispatch, callbacks, timers, shutdown, keyboard input, layout, and rendering. Suggest a focused regression test or manual TUI check for each plausible incompatibility. A successful data-only test run does not establish interactive rendering parity.

For any WinGet update, keep `Microsoft.WindowsPackageManager.ComInterop` and `Microsoft.WindowsPackageManager.InProcCom` on the same version. Review `src/ComBackend.cs` for AOT and COM behavior and `src/CliBackend.cs` for output parsing. Never replace indexed WinRT collection access with `foreach`: AOT cannot generate the required projected `IIterable<T>` wrapper here. Do not set `AcceptSourceAgreements` on a composite catalog reference. Distinguish read-only `--comdiag` and `--comsmoke` checks from install or upgrade validation.

Run `dotnet test --project tests/WinGetScout.Tests.csproj`. For package updates, also publish the Windows x64 and ARM64 AOT targets and confirm the COM companion files are present. Report tests and limits accurately. Suggest concrete code or tests rather than a generic dependency summary. Do not auto-merge critical dependency PRs.
