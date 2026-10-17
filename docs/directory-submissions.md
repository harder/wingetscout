# Other places to list Scout for WinGet

This page contains the submission copy and steps for Scout for WinGet 1.1.0. Use the current release and update any version-specific links before a later submission. The WinGet community package PR has been submitted, but the package is not available from the community source until that PR is merged and indexed.

The [1.1.0 release notes](releases/1.1.0.md) cover selection-aware batches,
per-package progress, reviewed retries, contextual actions, backend/source
diagnostics, and report exports. The website captures show the 1.1.0 UI with sample
packages; both demo plans are cancelled without executing packages.

## 1. Scoop

The ready-to-copy manifest is [packaging/scoop/wingetscout.json](../packaging/scoop/wingetscout.json). It uses the published x64 and ARM64 ZIPs, pins their SHA-256 hashes, and creates a `wingetscout` command shim. Its `checkver` and `autoupdate` fields allow a Scoop bucket's updater to propose future version bumps. Scout keeps its own user data under `%LOCALAPPDATA%\WinGetScout`, so this manifest does not need Scoop's `persist` field. The ZIP's `wingetscout/` directory keeps the executable and COM DLLs together.

### Use and test the manifest now

On a Windows machine with Scoop installed, in this repository:

```powershell
scoop install ./packaging/scoop/wingetscout.json
wingetscout --smoke
wingetscout --comdiag
scoop uninstall wingetscout
```

Use a new PowerShell session if the shim is not visible immediately. Test both x64 and ARM64 when those machines are available. If a local WinGet installation of Scout is already present, avoid installing a second copy for normal use; test in a disposable Windows environment or uninstall the local package after the check. Confirm the name Scoop assigned with `scoop list` before uninstalling.

### Submit to Scoop Extras later

Scoop Extras' current package request form asks for an indication of popularity and gives roughly **100 GitHub stars or 50 forks** as examples of its acceptance bar. Scout currently has far fewer. Wait until it meets the published criterion or a maintainer explicitly invites the package. The default Scoop Main bucket has an even higher bar. A separate public Scoop bucket can host this manifest earlier if desired.

When eligible:

