using Terminal.Gui.App;
using static WinGetScout.Tests.RunWorkflowTests;
using static WinGetScout.Tests.FullWorkflowUiTests;

namespace WinGetScout.Tests;

[Collection ("Terminal UI")]
public sealed class BatchClarityUiTests
{
    [Theory]
    [InlineData (AppMode.Search)]
    [InlineData (AppMode.Upgrades)]
    public void BatchMarksUseOneCellWithoutIndentingNamesAcrossNavigationFilterAndResize (AppMode mode)
    {
        using ListNavigationTests.UiSession ui = new (mode, 3);
        ITableSource source = ui.Table.Table!;
        Assert.Equal ("Package 000", source [0, 1]);
        Assert.Equal ("●", source [0, 0]);

        ui.Press (new Key (' '));
        Assert.Same (source, ui.Table.Table);
        Assert.Equal ("◆", source [0, 0]);
        ui.Press (KeyCode.CursorDown);
        Assert.Equal ("✓", source [0, 0]);
        Assert.Equal ("●", source [1, 0]);
        ui.Press (new Key (' '));
        Assert.Equal ("◆", source [1, 0]);
        Assert.Equal ("Package 001", source [1, 1]);

        ui.State.LocalFilter = "001";
        ui.State.ApplyFilter ();
        ui.Invoke ("RefreshTable");
        Assert.Equal ("◆", ui.Table.Table! [0, 0]);
        Assert.Contains ("1 hidden", ui.Get<FrameView> ("_listFrame").Title);
        ui.Window.Width = 80;
        ui.Application.LayoutAndDraw ();
        ColumnStyle marker = ui.Table.Style.GetOrCreateColumnStyle (0);
        Assert.Equal (1, marker.MinWidth);
        Assert.Equal (1, marker.MaxWidth);
        Assert.Equal ("Package 001", ui.Table.Table [0, 1]);

        ui.Press (new Key ('a'));
        Assert.Equal ("●", ui.Table.Table [0, 0]);
        ui.State.LocalFilter = string.Empty;
        ui.State.ApplyFilter ();
        ui.Invoke ("RefreshTable");
        Assert.Contains (Assert.IsType<string> (ui.Table.Table! [0, 0]), new [] { "✓", "◆" });
        ui.Press (KeyCode.Esc);
        Assert.DoesNotContain (Enumerable.Range (0, 3), row => ui.Table.Table [row, 0] is "✓" or "◆");
        ui.Press (new Key ('a'));
        Assert.All (Enumerable.Range (0, 3), row => Assert.Contains (Assert.IsType<string> (ui.Table.Table [row, 0]), new [] { "✓", "◆" }));
        Assert.Single (Enumerable.Range (0, 3), row => Equals ("◆", ui.Table.Table [row, 0]));
        ui.Window.Width = 140;
        ui.Application.LayoutAndDraw ();
        Assert.All (Enumerable.Range (0, 3), row => Assert.Equal ($"Package {row:D3}", ui.Table.Table [row, 1]));
        ui.State.Packages [2].PinState = new (PinStateKind.Blocking);
        ui.Invoke ("RefreshTable");
        Assert.Equal ("✓", ui.Table.Table! [2, 0]);
        Assert.Equal (mode == AppMode.Upgrades ? "📌 Package 002" : "Package 002", ui.Table.Table [2, 1]);
    }

