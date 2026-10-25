# Scout for WinGet website

The public site at [wingetscout.com](https://wingetscout.com/) is built from `site/` with plain HTML, CSS, and JavaScript. [GitHub Pages](../.github/workflows/pages.yml) validates site changes in pull requests and deploys them when they reach `main`.

Run `python site/build.py` from the repository root to build `site/_build/` and check local files and anchors. The Pages workflow uses the same command.

## Refresh the product captures

The four theme PNGs and two short GIFs in `site/media/` come from the real app in
safe `--mock` mode and show sample packages.

The 1.1.1 captures use the frozen [catalog snapshot](../src/MockData/README.md),
with real package metadata and observed installed/available versions. Operations
remain simulated in safe `--mock` mode.

The search demo finds Neovim, marks it, opens Actions, reviews its ready-to-install
plan, and inspects Details. The upgrades demo marks two packages, reviews their
plan, inspects Details, and opens backend/source diagnostics. Both cancel the
plans without executing packages. The Moss still shows two marked upgrades,
the one-cell current/selected indicators, and the selection-aware footer.

Build the current branch, install [tuirec](https://github.com/tui-cs/tuirec),
then run on Windows:

```powershell
dotnet build WinGetScout.csproj -c Release -f net10.0
./site/capture-themes.ps1 -AppPath ./bin/Release/net10.0/wingetscout.exe
python site/build.py
```

Pass `-TuirecPath <path-to-tuirec.exe>` when it is not on `PATH`. The script records at 140 × 34 terminal cells with the application's current column layout. It temporarily enables true color for the child process. Inspect every capture before committing it, especially the text, selection state, and column widths. If a layout change ships in a new release, regenerate the media from that source and update any version-specific description at the same time.

The recorder also saves raw demo casts under `publish/capture-evidence/` for
checking the review and cancellation steps. Use `-EvidenceDirectory` to choose
a different evidence folder. Review representative GIF frames as well as each PNG.

Keep the homepage's new-release section, documentation, README shortcuts, and
[release notes](releases/1.1.1.md) aligned. Until a new release is published,
download buttons must still target the latest published
assets and main-only features must be labelled. The documentation sidebar and
section IDs are checked by the site build; check both pages at desktop and narrow
mobile widths after changing their contents.

The repository's Pages settings hold the custom domain and HTTPS configuration; `site/CNAME` and `site/sitemap.xml` record the public URL used by the build.