1. Search [Extras issues](https://github.com/ScoopInstaller/Extras/issues) and [pull requests](https://github.com/ScoopInstaller/Extras/pulls) for `wingetscout`.
2. Open an Extras [package request](https://github.com/ScoopInstaller/Extras/issues/new/choose) and fill the fields with the name, short description, homepage, both release ZIP links, and genuine popularity evidence. Do not claim stars or forks that the repository lacks.
3. Wait for a maintainer's response. Fork `ScoopInstaller/Extras`, put `wingetscout.json` in its `bucket/` directory, and open a PR titled `wingetscout: Add version 1.1.0`.
4. Run the install/uninstall test above against the manifest in that fork. After opening the PR, comment `/verify` to start Scoop's manifest verifier, then address any review feedback.

Copy for the package request:

> **Name:** Scout for WinGet (`wingetscout`)<br>
> **Description:** Interactive Windows terminal app for searching, inspecting, installing, and updating WinGet packages. It also supports pins, reviewed batch actions and retries, run diagnostics, saved sets, and scheduled update checks.<br>
> **Homepage:** https://wingetscout.com/<br>
> **x64 ZIP:** https://github.com/harder/wingetscout/releases/download/v1.1.0/wingetscout-1.1.0-win-x64.zip<br>
> **ARM64 ZIP:** https://github.com/harder/wingetscout/releases/download/v1.1.0/wingetscout-1.1.0-win-arm64.zip<br>
> **Source:** https://github.com/harder/wingetscout<br>
> **Popularity/repute:** Add verifiable usage, reviews, stars, or forks when eligible.

Official references: [Extras criteria and package request](https://github.com/ScoopInstaller/Extras/blob/master/.github/ISSUE_TEMPLATE/package-request.yml), [Scoop bucket contribution guide](https://github.com/ScoopInstaller/.github/blob/main/.github/CONTRIBUTING.md), and [manifest fields](https://github.com/ScoopInstaller/Scoop/wiki/App-Manifests).

## 2. Awesome TUIs

1. Search the [list](https://github.com/rothgar/awesome-tuis) and its open PRs for `wingetscout` and `Scout for WinGet`.
2. Fork the repository, edit `README.md`, and insert this entry alphabetically in the **Productivity** section's Markdown list. Its maintainers ask for interactive, maintained terminal programs that do not simply wrap another interactive program. Scout is an interactive interface over WinGet's API and CLI.
3. Open a PR titled `Add Scout for WinGet` and link to the website or screenshot in the description.

Ready-to-paste README entry:

```markdown
- [Scout for WinGet](https://github.com/harder/wingetscout) Interactive Windows TUI for finding, installing, and updating WinGet packages.
```

Suggested PR body:

> Adds Scout for WinGet, an open-source Windows terminal interface for WinGet. It supports package search and details, install and upgrade workflows, pins, saved sets, and update checks. The repository includes releases and screenshots: https://wingetscout.com/.

## 3. Terminal Trove

Use [Terminal Trove's submission form](https://terminaltrove.com/submit/). It prefers cross-platform tools but does not require them; state clearly that Scout runs on Windows. The form requires a PNG preview and install instructions that refer to an existing package repository. **Wait for the WinGet PR to be merged and for `winget install -e --id Harder.WinGetScout` to work from the community source**, then use the fields below. Check its full list first to avoid a duplicate. The normal submission queue is free.

| Field | Copy or select |
| --- | --- |
| Name | Scout for WinGet |
| URL | wingetscout.com |
| Tagline | Find and manage WinGet packages from an interactive Windows terminal app. |
| Source code | https://github.com/harder/wingetscout |
| Primary language | C# |
| License | MIT |
| Project type | Open source |
| Preview PNG | https://wingetscout.com/media/scout-sage-search.png |
| Preview GIF | https://wingetscout.com/media/scout-search-demo.gif |
| Categories | `tui`, `cli`, `package-managers`, `windows`, `system-tools` |
| Install | Windows / WinGet: `winget install -e --id Harder.WinGetScout` (after published) |
| Author | Yes; supply your own contact email in the form |

**Description (100–400 characters):**

> Scout for WinGet is an interactive terminal app for managing Windows software through WinGet. Search packages, inspect details, install, upgrade, uninstall, and manage pins in one keyboard-friendly interface. It uses the WinGet COM API when available and falls back to the CLI.

**2–3 standout features (60–400 characters):**

> Review selected installs or upgrades with per-package progress; inspect run diagnostics and retry unfinished requests after fresh review; copy or save backend and source reports.

**What makes this different (50–500 characters):**

> Scout keeps package lists, details, and actions together in a terminal UI, reducing command memorization and context switching. It combines WinGet's structured COM API with a CLI fallback, keeps selection scope and progress visible, and offers diagnostic reports and reviewed retries for unfinished work.

**Who is this for / when to use it (60–300 characters):**

> Windows users who prefer a terminal and want to discover packages, compare details, review upgrades, and manage installed software without repeatedly composing WinGet commands.

The screenshots and GIF show sample packages captured in safe mock mode. Label them as demos if the form offers a notes field. Terminal Trove's form asks for a personal email; that must be supplied by you.

## 4. AlternativeTo

1. Create or sign in to an [AlternativeTo](https://alternativeto.net/) account and verify its email address.
2. Open the top-right user menu and choose **Suggest new application**. Search first so you do not create a duplicate.
3. Fill the application fields below. Use the **Official website** and **Source code** fields for links; AlternativeTo forbids links in the prose description.
4. Submit for review. After approval, use **Contribute to this page → Suggest Alternatives** on relevant package-manager interfaces if they genuinely share Scout's main use case. Avoid presenting Scout as a replacement for WinGet's package engine.

| Field | Copy or select |
| --- | --- |
| Name | Scout for WinGet |
| Official website | https://wingetscout.com/ |
| Source code | https://github.com/harder/wingetscout |
| Platform | Windows |
| License | Free and Open Source (MIT) |
| Creator/developer | Kevin Harder |
| Tags, if offered | WinGet, package manager, terminal, TUI, software updates |
| Icon, if requested | `site/favicon.svg` (vector and square) |
| Screenshot, if requested | `site/media/scout-sage-search.png` |

Suggested description, with no URLs:

> Scout for WinGet is a free, open-source terminal interface for Windows package management. Browse and search WinGet packages, inspect details, review installs and upgrades, manage installed apps and pins, and save package sets. It can check for updates on a schedule and show optional Windows notifications. Its keyboard-friendly layout keeps package information and actions together, while using the WinGet COM API where available and falling back to the WinGet CLI.

AlternativeTo's regular review queue may be slow. The site offers optional paid priority review, which speeds review but does not guarantee acceptance. Its [FAQ](https://alternativeto.net/faq/) explains the submission and review process.
