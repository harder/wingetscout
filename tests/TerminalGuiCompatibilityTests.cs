using System.Drawing;
using Terminal.Gui.App;

namespace WinGetScout.Tests;

[Collection ("Terminal UI")]
public sealed class TerminalGuiCompatibilityTests
{
    [Theory]
    [InlineData (80)]
    [InlineData (100)]
    [InlineData (120)]
    [InlineData (140)]
    [InlineData (190)]
    public void TablesKeepVersionsVisibleWithoutGivingUnusedWidthToSource (int width)
    {
        string? previous = Environment.GetEnvironmentVariable ("DisableRealDriverIO");
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        try
        {
            Theme.Register ();
            using IApplication application = Application.Create ().Init (DriverRegistry.Names.ANSI);
            application.Driver!.SetScreenSize (width, 34);
            using App window = new (new MockBackend ()) { Width = Dim.Fill (), Height = Dim.Fill () };
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof (App).GetField ("_initialLoadDone", flags)!.SetValue (window, true);
            AppState state = (AppState)typeof (App).GetField ("_state", flags)!.GetValue (window)!;
            state.Mode = AppMode.Upgrades;
            state.Packages = [new () { Id = "7zip.7zip", Name = "7-Zip", Version = "26.04", AvailableVersion = "26.05", Source = "winget" }];
            state.CacheDetail ("7zip.7zip", new () { Id = "7zip.7zip", Name = "7-Zip", Version = "26.04", Source = "winget" });
            state.ApplyFilter ();
            SessionToken session = application.Begin (window)!;
            try
            {
                typeof (App).GetMethod ("RefreshTable", flags)!.Invoke (window, null);
                application.LayoutAndDraw ();
                FrameView list = (FrameView)typeof (App).GetField ("_listFrame", flags)!.GetValue (window)!;
                string[] lines = application.Driver.ToString ()!.Split ('\n');
                string row = Assert.Single (lines, line => line.Contains ("7-Zip") && line.Contains ("26.04"));
                string tableRow = row [..Math.Min (row.Length, list.Frame.Right + 1)];
                Assert.Contains ("26.05", tableRow);
                if (width >= 140)
                {
                    string header = Assert.Single (lines, line => line.Contains ("Name") && line.Contains ("Source"));
                    int source = header.IndexOf ("Source", StringComparison.Ordinal);
                    int left = header.LastIndexOf ('│', source);
                    int right = header.IndexOf ('│', source);
                    Assert.Equal (8, right - left - 1);
                }
            }
            finally { application.End (session); }
        }
        finally { Environment.SetEnvironmentVariable ("DisableRealDriverIO", previous); }
    }

    [Fact]
    public void CompactLinksStayOnOneRowAndOpenTheirCompleteDestination ()
    {
        string? previous = Environment.GetEnvironmentVariable ("DisableRealDriverIO");
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        try
        {
            using IApplication application = Application.Create ().Init (DriverRegistry.Names.ANSI);
            application.Driver!.SetScreenSize (32, 24);
            using Runnable window = new () { Width = Dim.Fill (), Height = Dim.Fill () };
            DetailPanel panel = new () { Width = Dim.Fill (), Height = Dim.Fill () };
            window.Add (panel);
            SessionToken session = application.Begin (window)!;
            try
            {
                const string url = "https://example.invalid/releases/v1.2_(arm64)?mode=preview&x=1";
                string? activated = null;
                panel.LinkActivated += (_, value) => activated = value;
                panel.SetDetail (new () { Id = "Example", Name = "Example", Version = "1", Homepage = url }, loading: false);
                application.LayoutAndDraw ();
                Markdown link = Assert.Single (panel.SubViews.OfType<Markdown> ());
                Assert.Equal (1, link.Frame.Height);
                Assert.False (link.HorizontalScrollBar.Visible);
                Assert.False (link.VerticalScrollBar.Visible);
                Assert.Contains ("Homepage ↗", application.Driver.ToString ());
                Assert.DoesNotContain ("https://", application.Driver.ToString ());
                application.Mouse.RaiseMouseEvent (new Mouse
                {
                    ScreenPosition = link.ViewportToScreen (new Point (2, 0)), Flags = MouseFlags.LeftButtonClicked
                });
                application.LayoutAndDraw ();
                Assert.Equal (url, activated);
            }
            finally { application.End (session); }
        }
        finally { Environment.SetEnvironmentVariable ("DisableRealDriverIO", previous); }
    }