    [Theory]
    [InlineData (true)]
    [InlineData (false)]
    public void EnterOnReviewRowOpensDetailsInsteadOfApprovingTheBatch (bool ready)
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1);
        BatchPlan plan = new ("Upgrade", [new (Tool (), ready, ready ? "1 → 2" : "Pinned: unpin before upgrading.")]);
        using BatchPlanDialog review = new (plan) { Width = 60, Height = 18 };
        SessionToken session = ui.Application.Begin (review)!;
        try
        {
            bool detailsOpened = false;
            object? timer = ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                if (ui.Application.TopRunnable is not RunDiagnosticsDialog detail) return true;
                detailsOpened = true;
                Assert.Contains ("Example.Tool", detail.Document.Text);
                Assert.Contains (plan.Items [0].Reason, detail.Document.Text);
                Assert.DoesNotContain (detail.SubViews.OfType<Button> (), b => b.Visible && (b.Text.Contains ("Copy") || b.Text.Contains ("Save")));
                ui.Press (KeyCode.Esc);
                return false;
            });
            ui.Application.LayoutAndDraw ();
            Assert.Single (review.SubViews.OfType<ListView> ()).SetFocus ();
            ui.Press (KeyCode.Enter);
            if (timer is not null) ui.Application.RemoveTimeout (timer);
            Assert.True (detailsOpened, "Enter on a package should inspect it, not approve every package.");
            Assert.False (review.Result);
            Assert.False (review.StopRequested);
            Assert.True (Assert.Single (review.SubViews.OfType<ListView> ()).HasFocus);
            Assert.Contains (review.SubViews.OfType<Label> (), l => l.Text.Contains ("Enter") && l.Text.Contains ("Details"));
        }
        finally { ui.Application.End (session); }
    }

    [Theory]
    [InlineData (0)]
    [InlineData (1)]
    [InlineData (3)]
    public void ReviewApprovalNamesTheNumberThatWillActuallyRun (int ready)
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1);
        using BatchPlanDialog review = new (new ("Upgrade", Enumerable.Range (0, 4)
            .Select (i => new BatchPlanItem (Tool (), i < ready, "Reason")).ToList ())) { Width = 60, Height = 18 };
        SessionToken session = ui.Application.Begin (review)!;
        try
        {
            Button run = Assert.Single (review.SubViews.OfType<Button> (), b => b.Text.Contains ("Upgrade"));
            Assert.Contains ($"({ready})", run.Text);
            Assert.Equal (ready > 0, run.Enabled);
            Assert.Contains (review.SubViews.OfType<Label> (), l => l.Text.Contains ("4 packages"));
        }
        finally { ui.Application.End (session); }
    }

    [Fact]
    public void ResultsOfferAllUnfinishedRequestsIncludingCancelledPackages ()
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1);
        RunItem failed = Failed (OperationKind.Upgrade);
        RunItem cancelled = failed with { Id = "Cancelled.Tool", Status = "Skipped", Reason = "Cancelled." };
        RunItem notStarted = failed with { Id = "Queued.Tool", Status = "Skipped", Reason = "Cancelled before this package started." };
        RunItem succeeded = failed with { Id = "Done.Tool", Status = "Succeeded" };
        RunItem legacy = new ("Legacy.Tool", "Legacy", "winget", "Failed", "No request recorded");
        using RunRecordDialog dialog = new (new (DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Upgrade",
            [failed, cancelled, notStarted, succeeded, legacy])) { Width = 60, Height = 18 };
        SessionToken session = ui.Application.Begin (dialog)!;
        try
        {
            Button retry = Assert.Single (dialog.SubViews.OfType<Button> (), b => b.Text.Contains ("unfinished"));
            Assert.Contains ("(3)", retry.Text);
            Click (ui, dialog, "unfinished");
            Assert.Equal ([failed, cancelled, notStarted], dialog.Result);
        }
        finally { ui.Application.End (session); }
    }

    [Theory]
    [InlineData (AppMode.Search)]
    [InlineData (AppMode.Upgrades)]
    public void EmptyVisibleListExplainsThatHiddenSelectionsWillStillRun (AppMode mode)
    {
        using ListNavigationTests.UiSession ui = new (mode, 3);
        ui.Press (new Key ('a'));
        ui.State.Packages = []; ui.State.ApplyFilter (); ui.Invoke ("RefreshTable");
        ui.Press (new Key (' '));
        Assert.Contains ("3 selected", ui.State.StatusMessage);
        Assert.Contains ("hidden", ui.State.StatusMessage);
        Assert.Contains (mode == AppMode.Search ? "i" : "u", ui.State.StatusMessage);
    }

    [Fact]
    public void InstallReviewUsesVisibleSortOrderBeforeHiddenSelections ()
    {
        Package zulu = new () { Id = "Zulu.Tool", Name = "Zulu", Version = "1", Source = "winget" };
        Package alpha = new () { Id = "Alpha.Tool", Name = "Alpha", Version = "1", Source = "winget" };
        Package hidden = new () { Id = "Hidden.Tool", Name = "Hidden", Version = "1", Source = "msstore" };
        RecordingBackend backend = new () { Catalog = [zulu, alpha, hidden], Sources = ["winget", "msstore"] };
        using ListNavigationTests.UiSession ui = new (AppMode.Search, 0, backend);
        ui.State.Packages = [zulu, alpha, hidden]; ui.State.ApplyFilter (); ui.Invoke ("RefreshTable");
        ui.Press (new Key ('a'));
        ui.State.Packages = [zulu, alpha]; ui.State.SortField = SortField.Name;
        ui.State.ApplyFilter (); ui.Invoke ("RefreshTable");
        string []? labels = null;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (ui.Application.TopRunnable is not BatchPlanDialog review) return true;
            labels = Assert.Single (review.SubViews.OfType<ListView> ()).Source!.ToList ().Cast<string> ().ToArray ();
            ui.Press (KeyCode.Esc); return false;
        });
        ui.Press (new Key ('i'));
        ui.PumpUntil (() => labels is not null && !ui.State.Loading);
        Assert.StartsWith ("READY Alpha —", labels![0]);
        Assert.StartsWith ("READY Zulu —", labels[1]);
        Assert.StartsWith ("READY Hidden —", labels[2]);
        Assert.Equal (0, backend.Mutations);
    }

    [Theory]
    [InlineData (AppMode.Search)]
    [InlineData (AppMode.Upgrades)]
    public void HiddenMultiSelectionCanBeReviewedCancelledAndResumedWithoutRepeatingSuccesses (AppMode mode)
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-batch-resume-" + Guid.NewGuid ().ToString ("N"));
        try
        {
            Package [] packages = Enumerable.Range (0, 3).Select (i => new Package
            {
                Id = $"Example.{i}", Name = $"Package {i}", Version = "1", AvailableVersion = "2",
                Source = i == 1 ? "msstore" : "winget"
            }).ToArray ();
            WorkflowStore store = new (root);
            TaskCompletionSource<OpResult> cancelledItem = new (TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken cancelToken = default;
            RecordingBackend backend = new () { Catalog = packages, Sources = ["winget", "msstore"] };
            List<string> executed = [];
            backend.OperationResult = (kind, id, _, ct) =>
            {
                executed.Add (id);
                if (backend.Mutations == 2) { cancelToken = ct; return cancelledItem.Task; }
                if (mode == AppMode.Upgrades) backend.Catalog = backend.Catalog.Where (p => p.Id != id).ToArray ();
                else backend.Installed = backend.Installed.Append (packages.Single (p => p.Id == id)).ToArray ();
                return Task.FromResult (CliBackend.CliResult (new () { Kind = kind, PackageId = id }, 0, "Completed"));
            };
            using ListNavigationTests.UiSession ui = new (mode, 0, backend, store);
            ui.State.Packages = packages.ToList (); ui.State.ApplyFilter (); ui.Invoke ("RefreshTable");
            ui.Press (new Key ('a'));
            ui.State.LocalFilter = "Package 0"; ui.State.ApplyFilter (); ui.Invoke ("RefreshTable");
            Assert.Equal (2, ui.Get<StatusBar> ("_statusBar").HiddenSelectionCount);
            int reviews = 0, results = 0;
            bool closed = false;
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                if (ui.Application.TopRunnable is BatchPlanDialog review)
                {
                    reviews++;
                    Assert.Contains ("Review", ui.State.StatusMessage);
                    Assert.DoesNotContain ("Checking", ui.State.StatusMessage);
                    Assert.Equal (reviews == 1 ? 0 : 2, backend.Mutations);
                    Assert.Equal (reviews == 1 ? 3 : 2, Assert.Single (review.SubViews.OfType<ListView> ()).Source!.ToList ().Count);
                    Click (ui, review, reviews == 1 ? mode == AppMode.Search ? "Install" : "Upgrade" : "Retry");
                }
                else if (ui.Application.TopRunnable is RunRecordDialog result)
                {
                    if (ui.State.Loading) return true;
                    results++;
                    if (results == 1)
                    {
                        Assert.Contains ("1 succeeded", ui.State.StatusMessage);
                        Assert.Contains ("2 skipped", ui.State.StatusMessage);
                        Assert.Equal (2, mode == AppMode.Search ? ui.Get<StatusBar> ("_statusBar").InstallSelectionCount : ui.State.BatchSelected.Count);
                        Assert.Equal (2, ui.Get<StatusBar> ("_statusBar").HiddenSelectionCount);
                        Click (ui, result, "unfinished");
                    }
                    else { closed = true; ui.Press (KeyCode.Esc); return false; }
                }
                return true;
            });
            ui.Press (new Key (mode == AppMode.Search ? 'i' : 'u'));
            ui.PumpUntil (() => cancelToken.CanBeCanceled);
            ui.Press (KeyCode.Esc);
            Assert.Contains ("Cancelling", ui.State.StatusMessage);
            cancelledItem.SetCanceled (cancelToken);
            ui.PumpUntil (() => closed);
            Assert.Equal (2, reviews);
            Assert.Equal (2, results);
            Assert.Equal (["Example.0", "Example.1", "Example.1", "Example.2"], executed);
            Assert.Equal (0, mode == AppMode.Search ? ui.Get<StatusBar> ("_statusBar").InstallSelectionCount : ui.State.BatchSelected.Count);
            Assert.Equal (2, store.Runs ().Count);
            Assert.Equal ("Retry", store.Runs () [0].Action);
            Assert.Equal (2, store.Runs () [0].Succeeded);
        }
        finally { if (Directory.Exists (root)) Directory.Delete (root, true); }
    }

    [Theory]
    [InlineData (50)]
    [InlineData (70)]
    public void LongActionContextKeepsSelectionCountsAboveTheActionList (int width)
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1);
        string context = $"A very long application name {new string ('x', 120)} [Vendor.Package] · winget\n1 of 8 shown · 3 selected (2 hidden)";
        using PackageActionDialog dialog = new (context, [new ("batch", "Review upgrade plan (3 selected)…")]) { Width = width, Height = 14 };
        SessionToken session = ui.Application.Begin (dialog)!;
        try
        {
            ui.Application.LayoutAndDraw ();
            Label label = Assert.Single (dialog.SubViews.OfType<Label> (), l => l.Frame.Y == 0);
            Label counts = Assert.Single (dialog.SubViews.OfType<Label> (), l => l.Frame.Y == 1);
            ListView rows = Assert.Single (dialog.SubViews.OfType<ListView> ());
            Assert.True (label.Frame.Bottom <= rows.Frame.Y,
                $"Wrapped context {label.Frame} obscures actions starting at {rows.Frame.Y}.");
            Assert.Equal (1, label.Frame.Height);
            Assert.False (label.TextFormatter.WordWrap);
            Assert.Equal (1, counts.Frame.Height);
            Assert.Contains ("3 selected (2 hidden)", counts.Text);
        }
        finally { ui.Application.End (session); }
    }

    [Fact]
    public void GlobalQuitClosesBothDetailsAndItsParentReviewWithoutApproving ()
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1);
        using BatchPlanDialog review = new (new ("Upgrade", [new (Tool (), true, "1 → 2")]));
        bool quit = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            Assert.Same (review, ui.Application.TopRunnable);
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                RunDiagnosticsDialog details = Assert.IsType<RunDiagnosticsDialog> (ui.Application.TopRunnable);
                ui.Press (KeyCode.Q | KeyCode.CtrlMask);
                Assert.True (details.StopRequested);
                Assert.True (review.StopRequested, "Ctrl+Q must unwind the parent review too.");
                Assert.True (ui.Window.StopRequested);
                quit = true;
                return false;
            });
            Assert.Single (review.SubViews.OfType<ListView> ()).SetFocus ();
            ui.Press (KeyCode.Enter);
            return false;
        });
        ui.Application.Run (review);
        Assert.True (quit);
        Assert.False (review.Result);
    }
}
