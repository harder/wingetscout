# Contributing

Scout for WinGet is a terminal app for managing Windows packages. Contributions that fix bugs, improve parsing, sharpen the test suite, improve Terminal.Gui behavior, or add focused features are welcome.

## Dev setup

```bash
git clone https://github.com/harder/wingetscout
cd wingetscout
dotnet test --project tests/WinGetScout.Tests.csproj
dotnet run -f net10.0 -- --mock           # UI iteration, any host
```

Building the actual AOT binary requires a **Windows host** with Visual Studio Build Tools (C++ workload). See [Build from source](README.md#build-from-source).

## Working on a change

1. **Add a test first** when the change touches parser behavior, model semantics, or anything covered by `tests/ParserTests.cs`. Every existing test is anchored to a real bug — please keep that pattern.
2. **Check real WinGet output** when changing parsing logic. Add representative examples and document user-facing limitations in the relevant documentation.
3. **Run the suite** before opening a PR: `dotnet test --project tests/WinGetScout.Tests.csproj`.
4. **Check in before large new-feature PRs.** Open an issue or discuss the approach first for anything sizable so the design lands before the code does. Coordinate packaging and signing changes against the [current distribution plan](docs/signing-and-distribution.md).

The [architecture guide](docs/architecture.md) maps the backend, UI, workflow,
and release code before you change their boundaries.

Package-manager distribution targets WinGet only; keep packaging changes within
that scope unless another distribution channel is explicitly requested.

For UI changes, cover the interaction through the existing driverless UI sessions:
`BatchClarityUiTests` and `BatchExecutionUiTests` exercise selection, approval,
progress, refresh, and retry; `UxInteractionTests` covers cancellation and late
responses; `FullWorkflowUiTests` covers dialogs, options, and compact layouts;
`DiagnosticsUiTests` covers read-only reports and copy/save actions. Keep the
review-before-execute boundary intact. Use mock or controlled recording backends
for package-changing tests. Wait for abandoned workers and their UI cleanup while
the UI stays live, so shutdown cannot mask a late callback.

Update the [usage guide](docs/usage.md), README shortcuts, and
[website](docs/website.md) when controls change. Add versioned notes under `docs/releases/` for user-facing changes and clearly
distinguish unreleased features from published downloads. Refresh the mock captures
when the visible UI changes.

The repository selects Microsoft.Testing.Platform in `global.json`. Pass the project with
`--project` as shown above; the older positional `dotnet test tests/...csproj` form is not
accepted by this runner. IDE test discovery likewise requires Microsoft.Testing.Platform support.

The same file pins the .NET SDK for reproducible locked restores. When updating that pin
or the WinGet COM packages, install the new SDK and regenerate both application modes:

```powershell
dotnet restore tests/WinGetScout.Tests.csproj --use-lock-file
dotnet restore WinGetScout.csproj -p:WingetComMode=Identity --use-lock-file
```

Commit `packages.lock.json`, `packages.identity.lock.json`, and
`tests/packages.lock.json` if they changed. CI reads the SDK version from `global.json`
and restores both portable and MSIX Identity graphs in locked mode.

For Windows COM changes, publish the Native AOT target for the machine's architecture and
run `wingetscout.exe --comdiag` from the complete publish folder. The portable build
needs `WindowsPackageManager.dll` and `Microsoft.Management.Deployment.InProc.dll`
beside the executable; the MSIX Identity build needs package identity and
`Microsoft.Management.Deployment.winmd`. A framework-dependent run or a mock
smoke test does not validate either published activation path.

Run `wingetscout.exe --comsmoke` from that folder for read-only COM inventory,
search, details, versions, and installer-preview checks. This diagnostic uses COM
directly, returns nonzero on failure, and does not fall back to the CLI. It does
not validate package installation or removal. Both diagnostics can also run from
an installed Identity MSIX to check its activation path.

## Filing issues

- **Bugs**: include the failing scenario, OS + architecture (x64 vs arm64), and where possible a `--dump` trace (e.g. `wingetscout --dump search vscode > dump.txt`).
- **UI gaps**: describe the expected behavior, terminal host and window size, and include a screenshot when useful. For batches, include selected/hidden counts and whether the issue occurred in review, execution, results, or retry.
- **Operation or source failures**: use `L` for per-package diagnostics or `D` for backend/source checks. Both viewers can copy reports or save text/JSON to attach to the issue.
- **Terminal.Gui regressions**: include the version you upgraded from and to. The Terminal.Gui compatibility tests in `tests/ParserTests.cs` should ideally catch these — if a regression slipped through, an extra test for it is highly welcome.

## Code style

Mostly follow standard C# / .NET conventions. The project loosely mirrors [Terminal.Gui's style](https://github.com/tui-cs/Terminal.Gui/blob/develop/.claude/rules/formatting.md) — notable points:

- Space before parens: `Method ()`, `array [i]`, `if (...)`.
- Braces on next line (Allman style).
- `var` only for built-in types (`int`, `string`, `bool`, etc.). Explicit type for everything else.
- Blank line before `return` / `break` / `continue`, after control blocks.

These aren't CI-enforced; just match the surrounding code.

## License

By contributing you agree your work is licensed under the project's [MIT license](LICENSE).
