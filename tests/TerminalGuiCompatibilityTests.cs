using System.Collections.ObjectModel;
using System.Drawing;
using Terminal.Gui.App;

namespace WinGetScout.Tests;

[Collection ("Terminal UI")]
public sealed class TerminalGuiCompatibilityTests
{
    [Theory]
    [InlineData ("plan")]
    [InlineData ("results")]
    public void ScrolledSummaryDialogsKeepSelectionVisibleWhenRowsAreReformatted (string kind)
    {
        string? previousDriverIO = Environment.GetEnvironmentVariable ("DisableRealDriverIO");
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        try
        {
            using IApplication application = Application.Create ().Init (DriverRegistry.Names.ANSI);
            application.Driver!.SetScreenSize (100, 30);
            Package[] packages = Enumerable.Range (0, 60)
                .Select (i => new Package { Id = $"Example.{i}", Name = $"Pkg{i:D3}", Source = "winget" }).ToArray ();
            using Runnable dialog = kind == "plan"
                ? new BatchPlanDialog (new ("Install", packages.Select (p => new BatchPlanItem (p, true, "Ready")).ToList ()))
                : new RunRecordDialog (new (DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Install",
                    packages.Select (p => new RunItem (p.Id, p.Name, p.Source, "Succeeded", "Done")).ToList ()));
            dialog.Width = 80;
            dialog.Height = 20;
            SessionToken session = application.Begin (dialog)!;
            try
            {
                ListView list = Assert.Single (dialog.SubViews.OfType<ListView> ());
                list.SetFocus ();
                application.Keyboard.RaiseKeyDownEvent (new (KeyCode.End));
                application.LayoutAndDraw ();
                Assert.Equal (59, list.SelectedItem);
                Assert.True (list.Viewport.Y > 0);

                // Summary rows replace their source when width changes. Upstream now
                // resets that source's viewport, so exercise Scout's selection restore.
                dialog.Width = 54;
                dialog.Height = 12;
                application.LayoutAndDraw ();
                Assert.Equal (59, list.SelectedItem);
                Assert.InRange (59, list.Viewport.Y, list.Viewport.Bottom - 1);
                Assert.Contains ("Pkg059", application.Driver.ToString ());
                Assert.True (list.HasFocus);
            }
            finally { application.End (session); }
        }
        finally { Environment.SetEnvironmentVariable ("DisableRealDriverIO", previousDriverIO); }
    }

    [Fact]
    public void HiddenPlanApprovalCannotBeTriggeredByItsLocalHotKey ()
    {
        using IApplication application = Application.Create ();
        using BatchPlanDialog dialog = new (new ("Install",
            [new (new () { Id = "Example.Package", Name = "Example package" }, true, "Ready")]))
        { Width = 80, Height = 20 };
        Button install = Assert.Single (dialog.SubViews.OfType<Button> (), button => button.Text.Contains ("Install"));
        SessionToken session = application.Begin (dialog)!;
        try
        {
            // The shared hotkey handler now permits hidden application-scoped shortcuts.
            // Scout's local approval button must still require visibility.
            install.Visible = false;
            application.LayoutAndDraw ();
            application.Keyboard.RaiseKeyDownEvent (new (KeyCode.I | KeyCode.AltMask));
            Assert.False (dialog.Result);

            install.Visible = true;
            application.LayoutAndDraw ();
            application.Keyboard.RaiseKeyDownEvent (new (KeyCode.I | KeyCode.AltMask));
            Assert.True (dialog.Result);
        }
        finally { application.End (session); }
    }