    [Theory]
    [InlineData (AppMode.Search)]
    [InlineData (AppMode.Upgrades)]
    public void CompactDetailsPrioritizeDescriptionAndOmitDuplicateOrOpaqueMetadata (AppMode mode)
    {
        string? previous = Environment.GetEnvironmentVariable ("DisableRealDriverIO");
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        try
        {
            using IApplication application = Application.Create ().Init (DriverRegistry.Names.ANSI);
            application.Driver!.SetScreenSize (34, 24);
            using Runnable window = new () { Width = Dim.Fill (), Height = Dim.Fill () };
            DetailPanel panel = new () { Width = Dim.Fill (), Height = Dim.Fill (), Mode = mode };
            window.Add (panel);
            SessionToken session = application.Begin (window)!;
            try
            {
                panel.SetDetail (new ()
                {
                    Id = "7zip.7zip", Name = "7-Zip", Version = mode == AppMode.Search ? "26.05" : "26.04",
                    AvailableVersion = "26.05", InstalledVersion = "26.04", Publisher = "Igor Pavlov", Author = "Igor Pavlov",
                    Source = "winget", License = "LGPL-2.1", Copyright = "Copyright © 1999-2026 Igor Pavlov.",
                    Tags = ["7zfiles", "archive", "archiver", "compress", "compression", "decompress", "decompression"],
                    PackageFamilyNames = ["OpaqueInstallerIdentity_8wekyb3d8bbwe"],
                    Description = "A file archiver with a high compression ratio.",
                    Homepage = "https://7-zip.org/download.html",
                    ReleaseNotesUrl = "https://github.com/ip7z/7zip/releases/tag/26.05"
                }, loading: false);
                application.LayoutAndDraw ();
                string screen = application.Driver.ToString ()!;
                Assert.Contains ("A file archiver", screen);
                Assert.DoesNotContain ("Author:", screen);
                Assert.DoesNotContain ("Copyright:", screen);
                Assert.DoesNotContain ("Family name:", screen);
                if (mode == AppMode.Search) Assert.DoesNotContain ("Available:", screen);
                else Assert.DoesNotContain ("Tags:", screen);
            }
            finally { application.End (session); }
        }
        finally { Environment.SetEnvironmentVariable ("DisableRealDriverIO", previous); }
    }

    [Fact]
    public void LongUnbrokenIdentifiersKeepTheirTailAfterWrapping ()
    {
        string? previous = Environment.GetEnvironmentVariable ("DisableRealDriverIO");
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        try
        {
            using IApplication application = Application.Create ().Init (DriverRegistry.Names.ANSI);
            application.Driver!.SetScreenSize (32, 30);
            using Runnable window = new () { Width = Dim.Fill (), Height = Dim.Fill () };
            DetailPanel panel = new () { Width = Dim.Fill (), Height = Dim.Fill () };
            window.Add (panel);
            SessionToken session = application.Begin (window)!;
            try
            {
                panel.SetDetail (new () { Id = "Microsoft.VeryLongPackageIdentifier.WithAnImportantTailSuffix", Name = "Package", Version = "1" }, loading: false);
                application.LayoutAndDraw ();
                string joined = string.Concat (application.Driver.ToString ()!.Split ('\n').Select (line => line.Trim ('│', ' ', '\r')));
                Assert.Contains ("Microsoft.VeryLongPackageIdentifier.WithAnImportantTailSuffix", joined);
            }
            finally { application.End (session); }
        }
        finally { Environment.SetEnvironmentVariable ("DisableRealDriverIO", previous); }
    }

