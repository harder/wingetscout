using System.Drawing;
using System.Reflection;
using Terminal.Gui.App;

namespace WinGetScout.Tests;

[Collection ("Terminal UI")]
public sealed class UxReviewRegressionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [Theory]
    [InlineData (AppMode.Search)]
    [InlineData (AppMode.Installed)]
    [InlineData (AppMode.Upgrades)]
    public void FilledEmptyAndRefilledListsKeepTheirMessageAndCompactColumns (AppMode mode)
    {
        WithScreen (140, 34, () => new App (new MockBackend ()) { Width = Dim.Fill (), Height = Dim.Fill () }, (application, root) =>
        {
            App window = (App)root;
            AppState state = Field<AppState> (window, "_state");
            TableView table = Field<TableView> (window, "_packageTable");
            Package package = new () { Id = "Example.Tool", Name = "Example", Version = "1", AvailableVersion = "2", Source = "winget" };
            state.Mode = mode;
            state.Packages = [package];
            state.CacheDetail (package.Id, new () { Id = package.Id, Name = package.Name, Version = "1", Source = "winget" });
            state.ApplyFilter ();
            Invoke (window, "RefreshTable");
            application.LayoutAndDraw ();
            state.LocalFilter = "no-such-package";
            state.ApplyFilter ();
            Invoke (window, "RefreshTable");
            application.LayoutAndDraw ();
            string expected = App.EmptyStateMessage (state);
            Assert.Contains (expected [..Math.Min (24, expected.Length)], application.Driver!.ToString ());
            state.LocalFilter = string.Empty;
            state.ApplyFilter ();
            Invoke (window, "RefreshTable");
            application.LayoutAndDraw ();
            Assert.Equal (1, table.Style.GetOrCreateColumnStyle (0).MaxWidth);
            Assert.False (table.Style.ExpandLastColumn);
            Assert.Contains ("Example", application.Driver.ToString ());
        });
    }

    [Theory]
    [InlineData ("https://example.invalid/path?name=&amp;&other=1")]
    [InlineData ("https://example.invalid/path?pattern=\\*.cs&value=&quot;")]
    public void LinkMarkupPreservesLiteralLabelsAndTheOriginalDestination (string url)
    {
        WithScreen (90, 30, PanelWindow, (application, root) =>
        {
            DetailPanel panel = Assert.Single (root.SubViews.OfType<DetailPanel> ());
            string? opened = null;
            panel.LinkActivated += (_, value) => opened = value;
            panel.SetDetail (new ()
            {
                Id = "Example", Name = "Example", Version = "1",
                Documentation = [new ("FAQ &amp; Windows", url)]
            }, false);
            application.LayoutAndDraw ();
            Assert.Contains ("FAQ &amp; Windows", application.Driver!.ToString ());
            Markdown link = Assert.Single (panel.SubViews.OfType<Markdown> ());
            application.Mouse.RaiseMouseEvent (new Mouse
            {
                ScreenPosition = link.ViewportToScreen (new Point (2, 0)), Flags = MouseFlags.LeftButtonClicked
            });
            application.LayoutAndDraw ();
            Assert.Equal (url, opened);
        });
    }

    [Fact]
    public void HelpTreatsBackendFailuresAsLiteralText ()
    {
        const string failure = "C:\\Tools\\_demo_\\*.dll &amp; [report](https://example.invalid)\n# literal heading";
        WithScreen (140, 34, () => new HelpDialog ("COM _backend_", failure), (application, _) =>
        {
            application.LayoutAndDraw ();
            string screen = application.Driver!.ToString ()!;
            Assert.Contains ("COM _backend_", screen);
            Assert.Contains ("_demo_", screen);
            Assert.Contains ("*.dll &amp; [report]", screen);
            Assert.Contains ("# literal heading", screen);
        });
    }

    [Theory]
    [InlineData (false, false)]
    [InlineData (false, true)]
    [InlineData (true, false)]
    [InlineData (true, true)]
    public void DuplicateReviewNamesKeepOnlyTheIdentityNeededToDistinguishThem (bool results, bool sameSource)
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1);
        Package first = new () { Id = sameSource ? "Publisher.One" : "Microsoft.WindowsTerminal", Name = "Windows Terminal", Source = "winget" };
        Package second = new () { Id = sameSource ? "Publisher.Two" : "9N0DX20HK701", Name = "Windows Terminal", Source = sameSource ? "winget" : "msstore" };
        using Runnable dialog = results
            ? new RunRecordDialog (new (DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Upgrade",
                [new (first.Id, first.Name, first.Source, "Failed", "Installer exited with 1603"),
                 new (second.Id, second.Name, second.Source, "Skipped", "Pinned")]))
            : new BatchPlanDialog (new ("Upgrade", [new (first, true, "1 → 2"), new (second, false, "Pinned")]));
        dialog.Width = 62; dialog.Height = 15;
        SessionToken session = ui.Application.Begin (dialog)!;
        try
        {
            ui.Application.LayoutAndDraw ();
            ListView list = Assert.Single (dialog.SubViews.OfType<ListView> ());
            string[] rows = list.Source!.ToList ().Cast<string> ().ToArray ();
            Assert.Contains (sameSource ? first.Id : first.Source, rows [0]);
            Assert.Contains (sameSource ? second.Id : second.Source, rows [1]);
            if (!sameSource) Assert.DoesNotContain (first.Id, rows [0]);
            Assert.All (rows, row => Assert.True (row.GetColumns () <= list.Viewport.Width));
        }
        finally { ui.Application.End (session); }
    }

    [Fact]
    public void MixedSourceAndSameSourceNameCollisionsRemainDistinct ()
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1);
        using PackageListView list = new () { Width = 70, Height = 5 };
        ui.Window.Add (list);
        ui.Application.LayoutAndDraw ();
        list.SetSummaryRows ([
            ("READY", "Terminal", "1 → 2", "Publisher.One", "winget"),
            ("READY", "Terminal", "1 → 2", "Publisher.Two", "winget"),
            ("READY", "Terminal", "1 → 2", "Store.Id", "msstore"),
            ("READY", "Unique", "1 → 2", "Unique.Id", "winget")]);
        string[] rows = list.Source!.ToList ().Cast<string> ().ToArray ();
        Assert.Contains ("[winget · Publisher.One] Terminal", rows [0]);
        Assert.Contains ("[winget · Publisher.Two] Terminal", rows [1]);
        Assert.Contains ("[msstore] Terminal", rows [2]);
        Assert.DoesNotContain ("Unique.Id", rows [3]);
        Assert.DoesNotContain ("winget", rows [3]);
    }

    [Theory]
    [InlineData (80)]
    [InlineData (100)]
    [InlineData (140)]
    public void FirstRenderedReviewFrameHasSeparateApprovalButtons (int width)
    {
        Package package = new () { Id = "Example.Tool", Name = "Example", Source = "winget" };
        WithScreen (width, 24, () => new BatchPlanDialog (new ("Upgrade", [new (package, true, "1 → 2")])), (application, root) =>
        {
            application.LayoutAndDraw ();
            Button[] buttons = root.SubViews.OfType<Button> ().ToArray ();
            for (int i = 0; i < buttons.Length; i++)
                for (int j = i + 1; j < buttons.Length; j++)
                    Assert.False (buttons [i].Frame.IntersectsWith (buttons [j].Frame));
            Assert.Contains ("Upgrade (1)", application.Driver!.ToString ());
            Assert.Contains ("Cancel", application.Driver.ToString ());
        });
    }

    [Theory]
    [InlineData (81)]
    [InlineData (84)]
    [InlineData (87)]
    public void NarrowUpgradesKeepVersionSpaceBeforeRestoringSource (int width)
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 3);
        ui.Window.Width = width;
        ui.Application.LayoutAndDraw ();
        ui.Invoke ("RefreshTable");
        string[] names = ui.Table.Table!.ColumnNames;
        int source = Array.FindIndex (names, name => name == "Source");
        int version = Array.FindIndex (names, name => name.StartsWith ("Version", StringComparison.Ordinal));
        Assert.False (ui.Table.Style.GetOrCreateColumnStyle (source).Visible);
        Assert.True (ui.Table.Style.GetOrCreateColumnStyle (version).MinWidth >= 14);
    }

    [Theory]
    [InlineData (AppMode.Search)]
    [InlineData (AppMode.Upgrades)]
    public void CurrentMarkedRowRemainsDistinctWithDetailsFocused (AppMode mode)
    {
        using ListNavigationTests.UiSession ui = new (mode, 3);
        ui.Press (new Key ('a'));
        ui.Table.Value = new (new (0, 1));
        ui.Detail.SetFocus ();
        ui.Application.LayoutAndDraw ();
        string current = Assert.IsType<string> (ui.Table.Table! [1, 0]);
        string other = Assert.IsType<string> (ui.Table.Table [0, 0]);
        Assert.NotEqual (current, other);
        Assert.Equal (1, current.GetColumns ());
        Assert.Equal ("✓", other);
    }

    [Fact]
    public async Task MockVersionsKeepTheNewestObservedVersionFirst ()
    {
        IReadOnlyList<string> versions = await new MockBackend ().ListVersionsAsync ("XPDC2RH70K22MN", CancellationToken.None, "msstore");
        Assert.Equal (new [] { "1.0.9262", "1.0.9188" }, versions);
    }

    [Fact]
    public void CatalogLoadsDoNotShareMutablePackageMetadata ()
    {
        PackageDetail[] first = MockCatalog.Load ();
        PackageDetail[] second = MockCatalog.Load ();
        first [0].Version = "changed";
        first [0].InstalledVersion = "changed";
        Assert.NotEqual (first [0].Version, second [0].Version);
        Assert.NotEqual (first [0].InstalledVersion, second [0].InstalledVersion);
        Assert.NotSame (first [0].Tags, second [0].Tags);
    }

    [Fact]
    public void MixedReadinessStatusesAlignTheirPackageNames ()
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1);
        using BatchPlanDialog dialog = new (new ("Upgrade",
            [new (new () { Id = "One", Name = "Alpha" }, true, "1 → 2"), new (new () { Id = "Two", Name = "Beta" }, false, "Pinned")]));
        SessionToken session = ui.Application.Begin (dialog)!;
        try
        {
            ui.Application.LayoutAndDraw ();
            string[] rows = Assert.Single (dialog.SubViews.OfType<ListView> ()).Source!.ToList ().Cast<string> ().ToArray ();
            Assert.Equal (rows [0].IndexOf ("Alpha", StringComparison.Ordinal), rows [1].IndexOf ("Beta", StringComparison.Ordinal));
        }
        finally { ui.Application.End (session); }
    }

    [Theory]
    [InlineData ("Support", "https://support.example.invalid/topic")]
    [InlineData ("Privacy", "https://example.invalid/privacy")]
    [InlineData ("Purchase", "https://example.invalid/buy")]
    [InlineData ("Guide", "https://docs.example.invalid/start")]
    public void SupplementalLinksAreReachableFromTheKeyboardWithoutExpandingDetails (string label, string expected)
    {
        string? launched = null;
        using ListNavigationTests.UiSession ui = new (AppMode.Installed, 1,
            urlLauncher: info => { launched = info.FileName; return null; });
        Package package = ui.State.Packages [0];
        PackageDetail detail = new ()
        {
            Id = package.Id, Name = package.Name, Source = package.Source,
            SupportUrl = "https://support.example.invalid/topic", PrivacyUrl = "https://example.invalid/privacy",
            PurchaseUrl = "https://example.invalid/buy", Documentation = [new ("Guide", "https://docs.example.invalid/start")]
        };
        ui.State.CacheDetail (package.Id, detail);
        ui.Invoke ("OnSelectedRowChanged");
        Assert.Contains (PackageActions.For (AppMode.Installed, package, true, true, true, 0, detail), action => action.Id == "links");
        int phase = 0;
        object? timer = ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (ui.Application.TopRunnable is not PackageActionDialog menu) return true;
            ListView list = Assert.Single (menu.SubViews.OfType<ListView> ());
            string[] items = list.Source!.ToList ().Cast<string> ().ToArray ();
            int choice = Array.FindIndex (items, item => item.StartsWith (phase == 0 ? "More links" : label, StringComparison.Ordinal));
            Assert.True (choice >= 0);
            list.SelectedItem = choice;
            phase++;
            ui.Press (KeyCode.Enter);
            return phase < 2;
        });
        try { ui.Press (new Key ('m')); }
        finally { if (timer is not null) ui.Application.RemoveTimeout (timer); }
        Assert.Equal (2, phase);
        Assert.Equal (expected, launched);
        Assert.True (ui.Table.HasFocus);
    }

    private static Runnable PanelWindow ()
    {
        Runnable root = new () { Width = Dim.Fill (), Height = Dim.Fill () };
        root.Add (new DetailPanel { Width = Dim.Fill (), Height = Dim.Fill () });
        return root;
    }

    private static T Field<T> (object owner, string name) => (T)owner.GetType ().GetField (name, PrivateInstance)!.GetValue (owner)!;
    private static void Invoke (object owner, string name) => owner.GetType ().GetMethod (name, PrivateInstance)!.Invoke (owner, null);

    private static void WithScreen (int width, int height, Func<Runnable> create, Action<IApplication, Runnable> inspect)
    {
        string? previous = Environment.GetEnvironmentVariable ("DisableRealDriverIO");
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        try
        {
            Theme.Register ();
            using IApplication application = Application.Create ().Init (DriverRegistry.Names.ANSI);
            application.Driver!.SetScreenSize (width, height);
            using Runnable root = create ();
            if (root is App app) typeof (App).GetField ("_initialLoadDone", PrivateInstance)!.SetValue (app, true);
            SessionToken session = application.Begin (root)!;
            try { inspect (application, root); }
            finally { application.End (session); }
        }
        finally { Environment.SetEnvironmentVariable ("DisableRealDriverIO", previous); }
    }
}
