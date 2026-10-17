using static WinGetScout.Tests.RunWorkflowTests;

namespace WinGetScout.Tests;

[Collection ("Terminal UI")]
public sealed class UxInteractionTests
{
    [Theory]
    [InlineData ("escape")]
    [InlineData ("tab")]
    [InlineData ("source")]
    [InlineData ("refresh")]
    public void AbandonedPlanChecksCannotOpenLateReviews (string action)
    {
        TaskCompletionSource<BatchPlan> pending = new (TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken checkToken = default;
        RecordingBackend backend = new ();
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1, backend);
        bool started = false, lateReview = false;
        Func<CancellationToken, Task<BatchPlan>> build = ct => { checkToken = ct; started = true; return pending.Task; };
        ui.Invoke ("ReviewOperationPlan", "Checking selected upgrades…", "Upgrade", build);
        ui.PumpUntil (() => started);
        ui.Press (action switch
        {
            "escape" => new Key (KeyCode.Esc), "tab" => new Key ('2'),
            "source" => new Key ('f'), _ => new Key ('r')
        });
        bool cancelled = checkToken.IsCancellationRequested;
        ForegroundWorkflowCoordinator foreground = ui.Get<ForegroundWorkflowCoordinator> ("_foreground");
        bool releasedBeforeCompletion = foreground.TryReserveOperation (out OperationReservation? replacement);
        replacement?.Dispose ();
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (ui.Application.TopRunnable is BatchPlanDialog review)
            {
                lateReview = true; review.RequestStop ();
            }
            return true;
        });
        pending.SetResult (new ("Upgrade", [new (Tool (), true, "1 → 2")]));
        ui.PumpUntil (() => !ui.State.Loading);
        Assert.True (cancelled, "Abandoning a pending plan should cancel its backend request.");
        Assert.True (releasedBeforeCompletion, "A backend that ignores cancellation must not lock the next workflow.");
        Assert.False (lateReview);
        Assert.Equal (0, backend.Mutations);
        Assert.True (ui.Get<ForegroundWorkflowCoordinator> ("_foreground").TryReserveOperation (out OperationReservation? next));
        next!.Dispose ();
    }

    [Theory]
    [InlineData (AppMode.Search)]
    [InlineData (AppMode.Upgrades)]
    public void EscapeCanClearTheEntireSelectionIncludingHiddenPackages (AppMode mode)
    {
        using ListNavigationTests.UiSession ui = new (mode, 4);
        ui.Press (new Key ('a'));
        if (mode == AppMode.Search)
        {
            ui.State.Packages = [];
            ui.State.ApplyFilter (); ui.Invoke ("RefreshTable");
        }
        else
        {
            ui.Press (new Key ('/'));
            ui.Get<TextField> ("_filterInput").Text = "no matching packages";
            ui.Press (KeyCode.Enter);
        }
        Assert.Equal (4, ui.Get<StatusBar> ("_statusBar").HiddenSelectionCount);
        ui.Press (KeyCode.Esc);
        Assert.Contains ("Selection cleared", ui.State.StatusMessage);
        Assert.DoesNotContain ("selected", ui.Get<FrameView> ("_listFrame").Title);
        Assert.Equal (0, ui.Get<StatusBar> ("_statusBar").HiddenSelectionCount);
        Assert.True (ui.Table.HasFocus);
        Assert.Equal (mode == AppMode.Upgrades ? "no matching packages" : "", ui.State.LocalFilter);
    }

    [Theory]
    [InlineData (AppMode.Search)]
    [InlineData (AppMode.Upgrades)]
    public void MarkingPackagesReplacesAnObsoleteSelectionClearedMessage (AppMode mode)
    {
        using ListNavigationTests.UiSession ui = new (mode, 4);
        ui.Press (new Key ('a'));
        ui.Press (KeyCode.Esc);
        Assert.Contains ("Selection cleared", ui.State.StatusMessage);
        ui.Press (new Key (' '));
        Assert.Contains ("1 selected", ui.State.StatusMessage);
        Assert.DoesNotContain ("cleared", ui.State.StatusMessage);
        ui.Press (new Key ('a'));
        Assert.Contains ("4 selected", ui.State.StatusMessage);
        ui.Press (new Key ('a'));
        Assert.Contains ("Selection cleared", ui.State.StatusMessage);
    }

    [Theory]
    [InlineData (AppMode.Search, false)]
    [InlineData (AppMode.Installed, false)]
    [InlineData (AppMode.Upgrades, false)]
    [InlineData (AppMode.Search, true)]
    [InlineData (AppMode.Installed, true)]
    [InlineData (AppMode.Upgrades, true)]
    public void SortingKeepsTheHighlightedPackageAndDetailsTogether (AppMode mode, bool header)
    {
        using ListNavigationTests.UiSession ui = new (mode, 4);
        ui.State.Packages.Reverse ();
        ui.State.ApplyFilter ();
        ui.Invoke ("RefreshTable");
        string highlighted = BatchPlanning.Key (ui.State.Filtered [0]);
        for (int i = 0; i < 12; i++)
        {
            if (header) ui.Invoke ("OnHeaderClicked", 1 + i % (mode == AppMode.Upgrades ? 4 : 3));
            else ui.Press (new Key ('S'));
            ui.Application.LayoutAndDraw ();
            Package current = ui.State.SelectedPackage (ui.Table.Value!.SelectedCell.Y)!;
            Assert.Equal (highlighted, BatchPlanning.Key (current));
            ui.AssertSelection ();
        }
    }

    [Fact]
    public void UpgradeReviewFollowsVisibleSortOrderThenIncludesHiddenSelections ()
    {
        Package zulu = new () { Id = "Zulu.Tool", Name = "Zulu", Version = "1", AvailableVersion = "2", Source = "winget" };
        Package alpha = new () { Id = "Alpha.Tool", Name = "Alpha", Version = "1", AvailableVersion = "2", Source = "winget" };
        RecordingBackend backend = new () { Catalog = [zulu, alpha, Tool ("msstore")] };
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 0, backend);
        ui.State.Packages = [zulu, alpha];
        ui.State.SortField = SortField.Name;
        ui.State.ApplyFilter (); ui.Invoke ("RefreshTable");
        ui.State.BatchSelected.UnionWith (backend.Catalog.Select (BatchPlanning.Key));
        string []? labels = null;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (ui.Application.TopRunnable is not BatchPlanDialog review) return true;
            labels = Assert.Single (review.SubViews.OfType<ListView> ()).Source!.ToList ().Cast<string> ().ToArray ();
            review.RequestStop (); return false;
        });
        ui.Invoke ("AskBatchUpgrade");
        ui.PumpUntil (() => labels is not null && !ui.State.Loading);
        Assert.Contains ("Alpha.Tool", labels![0]);
        Assert.Contains ("Zulu.Tool", labels[1]);
        Assert.Contains ("msstore", labels[2]);
        Assert.Equal (0, backend.Mutations);
    }

    [Theory]
    [InlineData (80, 24)]
    [InlineData (100, 28)]
    [InlineData (140, 32)]
    public void WorkflowButtonsRemainVisibleAndSeparateAfterResize (int width, int height)
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1);
        BatchPlan plan = new ("Upgrade", [new (Tool (), true, "1 → 2")]);
        RunRecord run = new (DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Upgrade", [Failed ()]);
        using BatchPlanDialog review = new (plan);
        using RunRecordDialog results = new (run);
        using PackageActionDialog actions = new ("Tool · 2 selected", PackageActions.For (AppMode.Upgrades, Tool (), true, true, true, 2, null));
        using RunDiagnosticsDialog diagnostics = new (Failed ());
        diagnostics.ConfigureActions (() => { }, _ => { }, null);
        using AdvancedInstallDialog advanced = new ("Tool");
        using VersionInputDialog versionInput = new ("Tool");
        using VersionPickerDialog versionPicker = new ("Tool", ["2", "1"]);
        using ThemePickerDialog themes = new ();
        using TextPromptDialog prompt = new ("Set name", "Name:");
        using SetManagerDialog sets = new ([], 2);
        using RunHistoryDialog history = new ([run]);
        using ScheduleDialog schedule = new (new (false, "09:00", false), null, false);
        foreach (View dialog in new View [] { review, results, actions, diagnostics, advanced,
            versionInput, versionPicker, themes, prompt, sets, history, schedule })
        {
            Terminal.Gui.App.SessionToken session = ui.Application.Begin ((Terminal.Gui.App.IRunnable)dialog)!;
            try
            {
                ui.Window.Width = width; ui.Window.Height = height;
                // Driverless screen size is the parent window's size, so constrain the modal explicitly.
                if (dialog == review || dialog == results || dialog == actions || dialog == diagnostics || dialog == sets || dialog == history)
                {
                    dialog.Width = (int)((width - 2) * (dialog == actions ? .65 : dialog == diagnostics ? .85 : dialog == history ? .7 : .78));
                    dialog.Height = (int)((height - 2) * (dialog == diagnostics ? .8 : .65));
                }
                ui.Application.LayoutAndDraw ();
                Button [] buttons = dialog.SubViews.OfType<Button> ().Where (b => b.Visible).ToArray ();
                foreach (Button button in buttons)
                {
                    // The optional one-cell button shadow is outside its interactive surface.
                    Assert.True (button.Frame.X >= 0 && button.Frame.Right - (button.Margin?.Thickness.Right ?? 0) <= dialog.Viewport.Width,
                        $"{dialog.Title} {button.Text}: {button.Frame} in {dialog.Viewport}");
                    Assert.True (button.Frame.Y >= 0 && button.Frame.Bottom - (button.Margin?.Thickness.Bottom ?? 0) <= dialog.Viewport.Height,
                        $"{dialog.Title} {button.Text}: {button.Frame} in {dialog.Viewport}");
                }
                for (int a = 0; a < buttons.Length; a++)
                    for (int b = a + 1; b < buttons.Length; b++)
                        Assert.False (buttons[a].Frame.X < buttons[b].Frame.Right - (buttons[b].Margin?.Thickness.Right ?? 0)
                            && buttons[a].Frame.Right - (buttons[a].Margin?.Thickness.Right ?? 0) > buttons[b].Frame.X
                            && buttons[a].Frame.Y < buttons[b].Frame.Bottom - (buttons[b].Margin?.Thickness.Bottom ?? 0)
                            && buttons[a].Frame.Bottom - (buttons[a].Margin?.Thickness.Bottom ?? 0) > buttons[b].Frame.Y,
                            $"{buttons[a].Text} overlaps {buttons[b].Text}");
            }
            finally { ui.Application.End (session); }
        }
    }

    [Theory]
    [InlineData (AppMode.Search, 80)]
    [InlineData (AppMode.Search, 140)]
    [InlineData (AppMode.Installed, 80)]
    [InlineData (AppMode.Installed, 140)]
    [InlineData (AppMode.Upgrades, 80)]
    [InlineData (AppMode.Upgrades, 140)]
    public void SeededNavigationSelectionFilterAndResizeSweepKeepsUiConsistent (AppMode mode, int width)
    {
        using ListNavigationTests.UiSession ui = new (mode, 40);
        Random random = new (971 + (int)mode);
        for (int step = 0; step < 120; step++)
        {
            switch (random.Next (9))
            {
                case 0: ui.Press (KeyCode.CursorDown); break;
                case 1: ui.Press (KeyCode.CursorUp); break;
                case 2: ui.Press (KeyCode.End); break;
                case 3: ui.Press (KeyCode.Home); break;
                case 4: ui.Press (new Key (' ')); break;
                case 5: ui.Press (new Key ('a')); break;
                case 6: ui.Press (new Key ('S')); break;
                case 7:
                    ui.Window.Width = step % 2 == 0 ? width : 100;
                    ui.Window.Height = step % 2 == 0 ? 24 : 32;
                    ui.Application.LayoutAndDraw ();
                    break;
                case 8:
                    if (mode == AppMode.Search) break;
                    ui.Press (new Key ('/'));
                    ui.Get<TextField> ("_filterInput").Text = step % 3 == 0 ? "no results" : step % 3 == 1 ? "Package 00" : "";
                    ui.Press (KeyCode.Enter);
                    break;
            }
            ui.AssertSelection ();
            IEnumerable<string> selected = mode == AppMode.Upgrades ? ui.State.BatchSelected
                : mode == AppMode.Search ? ui.Get<Dictionary<string, Package>> ("_searchSelected").Keys : [];
            ListCounts counts = ListCounts.Capture (ui.State.Filtered, ui.State.Packages.Count, selected);
            Assert.Contains (counts.Label, ui.Get<FrameView> ("_listFrame").Title);
            Assert.Equal (counts.HiddenSelected, ui.Get<StatusBar> ("_statusBar").HiddenSelectionCount);
            if (counts.Visible > 0) Assert.Equal (counts.Visible, ui.Table.Table!.Rows);
        }
    }
}
