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
2. **Check real WinGet output** when changing parsing logic. Add representative examples and document UI limitations in [feature-gaps.md](feature-gaps.md).
3. **Run the suite** before opening a PR: `dotnet test --project tests/WinGetScout.Tests.csproj`.
4. **Check in before large new-feature PRs.** Open an issue or discuss the approach first for anything sizable so the design lands before the code does. Coordinate packaging and signing changes against the [current distribution plan](code-signing.md).

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

## Filing issues

- **Bugs**: include the failing scenario, OS + architecture (x64 vs arm64), and where possible a `--dump` trace (e.g. `wingetscout --dump search vscode > dump.txt`).
- **UI gaps**: describe the expected behavior and include a screenshot when useful.
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
