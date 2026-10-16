using Terminal.Gui.App;
using static WinGetScout.Tests.RunWorkflowTests;

namespace WinGetScout.Tests;

[Collection ("Terminal UI")]
public sealed class WorkflowUiTests
{
    [Theory]
    [InlineData ("Save")]
    [InlineData ("Disable")]
    public void MockScheduleChangesAreDisabledAndCannotReachProductionTasks (string action)
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-mock-schedule-" + Guid.NewGuid ().ToString ("N"));
        try
        {
            WorkflowStore store = new (root);
            UpdateCheckSettings previous = new (true, "09:00", true);
            store.SaveSchedule (previous);
            using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1, new MockBackend (), store);
            bool sawDialog = false;
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                ScheduleDialog dialog = Assert.IsType<ScheduleDialog> (ui.Application.TopRunnable);
                sawDialog = true;
                Button save = Assert.Single (dialog.SubViews.OfType<Button> (), b => b.Text.Contains ("save"));
                Button disable = Assert.Single (dialog.SubViews.OfType<Button> (), b => b.Text.Contains ("Disable"));
                Assert.False (save.Enabled);
                Assert.False (disable.Enabled);
                Assert.True (Assert.Single (dialog.SubViews.OfType<Button> (), b => b.Text.Contains ("now")).Enabled);
                // Even a programmatically supplied result must not cross the mock boundary.
                dialog.Result = new (action, "14:00", false);
                dialog.RequestStop ();
                return false;
            });
            ui.Invoke ("ManageScheduledChecks");
            Assert.True (sawDialog);
            Assert.Equal (previous, store.Schedule ());
            Assert.Contains ("mock", ui.State.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False (ui.State.Loading);
        }
        finally { if (Directory.Exists (root)) Directory.Delete (root, true); }
    }

    [Fact]
    public void MockSessionCanStillCheckNowWithoutCreatingScheduleSettings ()
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-mock-check-" + Guid.NewGuid ().ToString ("N"));
        try
        {
            WorkflowStore store = new (root);
            using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1, new MockBackend (), store);
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                ScheduleDialog dialog = Assert.IsType<ScheduleDialog> (ui.Application.TopRunnable);
                Button check = Assert.Single (dialog.SubViews.OfType<Button> (), b => b.Text.Contains ("now"));
                check.SetFocus (); ui.Press (KeyCode.Enter);
                return false;
            });
            ui.Invoke ("ManageScheduledChecks");
            ui.PumpUntil (() => store.LatestCheck () is not null);
            Assert.Equal ("Succeeded", store.LatestCheck ()!.Status);
            Assert.False (File.Exists (Path.Combine (root, "schedule.json")));
            Assert.False (store.Schedule ().Enabled);
        }
        finally { if (Directory.Exists (root)) Directory.Delete (root, true); }
    }

    [Fact]
    public void FilteredCountsKeepHiddenSelectionsAndSelectAllOnlyChangesVisibleRows ()
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 4);
        ui.Press (new Key ('a'));
        ui.Press (new Key ('/'));
        ui.Get<TextField> ("_filterInput").Text = "Package 000";
        FrameView frame = ui.Get<FrameView> ("_listFrame");
        Assert.Contains ("1 of 4 shown", frame.Title);
        Assert.Contains ("4 selected (3 hidden)", frame.Title);
        Assert.Equal (3, ui.Get<StatusBar> ("_statusBar").HiddenSelectionCount);
        ui.Press (KeyCode.Enter);
        ui.Press (new Key ('a'));
        Assert.Equal (3, ui.State.BatchSelected.Count);
        Assert.Contains ("3 selected (3 hidden)", frame.Title);
        ui.Press (new Key ('/'));
        ui.Get<TextField> ("_filterInput").Text = string.Empty;
        ui.Press (KeyCode.Enter);
        Assert.Contains ("4 of 4 shown · 3 selected", frame.Title);
        ui.Press (KeyCode.End);
        ui.Window.Width = 100; ui.Window.Height = 24;
        ui.Application.LayoutAndDraw ();
        ui.Press (KeyCode.CursorUp);
        ui.AssertSelection ();
    }

    [Fact]
    public void SearchCountsRetainEarlierResultsButInstalledStatusDoesNotShowTheirSelections ()
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Search, 2);
        ui.Press (new Key ('a'));
        ui.State.Packages = [new () { Id = "Different", Name = "Different", Source = "winget" }];
        ui.State.ApplyFilter (); ui.Invoke ("RefreshTable");
        Assert.Contains ("2 selected (2 hidden)", ui.Get<FrameView> ("_listFrame").Title);
        ui.State.Mode = AppMode.Installed;
        ui.Invoke ("RefreshTable"); ui.Invoke ("RefreshStatusBar");
        Assert.DoesNotContain ("selected", ui.Get<FrameView> ("_listFrame").Title);
        Assert.Equal (0, ui.Get<StatusBar> ("_statusBar").HiddenSelectionCount);
    }

    [Theory]
    [InlineData ("homepage", "Open homepage", "https://example.invalid/selected/home")]
    [InlineData ("changelog", "Open release notes", "https://example.invalid/selected/releases")]
    public void MenuLinksKeepTheirDestinationWhenDelayedRefreshSelectsAnotherRow (string action, string label, string expected)
    {
        TaskCompletionSource<IReadOnlyList<Package>> refresh = new (TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> opened = [];
        ListNavigationTests.NavigationBackend backend = new () { Load = (_, _) => refresh.Task };
        using ListNavigationTests.UiSession ui = new (AppMode.Installed, 2, backend,
            urlLauncher: psi => { opened.Add (psi.FileName); return null; });
        Package [] packages = ui.State.Packages.ToArray ();
        ui.State.CacheDetail (packages [0].Id, new () { Id = packages [0].Id, Name = "Original", Homepage = "https://example.invalid/other/home", ReleaseNotesUrl = "https://example.invalid/other/releases" });
        ui.State.CacheDetail (packages [1].Id, new () { Id = packages [1].Id, Name = "Selected", Homepage = "https://example.invalid/selected/home", ReleaseNotesUrl = "https://example.invalid/selected/releases" });
        ui.Invoke ("OnSelectedRowChanged");
        ui.Press (new Key ('r'));
        ui.PumpUntil (() => backend.Sources.Count == 1);
        ui.Press (KeyCode.CursorDown);
        Assert.Equal (packages [1].Id, ui.State.CurrentDetail?.Id);
        bool completedRefresh = false, choseLink = false;
        System.Diagnostics.Stopwatch deadline = System.Diagnostics.Stopwatch.StartNew ();
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (deadline.Elapsed > TimeSpan.FromSeconds (5)) throw new TimeoutException ("Refresh did not complete during menu review.");
            PackageActionDialog menu = Assert.IsType<PackageActionDialog> (ui.Application.TopRunnable);
            ListView rows = Assert.Single (menu.SubViews.OfType<ListView> ());
            Assert.Contains (label, rows.Source!.ToList ().Cast<string> ());
            if (!completedRefresh)
            {
                completedRefresh = true;
                refresh.SetResult (packages);
                return true;
            }
            if (ui.State.CurrentDetail?.Id != packages [0].Id) return true;
            choseLink = true;
            menu.Result = action;
            menu.RequestStop ();
            return false;
        });
        ui.Press (new Key ('m'));
        Assert.True (choseLink);
        Assert.Equal (expected, Assert.Single (opened));
        Assert.Equal (packages [0].Id, ui.State.CurrentDetail?.Id);
    }

    [Theory]
    [InlineData (false)]
    [InlineData (true)]
    public void ContextMenuCanBeCancelledWithoutExecutingAndIsBlockedByActiveOperation (bool shiftF10)
    {
        RecordingBackend backend = new ();
        using ListNavigationTests.UiSession ui = new (AppMode.Search, 1, backend);
        bool sawMenu = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            PackageActionDialog menu = Assert.IsType<PackageActionDialog> (ui.Application.TopRunnable);
            sawMenu = true;
            Assert.Equal (0, backend.Mutations);
            ui.Press (KeyCode.Esc);
            Assert.True (menu.StopRequested);
            Assert.Null (menu.Result);
            return false;
        });
        if (shiftF10) ui.Press (KeyCode.F10 | KeyCode.ShiftMask);
        else ui.Press (new Key ('m'));
        Assert.True (sawMenu);
        Assert.Equal (0, backend.Mutations);
        ForegroundWorkflowCoordinator coordinator = ui.Get<ForegroundWorkflowCoordinator> ("_foreground");
        Assert.True (coordinator.TryReserveOperation (out OperationReservation? reservation));
        using (reservation)
        {
            ui.Press (new Key ('m'));
            Assert.Same (ui.Window, ui.Application.TopRunnable);
        }
    }

    [Fact]
    public void ContextInstallActionStillRequiresConfirmation ()
    {
        RecordingBackend backend = new ();
        using ListNavigationTests.UiSession ui = new (AppMode.Search, 1, backend);
        bool choseAction = false, sawConfirmation = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (ui.Application.TopRunnable is PackageActionDialog menu && !choseAction)
            {
                choseAction = true;
                Assert.Equal (0, backend.Mutations);
                ui.Press (KeyCode.Enter);
            }
            else if (choseAction && ui.Application.TopRunnableView is { } modal && modal != ui.Window)
            {
                Assert.Contains ("Install", modal.Title);
                Assert.Equal (0, backend.Mutations);
                sawConfirmation = true;
                ui.Application.RequestStop ();
                return false;
            }
            return true;
        });
        ui.Press (new Key ('m'));
        ui.PumpUntil (() => sawConfirmation && !ui.State.Loading);
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public void UpgradeReviewIncludesSourceHiddenSelectionsAndCanBeCancelled ()
    {
        RecordingBackend backend = new () { Catalog = [Tool (), Tool ("msstore")] };
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1, backend);
        ui.State.BatchSelected.UnionWith ([BatchPlanning.Key (Tool ()), BatchPlanning.Key (Tool ("msstore")), "other\u001fMissing.Tool"]);
        bool reviewed = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (ui.Application.TopRunnable is not BatchPlanDialog review) return true;
            reviewed = true;
            ListView rows = Assert.Single (review.SubViews.OfType<ListView> ());
            rows.SetFocus (); ui.Press (KeyCode.End);
            Assert.Equal (2, rows.SelectedItem);
            Assert.Equal (0, backend.Mutations);
            ui.Press (KeyCode.Esc);
            return false;
        });
        ui.Invoke ("AskBatchUpgrade");
        ui.PumpUntil (() => reviewed && !ui.State.Loading);
        Assert.Equal (3, ui.State.BatchSelected.Count);
        Assert.Equal (0, backend.Mutations);
        Assert.Contains ("cancelled", ui.State.StatusMessage);
    }

    [Fact]
    public void CancellingRetryReviewDoesNotCallMutationOrChangeHistory ()
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-retry-ui-" + Guid.NewGuid ().ToString ("N"));
        try
        {
            WorkflowStore store = new (root);
            RunItem failed = Failed (version: "2", settings: new () { Scope = InstallScopePref.User });
            RunRecord record = new (DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Install", [failed]);
            store.SaveRun (record);
            string before = File.ReadAllText (Path.Combine (root, "runs.json"));
            RecordingBackend backend = new ();
            using ListNavigationTests.UiSession ui = new (AppMode.Search, 1, backend, store);
            bool sawReview = false;
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                if (ui.Application.TopRunnable is not BatchPlanDialog review) return true;
                sawReview = true;
                Assert.Equal (0, backend.Mutations);
                ui.Press (KeyCode.Esc);
                Assert.False (review.Result);
                return false;
            });
            ui.Invoke ("AskRetry", new List<RunItem> { failed });
            ui.PumpUntil (() => sawReview && !ui.State.Loading);
            Assert.Equal (0, backend.Mutations);
            Assert.Equal (before, File.ReadAllText (Path.Combine (root, "runs.json")));
            Assert.Contains ("cancelled", ui.State.StatusMessage);
        }
        finally { if (Directory.Exists (root)) Directory.Delete (root, true); }
    }

    [Fact]
    public void ResultDialogDisablesRetryForLegacyAndSuccessfulRows ()
    {
        using IApplication app = Terminal.Gui.App.Application.Create ();
        RunItem retryable = Failed ();
        RunRecord record = new (DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Install",
            [retryable, retryable with { Status = "Succeeded" }, new ("Old", "Old", "winget", "Failed", "Legacy")]);
        using RunRecordDialog dialog = new (record) { Width = 100, Height = 24 };
        SessionToken session = app.Begin (dialog)!;
        try
        {
            ListView rows = Assert.Single (dialog.SubViews.OfType<ListView> ());
            Button retry = Assert.Single (dialog.SubViews.OfType<Button> (), b => b.Text.Contains ("selected"));
            rows.SelectedItem = 0; Assert.True (retry.Enabled);
            rows.SelectedItem = 1; Assert.False (retry.Enabled);
            rows.SelectedItem = 2; Assert.False (retry.Enabled);
        }
        finally { app.End (session); }
    }

    [Fact]
    public void ApprovedRetryExecutesOnceWithOriginalOptionsAndCreatesANewRun ()
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-approved-retry-" + Guid.NewGuid ().ToString ("N"));
        try
        {
            WorkflowStore store = new (root);
            InstallSettings options = new () { Scope = InstallScopePref.User, CustomArgs = "/feature" };
            RunItem failed = Failed (version: "2", settings: options);
            store.SaveRun (new (DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Install", [failed]));
            RecordingBackend backend = new ();
            using ListNavigationTests.UiSession ui = new (AppMode.Search, 1, backend, store);
            bool reviewed = false, sawResults = false;
            ui.State.BatchSelected.Add ("winget\u001fUnrelated.Package");
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                if (ui.Application.TopRunnable is BatchPlanDialog review && !reviewed)
                {
                    Assert.Equal (0, backend.Mutations);
                    reviewed = true;
                    Button run = Assert.Single (review.SubViews.OfType<Button> (), b => b.Text.Contains ("Retry"));
                    Assert.True (run.Enabled);
                    run.SetFocus (); ui.Press (KeyCode.Enter);
                }
                else if (ui.Application.TopRunnable is RunRecordDialog result)
                {
                    Assert.Equal (1, backend.Mutations);
                    sawResults = true; result.RequestStop ();
                    return false;
                }
                return true;
            });
            ui.Invoke ("AskRetry", new List<RunItem> { failed });
            ui.PumpUntil (() => sawResults && !ui.State.Loading);
            Assert.True (reviewed);
            Assert.Equal ("2", backend.LastVersion);
            Assert.Equal ("winget", backend.LastSource);
            Assert.Same (options, backend.LastSettings);
            IReadOnlyList<RunRecord> runs = store.Runs ();
            Assert.Equal (2, runs.Count);
            Assert.Equal ("Retry", runs [0].Action);
            Assert.Equal ("Succeeded", Assert.Single (runs [0].Items).Status);
            Assert.Equal ("2", runs [0].Items [0].TargetVersion);
            Assert.Equal ("Failed", runs [1].Items [0].Status);
            Assert.NotNull (runs [0].Items [0].StartedAtUtc);
            Assert.Contains ("winget\u001fUnrelated.Package", ui.State.BatchSelected);
        }
        finally { if (Directory.Exists (root)) Directory.Delete (root, true); }
    }

    [Fact]
    public void DiagnosticsCanScrollWithKeyboardAndReflowAfterResize ()
    {
        using IApplication app = Terminal.Gui.App.Application.Create ();
        using RunDiagnosticsDialog dialog = new (Failed () with
        {
            Diagnostics = string.Join ('\n', Enumerable.Range (0, 100).Select (i => $"Diagnostic line {i}"))
        }) { Width = 100, Height = 24 };
        SessionToken session = app.Begin (dialog)!;
        try
        {
            app.LayoutAndDraw ();
            Code text = Assert.Single (dialog.SubViews.OfType<Code> ());
            text.SetFocus ();
            app.Keyboard.RaiseKeyDownEvent (new (KeyCode.PageDown));
            Assert.True (text.Viewport.Y > 0);
            dialog.Width = 80; dialog.Height = 18;
            app.LayoutAndDraw ();
            app.Keyboard.RaiseKeyDownEvent (new (KeyCode.CursorDown));
            Assert.True (text.HasFocus);
            Assert.True (text.Viewport.Y > 0);
        }
        finally { app.End (session); }
    }
}
