using System.Reflection;
using Terminal.Gui.App;

namespace WinGetScout.Tests;

[CollectionDefinition ("Terminal UI", DisableParallelization = true)]
public sealed class TerminalUiCollection;

[Collection ("Terminal UI")]
public sealed class ListNavigationTests
{
    public static IEnumerable<object []> DialogCases () =>
        from kind in new [] { "versions", "sets", "history", "results", "plan" }
        from count in new [] { 0, 1, 60 }
        select new object [] { kind, count };

    [Theory]
    [MemberData (nameof (DialogCases))]
    public void DialogListsKeepValidRowsAndAllowTab (string kind, int count)
    {
        using IApplication application = Terminal.Gui.App.Application.Create ();
        List<string> versions = Enumerable.Range (0, count).Select (i => $"{i}.0").ToList ();
        List<RunItem> items = versions.Select (v => new RunItem (v, v, "winget", "Succeeded", "Done")).ToList ();
        RunRecord record = new (DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Install", items);
        using Runnable dialog = kind switch
        {
            "versions" => new VersionPickerDialog ("Example", versions),
            "sets" => new SetManagerDialog (versions.Select (v => new PackageSet (v, [])).ToArray (), 0),
            "history" => new RunHistoryDialog (versions.Select (_ => record).ToArray ()),
            "results" => new RunRecordDialog (record),
            _ => new BatchPlanDialog (new ("Install", versions.Select (v => new BatchPlanItem (
                new () { Id = v, Name = v }, true, "Ready")).ToList ()))
        };
        dialog.Width = 80;
        dialog.Height = 20;
        SessionToken session = application.Begin (dialog)!;
        try
        {
            ListView list = Assert.Single (dialog.SubViews.OfType<ListView> ());
            if (count > 0) list.SetFocus ();
            foreach (KeyCode key in new [] { KeyCode.CursorDown, KeyCode.CursorUp, KeyCode.PageDown, KeyCode.PageUp })
            {
                for (int i = 0; i < count + 5; i++)
                {
                    application.Keyboard.RaiseKeyDownEvent (new (key));
                    if (count > 0) Assert.InRange (list.SelectedItem!.Value, 0, count - 1);
                    else Assert.True (list.SelectedItem is null or -1);
                }
                if (count > 0) Assert.True (list.HasFocus);
            }
            application.Keyboard.RaiseKeyDownEvent (new (KeyCode.Tab));
            Assert.IsType<Button> (dialog.MostFocused);
        }
        finally { application.End (session); }
    }

    [Theory]
    [InlineData (AppMode.Installed, 0)]
    [InlineData (AppMode.Installed, 1)]
    [InlineData (AppMode.Installed, 80)]
    [InlineData (AppMode.Upgrades, 0)]
    [InlineData (AppMode.Upgrades, 1)]
    [InlineData (AppMode.Upgrades, 80)]
    [InlineData (AppMode.Search, 0)]
    [InlineData (AppMode.Search, 1)]
    [InlineData (AppMode.Search, 80)]
    public void ArrowsStayInPackageListAtBothEnds (AppMode mode, int count)
    {
        using UiSession ui = new (mode, count);
        int bubbledArrows = 0;
        ui.Window.KeyDown += (_, key) =>
        {
            if (key.KeyCode is KeyCode.CursorDown or KeyCode.CursorUp) bubbledArrows++;
        };
        for (int i = 0; i < count + 10; i++)
        {
            ui.Press (KeyCode.CursorDown);
            Assert.True (ui.Table.HasFocus, $"Down escaped {mode} list with {count} rows at press {i}");
            ui.AssertSelection ();
        }
        Assert.Equal (Math.Max (0, count - 1), ui.Table.Value!.SelectedCell.Y);
        for (int i = 0; i < count + 10; i++)
        {
            ui.Press (KeyCode.CursorUp);
            Assert.True (ui.Table.HasFocus, $"Up escaped {mode} list with {count} rows at press {i}");
            ui.AssertSelection ();
        }
        Assert.Equal (0, ui.Table.Value!.SelectedCell.Y);
        Assert.Equal (0, bubbledArrows);

        ui.Press (KeyCode.Tab);
        Assert.True (ui.Detail.HasFocus);
        ui.Press (KeyCode.CursorDown);
        Assert.True (ui.Detail.HasFocus);
        Assert.Equal (0, ui.Table.Value!.SelectedCell.Y);
        ui.Press (KeyCode.Tab | KeyCode.ShiftMask);
        Assert.True (ui.Table.HasFocus);
    }

    [Theory]
    [InlineData (AppMode.Installed, 0)]
    [InlineData (AppMode.Installed, 1)]
    [InlineData (AppMode.Installed, 80)]
    [InlineData (AppMode.Upgrades, 0)]
    [InlineData (AppMode.Upgrades, 1)]
    [InlineData (AppMode.Upgrades, 80)]
    [InlineData (AppMode.Search, 0)]
    [InlineData (AppMode.Search, 1)]
    [InlineData (AppMode.Search, 80)]
    public void AliasesPagesAndResizeKeepSelectionVisible (AppMode mode, int count)
    {
        using UiSession ui = new (mode, count);
        int last = Math.Max (0, count - 1);
        for (int i = 0; i < 100; i++) ui.Press (new Key ('j'));
        Assert.Equal (last, ui.Table.Value!.SelectedCell.Y);
        Assert.True (ui.Table.HasFocus, $"Focus: {ui.Window.MostFocused}; viewport: {ui.Table.Viewport}; frame: {ui.Window.Frame}");
        if (count > ui.Table.Viewport.Height)
            Assert.True (ui.Table.Viewport.Y > 0, $"Viewport: {ui.Table.Viewport}; rows: {ui.Table.Table!.Rows}");
        ui.Press (KeyCode.Home);
        Assert.Equal (0, ui.Table.Value.SelectedCell.Y);
        ui.Press (KeyCode.PageDown);
        Assert.InRange (ui.Table.Value.SelectedCell.Y, count > 1 ? 1 : 0, last);
        for (int i = 0; i < 20; i++) ui.Press (KeyCode.PageDown);
        Assert.Equal (last, ui.Table.Value.SelectedCell.Y);
        ui.Window.Width = 100;
        ui.Window.Height = 24;
        ui.Application.LayoutAndDraw ();
        ui.Press (KeyCode.CursorUp);
        ui.AssertSelection ();
        Assert.Equal (Math.Max (0, last - 1), ui.Table.Value.SelectedCell.Y);
        for (int i = 0; i < 100; i++) ui.Press (new Key ('k'));
        Assert.Equal (0, ui.Table.Value.SelectedCell.Y);
        ui.Press (KeyCode.End);
        ui.Press (KeyCode.PageUp);
        Assert.InRange (ui.Table.Value.SelectedCell.Y, 0, Math.Max (0, last - 1));
        for (int i = 0; i < 20; i++) ui.Press (KeyCode.PageUp);
        Assert.Equal (0, ui.Table.Value.SelectedCell.Y);
        Assert.True (ui.Table.HasFocus);
    }

    [Theory]
    [InlineData (AppMode.Installed)]
    [InlineData (AppMode.Upgrades)]
    public void FilteringCanWidenAndNavigateWithoutMovingInputFocus (AppMode mode)
    {
        using UiSession ui = new (mode, 80);
        ui.Press (KeyCode.End);
        ui.Press (new Key ('/'));
        TextField input = ui.Get<TextField> ("_filterInput");
        input.Text = "Package 079";
        Assert.Single (ui.State.Filtered);
        ui.AssertSelection ();
        foreach (string filter in new [] { "no matches", "Package 079", "" })
        {
            input.Text = filter;
            foreach (KeyCode code in new [] { KeyCode.CursorDown, KeyCode.CursorUp, KeyCode.PageDown, KeyCode.PageUp, KeyCode.Home, KeyCode.End })
            {
                for (int i = 0; i < 5; i++) ui.Press (code);
                Assert.True (input.HasFocus);
                ui.AssertSelection ();
            }
        }
        Assert.Equal (80, ui.State.Filtered.Count);
        Assert.True (ui.Table.Viewport.Y > 0);
        ui.Press (KeyCode.Esc);
        Assert.True (ui.Table.HasFocus);
        ui.Press (new Key ('S'));
        ui.AssertSelection ();
        ui.SetRows (1);
        ui.AssertSelection ();
        ui.SetRows (0);
        ui.AssertSelection ();
        ui.SetRows (80);
        ui.AssertSelection ();
        Assert.Null (ui.State.SelectedPackage (-1));
        Assert.Null (ui.State.SelectedPackage (int.MaxValue));
    }

    [Theory]
    [InlineData (AppMode.Installed)]
    [InlineData (AppMode.Upgrades)]
    [InlineData (AppMode.Search)]
    public void SourceCycleReloadsInventoryIncludingAllSources (AppMode mode)
    {
        NavigationBackend backend = new ();
        using UiSession ui = new (mode, 16, backend);
        ui.State.SearchQuery = "Package";
        foreach (string? source in new string? [] { "winget", "msstore", null })
        {
            ui.Press (new Key ('f'));
            ui.PumpUntil (() => !ui.State.Loading);
            Assert.Equal (source, ui.State.SourceFilter);
            Assert.Equal (source is null ? 16 : 8, ui.State.Packages.Count);
            Assert.All (ui.State.Packages, p => Assert.True (source is null || p.Source == source));
            ui.AssertSelection ();
        }
        Assert.Equal (new string? [] { "winget", "msstore", null }, backend.Sources.ToArray ());
        ui.Press (KeyCode.End);
        string selected = ui.State.CurrentDetail!.Id;
        ui.Press (new Key ('r'));
        ui.PumpUntil (() => !ui.State.Loading);
        Assert.Equal (selected, ui.State.CurrentDetail!.Id);
        ui.AssertSelection ();
        Assert.True (ui.Table.Viewport.Y > 0 || ui.Table.Table!.Rows < ui.Table.Viewport.Height);
    }

    [Fact]
    public async Task QueuedUiCallbacksAreRejectedAfterCancellationOrDisposal ()
    {
        using UiSession ui = new (AppMode.Installed, 1);
        int calls = 0;
        using CancellationTokenSource request = new ();
        Task cancelled = ui.Dispatch (() => calls++, request.Token);
        request.Cancel ();
        ui.Pump ();
        await cancelled;
        Assert.Equal (0, calls);

        bool current = true;
        Task superseded = ui.Dispatch (() => calls++, CancellationToken.None, () => current);
        current = false;
        ui.Pump ();
        await superseded;
        Assert.Equal (0, calls);

        Task disposed = ui.Dispatch (() => calls++, CancellationToken.None);
        ui.Window.Dispose ();
        // Lifetime-linked request cancellation must settle this without pumping the UI.
        await disposed.WaitAsync (TimeSpan.FromSeconds (3), TestContext.Current.CancellationToken);
        ui.Pump ();
        Assert.Equal (0, calls);
    }

    [Fact]
    public async Task AwaitableDispatchRunsDuringModalAndPropagatesFailures ()
    {
        using UiSession ui = new (AppMode.Installed, 1);
        using Runnable modal = new ();
        SessionToken nested = ui.Application.Begin (modal)!;
        int callbackThread = 0;
        Task pending = ui.Dispatch (() => callbackThread = Environment.CurrentManagedThreadId, CancellationToken.None);
        ui.PumpUntil (() => pending.IsCompleted);
        await pending;
        Assert.Equal (Environment.CurrentManagedThreadId, callbackThread);
        ui.Application.End (nested);

        Task failed = ui.Dispatch (() => throw new InvalidOperationException ("dispatch failure"), CancellationToken.None);
        ui.PumpUntil (() => failed.IsCompleted);
        Assert.Equal ("dispatch failure", (await Assert.ThrowsAsync<InvalidOperationException> (() => failed)).Message);

        Task unrelatedCancellation = ui.Dispatch (() => throw new OperationCanceledException ("action failure"), CancellationToken.None);
        ui.PumpUntil (() => unrelatedCancellation.IsCompleted);
        Assert.Equal ("action failure", (await Assert.ThrowsAsync<OperationCanceledException> (() => unrelatedCancellation)).Message);
    }

    [Fact]
    public async Task AwaitableDispatchPreservesActionFailureAfterShutdownStarts ()
    {
        using UiSession ui = new (AppMode.Installed, 1);
        Task pending = ui.Dispatch (() =>
        {
            ui.Invoke ("BeginShutdown");
            throw new InvalidOperationException ("failure during shutdown");
        }, CancellationToken.None);

        ui.PumpUntil (() => pending.IsCompleted);

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException> (() => pending);
        Assert.Equal ("failure during shutdown", failure.Message);
    }

    [Fact]
    public async Task AwaitableDispatchSettlesWhenFinalSessionEndsWithoutPumping ()
    {
        using UiSession ui = new (AppMode.Installed, 1);
        int calls = 0;
        Task pending = ui.Dispatch (() => calls++, CancellationToken.None);
        ui.Application.End (ui.Application.SessionStack!.Single ());
        await pending.WaitAsync (TimeSpan.FromSeconds (3), TestContext.Current.CancellationToken);
        Assert.Equal (0, calls);
    }

    [Fact]
    public void ReplacedDetailCancellationFailuresRemainDiagnosable ()
    {
        using UiSession ui = new (AppMode.Installed, 1);
        using CancellationTokenRegistration registration = ui.Get<CancellationTokenSource> ("_detailCts")
            .Token.Register (() => throw new InvalidOperationException ("detail cancellation failure"));

        ui.Invoke ("CancelPendingDetailLoad");

        Exception failure = Assert.Single (ui.Window.BackgroundFailures);
        Assert.Contains ("detail cancellation failure", failure.ToString ());
    }

    [Fact]
    public void LateInventoryCannotReplaceANewerRefresh ()
    {
        TaskCompletionSource<IReadOnlyList<Package>> late = new (TaskCreationOptions.RunContinuationsAsynchronously);
        int loads = 0;
        NavigationBackend backend = new ()
        {
            Load = (_, _) => Interlocked.Increment (ref loads) == 1
                ? late.Task : Task.FromResult<IReadOnlyList<Package>> ([new () { Id = "Latest", Name = "Latest", Version = "1" }])
        };
        using UiSession ui = new (AppMode.Installed, 1, backend);
        ui.State.CacheDetail ("Latest", new () { Id = "Latest", Name = "Latest" });
        ui.Press (new Key ('r'));
        ui.PumpUntil (() => backend.Sources.Count == 1);
        ui.Press (new Key ('r'));
        ui.PumpUntil (() => ui.State.Packages.FirstOrDefault ()?.Id == "Latest");
        late.SetResult ([new () { Id = "Old", Name = "Old", Version = "1" }]);
        ui.PumpUntil (() => !ui.State.Loading);
        Assert.Equal ("Latest", Assert.Single (ui.State.Packages).Id);
        ui.AssertSelection ();
    }

    [Fact]
    public void LateDetailCannotReplaceANewerSelection ()
    {
        TaskCompletionSource<PackageDetail?> late = new (TaskCreationOptions.RunContinuationsAsynchronously);
        int shows = 0;
        NavigationBackend backend = new ()
        {
            Show = (id, _) => Interlocked.Increment (ref shows) == 1 ? late.Task
                : Task.FromResult<PackageDetail?> (new () { Id = id, Name = "Latest detail" })
        };
        using UiSession ui = new (AppMode.Installed, 2, backend);
        ui.State.ClearCachedDetails ();
        ui.Invoke ("OnSelectedRowChanged");
        ui.PumpUntil (() => Volatile.Read (ref shows) == 1);
        ui.Press (KeyCode.CursorDown);
        ui.PumpUntil (() => ui.State.CurrentDetail?.Name == "Latest detail");
        late.SetResult (new () { Id = "Example.000", Name = "Old detail" });
        ui.PumpUntil (() => !ui.State.DetailLoading);
        Assert.Equal ("Latest detail", ui.State.CurrentDetail!.Name);
        ui.AssertSelection ();
    }

    internal sealed class UiSession : IDisposable
    {
        public IApplication Application { get; }
        public App Window { get; }
        public AppState State { get; }
        public TableView Table { get; }
        public DetailPanel Detail { get; }
        private readonly SessionToken _session;

        public UiSession (AppMode mode, int count, IBackend? backend = null)
        {
            Theme.Register ();
            // Begin and Keyboard use the same event/command pipeline as a running app.
            // A driverless application keeps this test independent of the host's terminal.
            Application = Terminal.Gui.App.Application.Create ();
            Window = new (backend ?? new MockBackend ()) { Width = 140, Height = 32 };
            Field ("_initialLoadDone").SetValue (Window, true);
            State = Get<AppState> ("_state");
            Table = Get<TableView> ("_packageTable");
            Detail = Get<DetailPanel> ("_detailPanel");
            State.Mode = mode;
            SetRows (count);
            _session = Application.Begin (Window)!;
            // No OS driver is needed when tests pump dispatch and timers on this thread.
            Application.Initialized = true;
            SetUiThread ();
            Application.LayoutAndDraw ();
            Table.SetFocus ();
        }

        public void SetRows (int count)
        {
            State.Packages = Enumerable.Range (0, count).Select (i => new Package
            {
                Id = $"Example.{i:D3}", Name = $"Package {i:D3}", Version = "1", AvailableVersion = "2",
                Source = i % 2 == 0 ? "winget" : "msstore"
            }).ToList ();
            foreach (Package p in State.Packages)
                State.CacheDetail (p.Id, new () { Id = p.Id, Name = p.Name, Version = p.Version });
            State.ApplyFilter ();
            Invoke ("RefreshTable");
        }

        public void Press (KeyCode code) => Press (new Key (code));

        public void Press (Key key)
        {
            Application.Keyboard.RaiseKeyDownEvent (key);
            Application.LayoutAndDraw ();
        }

        public void AssertSelection ()
        {
            int row = Table.Value?.SelectedCell.Y ?? -1;
            if (State.Filtered.Count == 0)
            {
                Assert.Null (State.SelectedPackage (row));
                Assert.Null (State.CurrentDetail);
            }
            else
            {
                Assert.InRange (row, 0, State.Filtered.Count - 1);
                Assert.Equal (State.Filtered [row].Id, State.CurrentDetail?.Id);
            }
        }

        public Task Dispatch (Action action, CancellationToken token, Func<bool>? isCurrent = null)
        {
            // Queue from a worker as real backend completions do; keep the test's UI thread
            // available to cancel, supersede, or end the session before the queue is pumped.
            SetUiThread ();
            using ManualResetEventSlim queued = new ();
            CancellationToken lifetimeToken = Get<BackgroundTaskTracker> ("_background").LifetimeToken;
            Task result = Task.Run (async () =>
            {
                // Production requests are linked when created, before calling DispatchAsync.
                using CancellationTokenSource request = CancellationTokenSource.CreateLinkedTokenSource (lifetimeToken, token);
                Task pending;
                try
                {
                    pending = (Task)typeof (App).GetMethod ("DispatchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke (Window, [action, request.Token, isCurrent])!;
                }
                finally { queued.Set (); }
                await pending.ConfigureAwait (false);
            });
            Assert.True (queued.Wait (TimeSpan.FromSeconds (3)));
            return result;
        }

        public void Pump ()
        {
            SetUiThread ();
            // Driverless equivalent of ApplicationMainLoop's dispatch pass.
            Application.GetType ().GetMethod ("DrainDispatches", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke (Application, null);
            Application.TimedEvents!.RunTimers ();
            Application.LayoutAndDraw ();
        }

        public void PumpUntil (Func<bool> complete)
        {
            System.Diagnostics.Stopwatch deadline = System.Diagnostics.Stopwatch.StartNew ();
            while (!complete () && deadline.Elapsed < TimeSpan.FromSeconds (3))
            {
                Pump ();
                Thread.Sleep (1);
            }
            Assert.True (complete (), "UI work did not complete within three seconds");
            Assert.Empty (Window.BackgroundFailures);
        }

        private void SetUiThread () => Application.GetType ().GetProperty ("MainThreadId")!
            .SetValue (Application, Environment.CurrentManagedThreadId);

        public T Get<T> (string name) => (T)Field (name).GetValue (Window)!;
        private static FieldInfo Field (string name) => typeof (App).GetField (name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        public void Invoke (string name, params object? [] args) => typeof (App)
            .GetMethod (name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke (Window, args);

        public void Dispose ()
        {
            Application.End (_session);
            Window.ShutdownAsync (TimeSpan.FromSeconds (2)).GetAwaiter ().GetResult ();
            try { Window.Dispose (); }
            finally { Application.Dispose (); }
        }
    }

    internal sealed class NavigationBackend : IBackend
    {
        private readonly MockBackend _mock = new ();
        public System.Collections.Concurrent.ConcurrentQueue<string?> Sources { get; } = new ();
        public Func<string?, CancellationToken, Task<IReadOnlyList<Package>>>? Load { get; set; }
        public Func<string, CancellationToken, Task<PackageDetail?>>? Show { get; set; }
        private Task<IReadOnlyList<Package>> Inventory (string? source, CancellationToken ct)
        {
            Sources.Enqueue (source);
            return Load?.Invoke (source, ct) ?? Task.FromResult<IReadOnlyList<Package>> (
                Enumerable.Range (0, 16).Select (i => new Package
                {
                    Id = $"Example.{i:D3}", Name = $"Package {i:D3}", Version = "1", AvailableVersion = "2",
                    Source = i % 2 == 0 ? "winget" : "msstore"
                }).Where (p => source is null || p.Source == source).ToArray ());
        }
        public Task<IReadOnlyList<Package>> SearchAsync (string query, string? source, CancellationToken ct) => Inventory (source, ct);
        public Task<IReadOnlyList<Package>> ListInstalledAsync (string? source, CancellationToken ct) => Inventory (source, ct);
        public Task<IReadOnlyList<Package>> ListUpgradesAsync (string? source, CancellationToken ct) => Inventory (source, ct);
        public Task<PackageDetail?> ShowAsync (string id, CancellationToken ct) => Show?.Invoke (id, ct) ?? _mock.ShowAsync (id, ct);
        public Task<IReadOnlyList<string>> ListSourcesAsync (CancellationToken ct) => _mock.ListSourcesAsync (ct);
        public Task<IReadOnlyDictionary<string, PinState>> ListPinsAsync (CancellationToken ct) => _mock.ListPinsAsync (ct);
        public Task<string> DescribeAsync (CancellationToken ct) => _mock.DescribeAsync (ct);
        public bool CanVerify => false;
        public bool CanRepair => false;
        public Task<IReadOnlyList<string>> ListVersionsAsync (string id, CancellationToken ct) => throw new NotSupportedException ();
        public Task<InstallerPreview?> GetInstallerPreviewAsync (string id, string? version, InstallSettings? settings, CancellationToken ct) => throw new NotSupportedException ();
        public Task<OpResult> InstallAsync (string id, string? version, InstallSettings? settings, IProgress<OpProgress>? progress, CancellationToken ct, string? source = null) => throw new NotSupportedException ();
        public Task<OpResult> UninstallAsync (string id, IProgress<OpProgress>? progress, CancellationToken ct) => throw new NotSupportedException ();
        public Task<OpResult> UpgradeAsync (string id, IProgress<OpProgress>? progress, CancellationToken ct, string? source = null) => throw new NotSupportedException ();
        public Task<OpResult> DownloadAsync (string id, string? version, IProgress<OpProgress>? progress, CancellationToken ct) => throw new NotSupportedException ();
        public Task<InstallVerification?> VerifyInstalledAsync (string id, CancellationToken ct) => throw new NotSupportedException ();
        public Task<OpResult> RepairAsync (string id, IProgress<OpProgress>? progress, CancellationToken ct) => throw new NotSupportedException ();
        public Task<OpResult> PinAsync (string id, CancellationToken ct) => throw new NotSupportedException ();
        public Task<OpResult> UnpinAsync (string id, CancellationToken ct) => throw new NotSupportedException ();
    }
}
