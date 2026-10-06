# Scout for WinGet website

The public site at [wingetscout.com](https://wingetscout.com/) is built from this folder with plain HTML, CSS, and JavaScript. [GitHub Pages](../.github/workflows/pages.yml) validates site changes in pull requests and deploys them when they reach `main`.

Run `python site/build.py` from the repository root to build `site/_build/` and check local files and anchors. The Pages workflow uses the same command.

## Refresh the product captures

The four theme PNGs and two short GIFs in `site/media/` come from the real app in safe `--mock` mode. They show sample packages rather than someone's installed apps. Build the current branch, install [tuirec](https://github.com/tui-cs/tuirec), then run on Windows:

```powershell
dotnet build WinGetScout.csproj -c Release -f net10.0
./site/capture-themes.ps1 -AppPath ./bin/Release/net10.0/wingetscout.exe
python site/build.py
```

Pass `-TuirecPath <path-to-tuirec.exe>` when it is not on `PATH`. The script records at 140 × 34 terminal cells so the Upgrades Source column is visible. It temporarily enables true color for the child process. Inspect every capture before committing it, especially the text, selection state, and column widths. If a layout change ships in a new release, regenerate the media from that source and update any version-specific description at the same time.

The repository's Pages settings hold the custom domain and HTTPS configuration; `site/CNAME` and `site/sitemap.xml` record the public URL used by the build.