    [Fact]
    public void DetailsOmitProductCodesWhileKeepingPackageIdentityAndUsefulMetadata ()
    {
        string? previous = Environment.GetEnvironmentVariable ("DisableRealDriverIO");
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        try
        {
            using IApplication application = Application.Create ().Init (DriverRegistry.Names.ANSI);
            application.Driver!.SetScreenSize (100, 30);
            using Runnable window = new () { Width = Dim.Fill (), Height = Dim.Fill () };
            DetailPanel panel = new () { Width = Dim.Fill (), Height = Dim.Fill () };
            window.Add (panel);
            SessionToken session = application.Begin (window)!;
            try
            {
                panel.SetDetail (new ()
                {
                    Id = "Example.Tool", Name = "Example Tool", Version = "1", Publisher = "Example Publisher",
                    ProductCodes = ["InstallerIdentity", "{23170F69-40C1-2701-2605-000001000000}"],
                    Homepage = "https://example.invalid/home", Description = "A useful package description."
                }, loading: false);
                foreach (int width in new [] { 100, 64 })
                {
                    application.Driver.SetScreenSize (width, 30);
                    application.LayoutAndDraw ();
                    string screen = application.Driver.ToString ()!;
                    Assert.Contains ("Id: Example.Tool", screen);
                    Assert.Contains ("Publisher: Example Publisher", screen);
                    Assert.Contains ("Homepage ↗", screen);
                    Assert.Contains ("A useful package description.", screen);
                    Assert.DoesNotContain ("Product code", screen);
                    Assert.DoesNotContain ("InstallerIdentity", screen);
                    Assert.DoesNotContain ("23170F69", screen);
                }
            }
            finally { application.End (session); }
        }
        finally { Environment.SetEnvironmentVariable ("DisableRealDriverIO", previous); }
    }

    [Fact]
    public void DetailLinksSurvivePartialRedrawScrollbarHoverAndResize ()
    {
        string? previous = Environment.GetEnvironmentVariable ("DisableRealDriverIO");
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        try
        {
            // Exercise Scout's real Markdown children against the ANSI screen buffer,
            // without connecting the test to the user's terminal or clipboard.
            using IApplication application = Application.Create ().Init (DriverRegistry.Names.ANSI);
            application.Driver!.SetScreenSize (100, 30);
            using Runnable window = new () { Width = Dim.Fill (), Height = Dim.Fill () };
            DetailPanel panel = new () { Width = Dim.Fill (), Height = Dim.Fill () };
            window.Add (panel);
            SessionToken session = application.Begin (window)!;
            try
            {
                panel.SetDetail (new ()
                {
                    Id = "Example.Linked", Name = "Linked package", Version = "1",
                    Homepage = "https://example.invalid/home",
                    ReleaseNotesUrl = "https://example.invalid/releases",
                    Description = string.Join ('\n', Enumerable.Repeat ("Long package description", 80))
                }, loading: false);
                panel.ScrollToEnd ();
                application.LayoutAndDraw ();
                Markdown[] links = panel.SubViews.OfType<Markdown> ().ToArray ();
                Assert.Equal (2, links.Length);
                Assert.Contains ("Homepage ↗", application.Driver.ToString ());
                List<Cell> expected = SnapshotLinks (application.Driver, links);

                foreach (Markdown link in links)
                    link.SetNeedsDraw (new Rectangle (0, 0, 1, 1));
                application.LayoutAndDraw ();
                Assert.Equal (expected, SnapshotLinks (application.Driver, links));

                ScrollSlider slider = panel.VerticalScrollBar.Slider;
                Assert.True (slider.Visible);
                application.Mouse.RaiseMouseEvent (new Mouse
                {
                    ScreenPosition = slider.ViewportToScreen (Point.Empty), Flags = MouseFlags.PositionReport
                });
                application.LayoutAndDraw ();
                Assert.True (slider.MouseState.HasFlag (MouseState.In));
                Assert.Equal (expected, SnapshotLinks (application.Driver, links));

                application.Driver.SetScreenSize (64, 24);
                application.LayoutAndDraw ();
                panel.ScrollToEnd ();
                application.LayoutAndDraw ();
                Assert.Contains ("Homepage ↗", application.Driver.ToString ());
                Assert.Contains ("Release notes ↗", application.Driver.ToString ());
                panel.ScrollToStart ();
                application.LayoutAndDraw ();
                Assert.Contains ("Long package description", application.Driver.ToString ());
            }
            finally { application.End (session); }
        }
        finally { Environment.SetEnvironmentVariable ("DisableRealDriverIO", previous); }
    }

    private static List<Cell> SnapshotLinks (IDriver driver, IEnumerable<Markdown> links)
    {
        List<Cell> cells = [];
        foreach (Markdown link in links)
        {
            Rectangle area = link.ViewportToScreen (new Rectangle (Point.Empty, link.Viewport.Size));
            for (int y = area.Top; y < area.Bottom; y++)
                for (int x = area.Left; x < area.Right; x++)
                    cells.Add (driver.Contents! [y, x] with { IsDirty = false });
        }
        Assert.NotEmpty (cells);
        return cells;
    }
}
