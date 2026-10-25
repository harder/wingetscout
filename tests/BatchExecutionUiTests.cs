using static WinGetScout.Tests.RunWorkflowTests;

namespace WinGetScout.Tests;

[Collection ("Terminal UI")]
public sealed class BatchExecutionUiTests
{
    [Theory]
    [InlineData (AppMode.Upgrades, 'u')]
    [InlineData (AppMode.Upgrades, 'U')]
    [InlineData (AppMode.Upgrades, 'm')]
    [InlineData (AppMode.Search, 'i')]
    [InlineData (AppMode.Search, 'B')]
    [InlineData (AppMode.Search, 'm')]
    public void PrimaryActionsReviewAllMarkedPackagesBeforeExecuting (AppMode mode, char shortcut)
    {
        RecordingBackend backend = new () { Catalog = [Tool (), Tool ("msstore")] , Sources = ["winget", "msstore"] };
        using ListNavigationTests.UiSession ui = new (mode, 0, backend);
        ui.State.Packages = backend.Catalog.ToList ();
        ui.State.ApplyFilter (); ui.Invoke ("RefreshTable");
        ui.Press (new Key ('a'));
        // Marked packages from another source must participate even when not visible.
        ui.State.Packages = [backend.Catalog [0]];
        ui.State.ApplyFilter (); ui.Invoke ("RefreshTable"); ui.Invoke ("RefreshStatusBar");
        Assert.Equal (1, ui.Get<StatusBar> ("_statusBar").HiddenSelectionCount);
        bool reviewed = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (ui.Application.TopRunnable is PackageActionDialog)
            {
                Assert.Equal (0, backend.Mutations);
                ui.Press (KeyCode.Enter);
            }
            else if (ui.Application.TopRunnable is BatchPlanDialog review)
            {
                reviewed = true;
                Assert.Equal (0, backend.Mutations);
                Assert.Contains (review.SubViews.OfType<Label> (), label => label.Text.Contains ("2 ready"));
                ui.Press (KeyCode.Esc);
                return false;
            }
            else if (ui.Application.TopRunnableView is { } modal && modal != ui.Window)
            {
                ui.Application.RequestStop ();
                return false;
            }
            return true;
        });
        ui.Press (new Key (shortcut));
        ui.PumpUntil (() => reviewed && !ui.State.Loading);
        Assert.Equal (0, backend.Mutations);
        Assert.Equal (2, mode == AppMode.Upgrades ? ui.State.BatchSelected.Count
            : ui.Get<Dictionary<string, Package>> ("_searchSelected").Count);
    }

    [Fact]
    public void BatchShowsItemProgressRejectsLateSamplesAndRefreshesWhileResultsAreOpen ()
    {
        WithStore (store =>
        {
            Package first = Tool (), second = Tool ("msstore");
            TaskCompletionSource<OpResult> firstDone = new (TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<OpResult> secondDone = new (TaskCreationOptions.RunContinuationsAsynchronously);
            IProgress<OpProgress>? firstProgress = null, secondProgress = null;
            RecordingBackend backend = new () { Catalog = [first, second] };
            backend.OperationResult = (_, _, progress, _) =>
            {
                if (backend.Mutations == 1) { firstProgress = progress; return firstDone.Task; }
                secondProgress = progress; return secondDone.Task;
            };
            using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 0, backend, store);
            ui.State.Packages = [first, second]; ui.State.ApplyFilter (); ui.Invoke ("RefreshTable");
            ui.State.BatchSelected.UnionWith (backend.Catalog.Select (BatchPlanning.Key));
            bool refreshed = false;
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                if (ui.Application.TopRunnable is BatchPlanDialog review)
                {
                    Button run = Assert.Single (review.SubViews.OfType<Button> (), b => b.Text.Contains ("Upgrade"));
                    run.SetFocus (); ui.Press (KeyCode.Enter);
                }
                else if (ui.Application.TopRunnable is RunRecordDialog result)
                {
                    if (ui.State.Loading) return true;
                    Assert.Equal (2, backend.Mutations);
                    Assert.Equal ("msstore", Assert.Single (ui.State.Filtered).Source);
                    Assert.Equal ([BatchPlanning.Key (second)], ui.State.BatchSelected);
                    Assert.Contains ("1 succeeded", ui.State.StatusMessage);
                    Assert.Contains ("1 failed", ui.State.StatusMessage);
                    Assert.True (ui.State.StatusIsError);
                    Assert.Null (ui.State.OpProgress);
                    refreshed = true; result.RequestStop (); return false;
                }
                return true;
            });
            ui.Invoke ("AskBatchUpgrade");
            ui.PumpUntil (() => firstProgress is not null);
            Assert.Contains ("1/2", ui.State.StatusMessage);
            firstProgress!.Report (new (OpPhase.Downloading, .4));
            ui.PumpUntil (() => ui.State.OpProgress?.Fraction == .4);
            firstDone.SetResult (CliBackend.CliResult (new () { Kind = OperationKind.Upgrade, PackageId = first.Id }, 0, "Done"));
            ui.PumpUntil (() => secondProgress is not null);
            Assert.Contains ("2/2", ui.State.StatusMessage);
            Assert.Null (ui.State.OpProgress);
            firstProgress.Report (new (OpPhase.Done, 1));
            secondProgress!.Report (new (OpPhase.Installing, .2));
            ui.PumpUntil (() => ui.State.OpProgress?.Fraction == .2);
            firstProgress.Report (new (OpPhase.Done, 1));
            ui.Pump ();
            Assert.Equal (.2, ui.State.OpProgress?.Fraction);
            backend.Catalog = [second];
            secondDone.SetException (new InvalidOperationException ("Installer failed with diagnostics"));
            ui.PumpUntil (() => refreshed);
            RunRecord record = Assert.Single (store.Runs ());
            Assert.Equal (2, record.Items.Count);
            Assert.Equal ("Succeeded", record.Items [0].Status);
            Assert.Contains ("Installer failed", record.Items [1].Diagnostics);
            Assert.True (record.Items [1].CanRetry);
        });
    }

    [Fact]
    public void CancellingBatchStopsRemainingMutationsAndRecordsEveryPackage ()
    {
        WithStore (store =>
        {
            RecordingBackend backend = new () { Catalog = [Tool (), Tool ("msstore")] };
            TaskCompletionSource<OpResult> pending = new (TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken operationToken = default;
            backend.OperationResult = (_, _, _, ct) => { operationToken = ct; return pending.Task; };
            using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 0, backend, store);
            ui.State.BatchSelected.UnionWith (backend.Catalog.Select (BatchPlanning.Key));
            bool resultClosed = false;
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                if (ui.Application.TopRunnable is BatchPlanDialog review)
                {
                    Button run = Assert.Single (review.SubViews.OfType<Button> (), b => b.Text.Contains ("Upgrade"));
                    run.SetFocus (); ui.Press (KeyCode.Enter);
                }
                else if (ui.Application.TopRunnable is RunRecordDialog result)
                {
                    resultClosed = true; result.RequestStop (); return false;
                }
                return true;
            });
            ui.Invoke ("AskBatchUpgrade");
            ui.PumpUntil (() => backend.Mutations == 1);
            ui.Press (KeyCode.Esc);
            Assert.True (operationToken.IsCancellationRequested);
            pending.SetCanceled (operationToken);
            ui.PumpUntil (() => resultClosed && !ui.State.Loading);
            RunRecord run = Assert.Single (store.Runs ());
            Assert.Equal (1, backend.Mutations);
            Assert.Equal (2, run.Skipped);
            Assert.All (run.Items, item => Assert.True (item.CanRetry));
            Assert.Equal (2, ui.State.BatchSelected.Count);
            Assert.Null (ui.State.OpProgress);
        });
    }

    [Fact]
    public void SelectedActionsAreFirstAndHighlightedActionsExplainTheirScope ()
    {
        foreach (AppMode mode in new [] { AppMode.Search, AppMode.Upgrades })
        {
            IReadOnlyList<PackageAction> actions = PackageActions.For (mode, Tool (), true, false, false, 2, null);
            Assert.Equal ("batch", actions [0].Id);
            Assert.Contains ("2 selected", actions [0].Label);
            Assert.Contains ("highlighted", Assert.Single (actions, x => x.Id == (mode == AppMode.Search ? "install" : "upgrade")).Label);
        }
    }

    [Theory]
    [InlineData (true)]
    [InlineData (false)]
    public void ExplicitHighlightedUpgradeAffectsOnlyThatSelectionAndKeepsItsOutcome (bool success)
    {
        WithStore (store =>
        {
            Package first = Tool (), second = Tool ("msstore");
            RecordingBackend backend = new () { Catalog = [first, second] };
            backend.OperationResult = (kind, id, _, _) =>
            {
                if (success) backend.Catalog = [second];
                return Task.FromResult (CliBackend.CliResult (new () { Kind = kind, PackageId = id },
                    success ? 0 : 1603, success ? "Upgraded the highlighted package" : "Installer failed with code 1603"));
            };
            using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 0, backend, store);
            ui.State.Packages = [first, second]; ui.State.ApplyFilter (); ui.Invoke ("RefreshTable");
            ui.State.RecordPinSnapshot (new Dictionary<string, PinState> ());
            ui.State.BatchSelected.UnionWith (backend.Catalog.Select (BatchPlanning.Key));
            bool confirmed = false;
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                if (ui.Application.TopRunnable is PackageActionDialog menu)
                {
                    Assert.Single (menu.SubViews.OfType<ListView> ()).SelectedItem = 1;
                    ui.Press (KeyCode.Enter);
                }
                else if (ui.Application.TopRunnable is Dialog confirmation)
                {
                    Assert.Equal (0, backend.Mutations);
                    confirmed = true;
                    Button yes = Assert.Single (confirmation.Buttons!, b => b.Text.Contains ("Yes"));
                    yes.SetFocus (); ui.Press (KeyCode.Enter);
                    return false;
                }
                return true;
            });
            ui.Press (new Key ('m'));
            ui.PumpUntil (() => store.Runs ().Count == 1 && !ui.State.Loading);
            Assert.True (confirmed);
            Assert.Equal (1, backend.Mutations);
            Assert.Equal (success ? 1 : 2, ui.State.BatchSelected.Count);
            Assert.Contains (BatchPlanning.Key (second), ui.State.BatchSelected);
            Assert.Equal (!success, ui.State.StatusIsError);
            Assert.Contains (success ? "Upgraded the highlighted package" : "1603", ui.State.StatusMessage);
        });
    }

    [Theory]
    [InlineData (80)]
    [InlineData (100)]
    [InlineData (140)]
    public void StatusAndProgressHaveAFullRowAboveShortcutsAtSupportedWidths (int width)
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 2);
        ui.Press (new Key ('a'));
        ui.Window.Width = width; ui.Window.Height = 24;
        ui.State.StatusMessage = "Upgrade 2/2: PowerShell · Esc to cancel";
        ui.State.OpProgress = new (OpPhase.Installing, .5);
        ui.Invoke ("RefreshStatusBar"); ui.Application.LayoutAndDraw ();
        StatusBar footer = ui.Get<StatusBar> ("_statusBar");
        Assert.Equal (2, footer.Frame.Height);
        Assert.True (ui.Get<FrameView> ("_listFrame").Frame.Bottom <= footer.Frame.Y);
        Assert.True (ui.Detail.Frame.Bottom <= footer.Frame.Y);
        string message = footer.ComposeMessage (footer.Viewport.Width);
        Assert.Contains ("50%", message);
        Assert.Contains ("Installing", message);
        Assert.Contains ("2/2: PowerShell", message);
        Assert.Contains ("Esc to cancel", message);
        Assert.DoesNotContain ("…", message);
        footer.Op = null; footer.IsError = true;
        footer.Message = "Installed package 'Microsoft.PowerToys' not found. Press L for runs.";
        Assert.Equal (footer.Message, footer.ComposeMessage (footer.Viewport.Width));
    }

    [Fact]
    public async Task MockUpgradesUpdateInventoryWithoutChangingOtherSessions ()
    {
        MockBackend backend = new ();
        Package before = Assert.Single (await backend.ListUpgradesAsync (null, CancellationToken.None), p => p.Id == "7zip.7zip");
        OpResult result = await backend.UpgradeAsync (before.Id, null, CancellationToken.None, before.Source);
        Assert.True (result.Success);
        Assert.DoesNotContain (await backend.ListUpgradesAsync (null, CancellationToken.None), p => p.Id == before.Id);
        Package installed = Assert.Single (await backend.ListInstalledAsync (null, CancellationToken.None), p => p.Id == before.Id);
        Assert.Equal (before.AvailableVersion, installed.Version);
        Assert.Null (installed.AvailableVersion);
        Assert.Equal (before.Version, Assert.Single (await new MockBackend ().ListInstalledAsync (null, CancellationToken.None),
            p => p.Id == before.Id).Version);
    }

    private static void WithStore (Action<WorkflowStore> test)
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-batch-ui-" + Guid.NewGuid ().ToString ("N"));
        try { test (new (root)); }
        finally { if (Directory.Exists (root)) Directory.Delete (root, true); }
    }
}