    [Theory]
    [InlineData ("detach")]
    [InlineData ("clear")]
    [InlineData ("replace")]
    [InlineData ("shrink")]
    public void PackageDialogListRecoversAfterSourceIsClearedOrShortened (string change)
    {
        string? previousDriverIO = Environment.GetEnvironmentVariable ("DisableRealDriverIO");
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        try
        {
            using IApplication application = Application.Create ().Init (DriverRegistry.Names.ANSI);
            application.Driver!.SetScreenSize (100, 30);
            ObservableCollection<string> versions = new (Enumerable.Range (0, 60).Select (i => $"{i}.0"));
            using VersionPickerDialog dialog = new ("Example", versions) { Width = 80, Height = 20 };
            ListView list = Assert.Single (dialog.SubViews.OfType<ListView> ());
            list.SetSource (versions);
            SessionToken session = application.Begin (dialog)!;
            try
            {
                list.SetFocus ();
                application.Keyboard.RaiseKeyDownEvent (new (KeyCode.End));
                application.LayoutAndDraw ();
                Assert.Equal (59, list.SelectedItem);
                Assert.True (list.Viewport.Y > 0);

                switch (change)
                {
                    case "detach": list.Source = null; break;
                    case "clear": list.SetSource<string> (null); break;
                    case "replace": list.SetSource (new ObservableCollection<string> (["Replacement"])); break;
                    case "shrink":
                        while (versions.Count > 3) versions.RemoveAt (versions.Count - 1);
                        Assert.Equal (2, list.SelectedItem);
                        versions.Clear ();
                        break;
                }

                application.LayoutAndDraw ();
                Assert.Null (list.SelectedItem);
                Assert.Equal (0, list.Viewport.Y);
                if (change == "detach") Assert.Null (list.Source);
                else Assert.Equal (change == "replace" ? 1 : 0, list.Source!.Count);

                // Reusing Scout's list must show the new rows and preserve its keyboard
                // boundary behavior after a previously scrolled source disappears.
                list.SetSource (new ObservableCollection<string> (["Recovered version"]));
                list.SelectedItem = 0;
                list.SetFocus ();
                application.Keyboard.RaiseKeyDownEvent (new (KeyCode.CursorDown));
                application.LayoutAndDraw ();
                Assert.Equal (0, list.SelectedItem);
                Assert.True (list.HasFocus);
                Assert.Contains ("Recovered version", application.Driver.ToString ());
                application.Keyboard.RaiseKeyDownEvent (new (KeyCode.Tab));
                Assert.IsType<Button> (dialog.MostFocused);
            }
            finally { application.End (session); }
        }
        finally { Environment.SetEnvironmentVariable ("DisableRealDriverIO", previousDriverIO); }
    }

    [Fact]
    public void BeginOnlyDetailSessionsPreserveCallerSynchronizationContext ()
    {
        string? previousDriverIO = Environment.GetEnvironmentVariable ("DisableRealDriverIO");
        SynchronizationContext? previousContext = SynchronizationContext.Current;
        SynchronizationContext callerContext = new ();
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        SynchronizationContext.SetSynchronizationContext (callerContext);
        try
        {
            using (IApplication application = Application.Create ().Init (DriverRegistry.Names.ANSI))
            {
                Assert.Same (callerContext, SynchronizationContext.Current);
                using Runnable window = new () { Width = Dim.Fill (), Height = Dim.Fill () };
                window.Add (new DetailPanel { Width = Dim.Fill (), Height = Dim.Fill () });
                SessionToken session = application.Begin (window)!;
                try
                {
                    // Scout's driverless UI sessions pump explicitly. Begin must not install
                    // a context that would capture awaits without a running main loop.
                    Assert.Same (callerContext, SynchronizationContext.Current);
                    using Dialog nested = new () { Title = "Details", Width = 40, Height = 10 };
                    SessionToken nestedSession = application.Begin (nested)!;
                    try { Assert.Same (callerContext, SynchronizationContext.Current); }
                    finally { application.End (nestedSession); }
                    Assert.Same (callerContext, SynchronizationContext.Current);
                }
                finally { application.End (session); }
                Assert.Same (callerContext, SynchronizationContext.Current);
            }
            Assert.Same (callerContext, SynchronizationContext.Current);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext (previousContext);
            Environment.SetEnvironmentVariable ("DisableRealDriverIO", previousDriverIO);
        }
    }

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
