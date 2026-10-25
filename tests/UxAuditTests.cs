using Terminal.Gui.App;

namespace WinGetScout.Tests;

[Collection ("Terminal UI")]
public sealed class UxAuditTests
{
    [Fact]
    public void CompactSearchGivesRecoveredSpaceToNamesBeforeRestoringTheIdColumn ()
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Search, 3);
        ui.Window.Width = 100;
        ui.Application.LayoutAndDraw ();
        ui.Invoke ("RefreshTable");
        string[] columns = ui.Table.Table!.ColumnNames;
        int name = Array.FindIndex (columns, label => label.StartsWith ("Name", StringComparison.Ordinal));
        int id = Array.FindIndex (columns, label => label.StartsWith ("Id", StringComparison.Ordinal));
        Assert.False (ui.Table.Style.GetOrCreateColumnStyle (id).Visible);
        Assert.True (ui.Table.Style.GetOrCreateColumnStyle (name).MinWidth >= 24);
    }

    [Theory]
    [InlineData (80, 24)]
    [InlineData (100, 30)]
    [InlineData (140, 40)]
    public void HelpNeedsOnlyVerticalScrollingAndKeepsItsCloseButtonVisible (int width, int height)
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Installed, 1);
        ui.Window.Width = width; ui.Window.Height = height;
        ui.Application.LayoutAndDraw ();
        using HelpDialog help = new ("COM · winget 1.29.380", "A long diagnostic fallback reason that must wrap rather than widen the help document.");
        help.Width = width * 9 / 10; help.Height = height * 85 / 100;
        SessionToken session = ui.Application.Begin (help)!;
        try
        {
            ui.Application.LayoutAndDraw ();
            View content = Assert.Single (help.SubViews, v => v is Code or Markdown);
            Assert.IsType<Markdown> (content);
            Assert.True (content.GetContentWidth () <= content.Viewport.Width,
                $"Help content {content.GetContentSize ()} exceeds viewport {content.Viewport} at {width}×{height}.");
            Button close = Assert.Single (help.SubViews.OfType<Button> ());
            Assert.False (content.Frame.IntersectsWith (close.Frame));
            string original = content.Text;
            content.SetFocus (); ui.Press (KeyCode.PageDown);
            Assert.True (content.Viewport.Y > 0);
            ui.Press (KeyCode.End);
            Assert.True (content.Viewport.Bottom >= content.GetContentSize ().Height);
            ui.Press (new Key ('z'));
            Assert.Equal (original, content.Text);
            ui.Press (KeyCode.Home);
            Assert.Equal (0, content.Viewport.Y);
        }
        finally { ui.Application.End (session); }
    }

    [Theory]
    [InlineData (80)]
    [InlineData (140)]
    public void ReviewRowsKeepVersionChangesVisibleAndButtonsSeparate (int width)
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1);
        ui.Window.Width = width; ui.Window.Height = 24;
        ui.Application.LayoutAndDraw ();
        Package package = new () { Id = "Microsoft.VisualStudio.Community", Name = "Visual Studio Community 2026", Version = "18.10.3", AvailableVersion = "18.11.1", Source = "winget" };
        using BatchPlanDialog review = new (new ("Upgrade", [new (package, true, "18.10.3 → 18.11.1")]));
        review.Width = width * 78 / 100; review.Height = 15;
        SessionToken session = ui.Application.Begin (review)!;
        try
        {
            ui.Application.LayoutAndDraw ();
            ListView rows = Assert.Single (review.SubViews.OfType<ListView> ());
            string row = Assert.IsType<string> (Assert.Single (rows.Source!.ToList ().Cast<object> ()));
            string visible = StatusOwnership.TruncateScalarSafe (row, rows.Viewport.Width);
            Assert.Contains ("18.10.3 → 18.11.1", visible);
            Assert.DoesNotContain (package.Id, row);
            Button[] buttons = review.SubViews.OfType<Button> ().ToArray ();
            for (int i = 0; i < buttons.Length; i++)
                for (int j = i + 1; j < buttons.Length; j++)
                    Assert.False (buttons [i].Frame.IntersectsWith (buttons [j].Frame), $"{buttons [i].Text} overlaps {buttons [j].Text}.");
        }
        finally { ui.Application.End (session); }
    }

    [Fact]
    public void ResultsKeepTheFailureReasonVisibleBeforeDiagnosticIdentifiers ()
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1);
        ui.Window.Width = 80; ui.Window.Height = 24;
        ui.Application.LayoutAndDraw ();
        RunItem item = new ("Microsoft.VisualStudio.Community", "Visual Studio Community 2026", "winget", "Failed", "Installer exited with 1603");
        using RunRecordDialog results = new (new (DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Upgrade", [item]));
        results.Width = 62; results.Height = 15;
        SessionToken session = ui.Application.Begin (results)!;
        try
        {
            ui.Application.LayoutAndDraw ();
            ListView rows = Assert.Single (results.SubViews.OfType<ListView> ());
            string row = Assert.IsType<string> (Assert.Single (rows.Source!.ToList ().Cast<object> ()));
            Assert.Contains ("Installer exited with 1603", StatusOwnership.TruncateScalarSafe (row, rows.Viewport.Width));
        }
        finally { ui.Application.End (session); }
    }

    [Fact]
    public void OneMarkedPackageUsesSingularWordingInTheActionMenu ()
    {
        Package package = new () { Id = "7zip.7zip", Name = "7-Zip", Source = "winget" };
        PackageAction clear = Assert.Single (PackageActions.For (AppMode.Upgrades, package, true, true, true, 1, null), a => a.Id == "clear-selection");
        Assert.Contains ("1 package)", clear.Label);
    }

    [Fact]
    public void CompactHelpRendersInstructionsWithoutMarkupOrHorizontalScrolling ()
    {
        string? previous = Environment.GetEnvironmentVariable ("DisableRealDriverIO");
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        try
        {
            using IApplication application = Application.Create ().Init (DriverRegistry.Names.ANSI);
            application.Driver!.SetScreenSize (80, 24);
            using HelpDialog help = new ("Mock backend");
            SessionToken session = application.Begin (help)!;
            try
            {
                application.LayoutAndDraw ();
                string screen = application.Driver.ToString ()!;
                Assert.Contains ("Navigation", screen);
                Assert.Contains ("Move through rows", screen);
                Assert.DoesNotContain ("##", screen);
                Assert.False (Assert.Single (help.SubViews.OfType<Markdown> ()).HorizontalScrollBar.Visible);
            }
            finally { application.End (session); }
        }
        finally { Environment.SetEnvironmentVariable ("DisableRealDriverIO", previous); }
    }

    [Fact]
    public async Task MockCatalogHasUniqueIdentitiesAndRealInstalledAndCatalogVersions ()
    {
        MockBackend backend = new ();
        IReadOnlyList<Package> search = await backend.SearchAsync (string.Empty, null, CancellationToken.None);
        Assert.All (search.GroupBy (BatchPlanning.Key), group => Assert.Single (group));
        Package installed = Assert.Single (await backend.ListInstalledAsync (null, CancellationToken.None), p => p.Id == "7zip.7zip");
        Package available = Assert.Single (search, p => p.Id == installed.Id);
        Assert.Equal ("26.04", installed.Version);
        Assert.Equal ("26.05", available.Version);
        Assert.Equal (installed.Version, available.InstalledVersion);
        PackageDetail detail = (await backend.ShowAsync (installed.Id, CancellationToken.None))!;
        Assert.Equal ("Igor Pavlov", detail.Publisher);
        Assert.True (detail.Tags?.Count > 10);
        Assert.Contains ("compression ratio", detail.Description);
        detail.Version = "changed";
        Assert.NotEqual (detail.Version, (await backend.ShowAsync (installed.Id, CancellationToken.None))!.Version);
        Assert.Contains (search, p => p.Source == "msstore");
    }
}
