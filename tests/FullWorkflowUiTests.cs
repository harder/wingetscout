using Terminal.Gui.App;
using static WinGetScout.Tests.RunWorkflowTests;

namespace WinGetScout.Tests;

[Collection ("Terminal UI")]
public sealed class FullWorkflowUiTests
{
    [Fact]
    public void HelpCanReachEverySectionByKeyboardAndKeepsTextReadOnly ()
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Installed, 4);
        bool visited = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            HelpDialog help = Assert.IsType<HelpDialog> (ui.Application.TopRunnable);
            help.Width = 80;
            help.Height = 20;
            ui.Application.LayoutAndDraw ();
            Code content = Assert.Single (help.SubViews.OfType<Code> ());
            string text = content.Text;
            content.SetFocus ();
            ui.Press (KeyCode.PageDown);
            Assert.True (content.Viewport.Y > 0, $"Help must scroll using Page Down. Viewport {content.Viewport}, content {content.GetContentSize ()}, focus {content.HasFocus}, dialog {help.Frame}.");
            ui.Press (KeyCode.End);
            Assert.True (content.Viewport.Bottom >= content.GetContentSize ().Height);
            ui.Press (new Key ('z'));
            Assert.Equal (text, content.Text);
            ui.Press (KeyCode.Home);
            Assert.Equal (0, content.Viewport.Y);
            ui.Press (KeyCode.Tab);
            Assert.IsType<Button> (help.MostFocused);
            ui.Press (KeyCode.Esc);
            visited = true;
            return false;
        });
        ui.Press (new Key ('?'));
        Assert.True (visited);
        Assert.True (ui.Table.HasFocus);
    }

    [Fact]
    public void ThemeChangesKeepTheHighlightedPackageAndMarkedPackages ()
    {
        string original = Theme.CurrentPaletteName;
        try
        {
            using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 30);
            ui.Press (KeyCode.End);
            ui.Press (new Key (' '));
            string id = ui.State.CurrentDetail!.Id;
            for (int i = 0; i < Theme.Palettes.Count; i++)
            {
                int choice = i;
                ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
                {
                    ThemePickerDialog picker = Assert.IsType<ThemePickerDialog> (ui.Application.TopRunnable);
                    ListView list = Assert.Single (picker.SubViews.OfType<ListView> ());
                    list.SelectedItem = choice;
                    Click (ui, picker, "Apply");
                    return false;
                });
                ui.Press (new Key ('t'));
                Assert.Equal (Theme.Palettes [choice].Id, Theme.CurrentPaletteName);
                Assert.Equal (id, ui.State.SelectedPackage (ui.Table.Value!.SelectedCell.Y)!.Id);
                Assert.Single (ui.State.BatchSelected);
                ui.AssertSelection ();
                Assert.True (ui.Table.HasFocus);
            }
        }
        finally { Theme.TryApply (original); }
    }

    [Theory]
    [InlineData ("")]
    [InlineData ("   ")]
    public void BlankVersionStaysInTheDialogWithUsefulFeedback (string value)
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Search, 1);
        using VersionInputDialog dialog = new ("Tool");
        SessionToken session = ui.Application.Begin (dialog)!;
        try
        {
            Assert.Single (dialog.SubViews.OfType<TextField> ()).Text = value;
            Click (ui, dialog, "Confirm");
            Assert.False (dialog.StopRequested);
            Assert.Null (dialog.Result);
            Assert.Contains (dialog.SubViews.OfType<Label> (), label => label.Text.Contains ("Enter a version"));
            Assert.IsType<TextField> (dialog.MostFocused);
        }
        finally { ui.Application.End (session); }
    }

    [Fact]
    public void VersionEntryTrimsAccidentalOuterWhitespace ()
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Search, 1);
        using VersionInputDialog dialog = new ("Tool");
        SessionToken session = ui.Application.Begin (dialog)!;
        try
        {
            Assert.Single (dialog.SubViews.OfType<TextField> ()).Text = "  2.1.0-preview  ";
            Click (ui, dialog, "Confirm");
            Assert.Equal ("2.1.0-preview", dialog.Result);
        }
        finally { ui.Application.End (session); }
    }

    public static IEnumerable<object []> AdvancedOptions () =>
        from scope in Enum.GetValues<InstallScopePref> ()
        from mode in Enum.GetValues<InstallModePref> ()
        from arch in Enum.GetValues<InstallArchPref> ()
        select new object [] { scope, mode, arch };

    [Theory]
    [MemberData (nameof (AdvancedOptions))]
    public void EveryAdvancedOptionCombinationStillRequiresFinalReview (
        InstallScopePref scope, InstallModePref mode, InstallArchPref arch)
    {
        RecordingBackend backend = new ();
        using ListNavigationTests.UiSession ui = new (AppMode.Search, 1, backend);
        bool options = false, confirmation = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (ui.Application.TopRunnable is AdvancedInstallDialog advanced)
            {
                OptionSelector [] selectors = advanced.SubViews.OfType<OptionSelector> ().ToArray ();
                int [] choices = [(int)scope, (int)mode, (int)arch];
                for (int i = 0; i < selectors.Length; i++)
                {
                    selectors [i].SetFocus ();
                    for (int step = 0; step < choices [i]; step++) ui.Press (KeyCode.CursorRight);
                    ui.Press (new Key (' '));
                }
                Assert.Single (advanced.SubViews.OfType<TextField> ()).Text = "/feature-test";
                Assert.Equal (0, backend.Mutations);
                options = true;
                Click (ui, advanced, "Review");
                Assert.Equal (scope, advanced.Result!.Scope);
                Assert.Equal (mode, advanced.Result.Mode);
                Assert.Equal (arch, advanced.Result.Architecture);
                Assert.Equal ("/feature-test", advanced.Result.CustomArgs);
            }
            else if (options && ui.Application.TopRunnable is Dialog review)
            {
                Assert.Contains ("Install", review.Title);
                Assert.Equal (0, backend.Mutations);
                confirmation = true;
                ui.Press (KeyCode.Esc);
                return false;
            }
            return true;
        });
        ui.Press (new Key ('A'));
        ui.PumpUntil (() => confirmation && !ui.State.Loading);
        Assert.Equal (0, backend.Mutations);
        Assert.True (ui.Table.HasFocus);
    }

    [Theory]
    [InlineData ("i", AppMode.Search, OperationKind.Install)]
    [InlineData ("u", AppMode.Upgrades, OperationKind.Upgrade)]
    [InlineData ("x", AppMode.Installed, OperationKind.Uninstall)]
    [InlineData ("d", AppMode.Search, OperationKind.Download)]
    [InlineData ("R", AppMode.Installed, OperationKind.Repair)]
    [InlineData ("p", AppMode.Installed, OperationKind.Pin)]
    [InlineData ("p", AppMode.Installed, OperationKind.Unpin)]
    public void MutationShortcutsAreSafeToDeclineThenExecuteExactlyOnce (
        string shortcut, AppMode mode, OperationKind kind)
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-all-actions-" + Guid.NewGuid ().ToString ("N"));
        try
        {
            WorkflowStore store = new (root);
            RecordingBackend backend = new () { CanRepair = true };
            using ListNavigationTests.UiSession ui = new (mode, 1, backend, store);
            ui.State.RecordPinSnapshot (kind == OperationKind.Unpin
                ? new Dictionary<string, PinState> { [ui.State.Packages [0].Id] = new (PinStateKind.Pinned) }
                : new Dictionary<string, PinState> ());
            ui.State.ApplyPinSnapshot (ui.State.Packages);
            foreach (bool accept in new [] { false, true })
            {
                bool reviewed = false;
                ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
                {
                    if (ui.Application.TopRunnable is not Dialog review) return true;
                    Assert.Equal (0, backend.Mutations);
                    Assert.Contains (kind.ToString (), review.Title, StringComparison.OrdinalIgnoreCase);
                    reviewed = true;
                    if (accept) Click (ui, review, "Yes");
                    else ui.Press (KeyCode.Esc);
                    return false;
                });
                ui.Press (new Key (shortcut [0]));
                ui.PumpUntil (() => reviewed && !ui.State.Loading);
                Assert.Equal (accept ? 1 : 0, backend.Mutations);
                Assert.Equal (accept ? 1 : 0, store.Runs ().Count);
                if (accept) Assert.Equal (kind, backend.LastKind);
                Assert.True (ui.Table.HasFocus);
            }
        }
        finally { if (Directory.Exists (root)) Directory.Delete (root, true); }
    }

    [Theory]
    [InlineData (false)]
    [InlineData (true)]
    public void PinFilteredListKeepsHighlightWhenThePackageStillMatches (bool pinned)
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Installed, 6);
        ui.State.RecordPinSnapshot (new Dictionary<string, PinState> ());
        foreach (Package package in ui.State.Packages)
            package.PinState = pinned ? new (PinStateKind.Pinned) : PinState.Unpinned;
        ui.Press (KeyCode.End);
        string id = ui.State.CurrentDetail!.Id;
        // All -> pinned -> unpinned -> all. Only assert identity while it remains visible.
        if (!pinned) ui.State.PinFilter = PinFilter.PinnedOnly;
        ui.Press (new Key ('P'));
        Assert.Equal (id, ui.State.SelectedPackage (ui.Table.Value!.SelectedCell.Y)?.Id);
        ui.AssertSelection ();
    }

    public static IEnumerable<object []> DialogExits () =>
        from kind in new [] { "version", "versions", "advanced", "theme", "help", "plan", "results", "diagnostics", "actions", "prompt", "sets", "history", "schedule" }
        from exit in new [] { "escape", "button", "quit" }
        select new object [] { kind, exit };

    [Theory]
    [MemberData (nameof (DialogExits))]
    public void EveryDialogCanBeAbandonedWithoutStartingAnOperation (string kind, string exit)
    {
        RecordingBackend backend = new ();
        using ListNavigationTests.UiSession ui = new (AppMode.Search, 1, backend);
        RunRecord run = new (DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Install", [Failed ()]);
        using Runnable dialog = kind switch
        {
            "version" => new VersionInputDialog ("Tool"),
            "versions" => new VersionPickerDialog ("Tool", ["2", "1"]),
            "advanced" => new AdvancedInstallDialog ("Tool"),
            "theme" => new ThemePickerDialog (),
            "help" => new HelpDialog ("Test backend"),
            "plan" => new BatchPlanDialog (new ("Install", [new (Tool (), true, "Ready")])),
            "results" => new RunRecordDialog (run),
            "diagnostics" => new RunDiagnosticsDialog (Failed ()),
            "actions" => new PackageActionDialog ("Tool", [new ("install", "Install")]),
            "prompt" => new TextPromptDialog ("Save set", "Name:"),
            "sets" => new SetManagerDialog ([], 0),
            "history" => new RunHistoryDialog ([run]),
            _ => new ScheduleDialog (new (false, "09:00", true), null, canSchedule: false)
        };
        bool exited = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            Assert.Same (dialog, ui.Application.TopRunnable);
            ui.Application.LayoutAndDraw ();
            if (dialog.SubViews.OfType<TextField> ().FirstOrDefault (v => v.Enabled) is { } input)
            {
                input.Text = "inert test value";
                input.SetFocus ();
            }
            if (exit == "button")
                Click (ui, dialog, kind is "version" or "versions" or "advanced" or "theme" or "plan" or "actions" or "prompt" ? "Cancel" : "Close");
            else ui.Press (exit == "quit" ? KeyCode.Q | KeyCode.CtrlMask : KeyCode.Esc);
            Assert.True (dialog.StopRequested, $"{kind} did not close on {exit}.");
            exited = true;
            return false;
        });
        ui.Application.Run (dialog);
        Assert.True (exited);
        Assert.Equal (0, backend.Mutations);
        if (exit != "quit") Assert.True (ui.Table.HasFocus);
        else Assert.True (ui.Window.StopRequested);
    }

    [Theory]
    [InlineData (false, false)]
    [InlineData (false, true)]
    [InlineData (true, false)]
    [InlineData (true, true)]
    public void SpecificVersionInstallRequiresReviewAndPreservesTheChosenVersion (bool manual, bool approve)
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-version-ui-" + Guid.NewGuid ().ToString ("N"));
        try
        {
            RecordingBackend backend = new () { Versions = _ => manual ? [] : ["3", "2", "1"] };
            using ListNavigationTests.UiSession ui = new (AppMode.Search, 1, backend, new WorkflowStore (root));
            bool picked = false, reviewed = false;
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                if (ui.Application.TopRunnable is VersionPickerDialog picker)
                {
                    Assert.Equal (0, backend.Mutations);
                    ListView list = Assert.Single (picker.SubViews.OfType<ListView> ());
                    list.SetFocus (); ui.Press (KeyCode.CursorDown);
                    Click (ui, picker, "Install"); picked = true;
                }
                else if (ui.Application.TopRunnable is VersionInputDialog input)
                {
                    Assert.Equal (0, backend.Mutations);
                    Assert.Single (input.SubViews.OfType<TextField> ()).Text = "2";
                    Click (ui, input, "Confirm"); picked = true;
                }
                else if (picked && ui.Application.TopRunnable is Dialog review)
                {
                    Assert.Equal (0, backend.Mutations);
                    reviewed = true;
                    if (approve) Click (ui, review, "Yes");
                    else ui.Press (KeyCode.Esc);
                    return false;
                }
                return true;
            });
            ui.Press (new Key ('I'));
            ui.PumpUntil (() => reviewed && !ui.State.Loading);
            Assert.Equal (approve ? 1 : 0, backend.Mutations);
            if (approve)
            {
                Assert.Equal ("2", backend.LastVersion);
                Assert.Equal ("winget", backend.LastSource);
            }
            Assert.True (ui.Table.HasFocus);
        }
        finally { if (Directory.Exists (root)) Directory.Delete (root, true); }
    }

    [Theory]
    [InlineData (VerifyOutcome.Ok, false)]
    [InlineData (VerifyOutcome.NotApplicable, false)]
    [InlineData (VerifyOutcome.Error, false)]
    [InlineData (VerifyOutcome.Issues, false)]
    [InlineData (VerifyOutcome.Issues, true)]
    public void VerificationOnlyRepairsAfterExplicitApproval (VerifyOutcome outcome, bool repair)
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-verify-ui-" + Guid.NewGuid ().ToString ("N"));
        try
        {
            RecordingBackend backend = new () { CanRepair = true, Verification = new ()
            {
                Outcome = outcome, Checks = [new ("Registration", true, "Found"), new ("Files", false, "Missing")]
            } };
            using ListNavigationTests.UiSession ui = new (AppMode.Installed, 1, backend, new WorkflowStore (root));
            bool reviewed = false;
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                if (ui.Application.TopRunnable is not Dialog review) return true;
                Assert.Contains ("Verify", review.Title);
                Assert.Equal (0, backend.Mutations);
                reviewed = true;
                if (repair) Click (ui, review, "Repair");
                else ui.Press (KeyCode.Esc);
                return false;
            });
            ui.Press (new Key ('V'));
            ui.PumpUntil (() => reviewed && !ui.State.Loading);
            Assert.Equal (repair ? 1 : 0, backend.Mutations);
            if (repair) Assert.Equal (OperationKind.Repair, backend.LastKind);
        }
        finally { if (Directory.Exists (root)) Directory.Delete (root, true); }
    }

    [Theory]
    [InlineData ("25:00")]
    [InlineData ("9am")]
    [InlineData ("")]
    public void ScheduleValidationCanBeCorrectedWithoutLosingNotificationPreference (string invalidTime)
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1);
        using ScheduleDialog dialog = new (new (false, "09:00", true), null);
        SessionToken session = ui.Application.Begin (dialog)!;
        try
        {
            TextField time = Assert.Single (dialog.SubViews.OfType<TextField> ());
            OptionSelector notify = Assert.Single (dialog.SubViews.OfType<OptionSelector> ());
            notify.Value = 1; time.Text = invalidTime;
            Click (ui, dialog, "Enable / save");
            Assert.False (dialog.StopRequested);
            Assert.Null (dialog.Result);
            Assert.Contains (dialog.SubViews.OfType<Label> (), l => l.Text.Contains ("24-hour"));
            time.Text = "17:45";
            Click (ui, dialog, "Enable / save");
            Assert.Equal (new ("Save", "17:45", false), dialog.Result);
        }
        finally { ui.Application.End (session); }
    }

    [Fact]
    public void ExportUsesVisibleRowsAndReportsWhereTheFileWasSaved ()
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-export-ui-" + Guid.NewGuid ().ToString ("N"));
        string previous = Environment.CurrentDirectory;
        Directory.CreateDirectory (root);
        try
        {
            Environment.CurrentDirectory = root;
            using ListNavigationTests.UiSession ui = new (AppMode.Installed, 6);
            ui.Press (new Key ('/'));
            ui.Get<TextField> ("_filterInput").Text = "005";
            ui.Press (KeyCode.Enter);
            ui.Press (new Key ('e'));
            ui.PumpUntil (() => !ui.State.Loading);
            string path = Path.Combine (root, "wingetscout-export.csv");
            string [] lines = File.ReadAllLines (path);
            Assert.Equal (2, lines.Length);
            Assert.Contains ("Example.005", lines [1]);
            Assert.Contains (path, ui.State.StatusMessage);
            Assert.False (ui.State.StatusIsError);
            ui.Press (new Key ('e'));
            ui.PumpUntil (() => !ui.State.Loading);
            Assert.Equal (lines, File.ReadAllLines (path));
        }
        finally
        {
            Environment.CurrentDirectory = previous;
            Directory.Delete (root, true);
        }
    }

    [Theory]
    [InlineData (false)]
    [InlineData (true)]
    public void SingleUpgradeShortcutRespectsPinSafetyLikeBatchAndContextActions (bool stale)
    {
        RecordingBackend backend = new ();
        using ListNavigationTests.UiSession ui = new (AppMode.Upgrades, 1, backend);
        Package package = ui.State.Packages [0];
        if (!stale)
        {
            ui.State.RecordPinSnapshot (new Dictionary<string, PinState> { [package.Id] = new (PinStateKind.Pinned) });
            ui.State.ApplyPinSnapshot (ui.State.Packages);
        }
        bool prompted = false;
        object? timer = ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (ui.Application.TopRunnable is Dialog review) { prompted = true; review.RequestStop (); }
            return false;
        });
        ui.Press (new Key ('u'));
        if (timer is not null) ui.Application.RemoveTimeout (timer);
        Assert.False (prompted, "An unsafe single upgrade must explain the blocked action instead of offering execution.");
        Assert.Equal (0, backend.Mutations);
        Assert.Contains (stale ? "Pin status is unavailable" : "Unpin", ui.State.StatusMessage);
    }

    [Fact]
    public void SavedSetSaveLoadAndDeleteFlowKeepsReviewBeforeExecution ()
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-sets-ui-" + Guid.NewGuid ().ToString ("N"));
        try
        {
            WorkflowStore store = new (root);
            RecordingBackend backend = new ();
            using ListNavigationTests.UiSession ui = new (AppMode.Search, 4, backend, store);
            ui.Press (new Key ('a'));
            bool named = false;
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                if (ui.Application.TopRunnable is SetManagerDialog manager) Click (ui, manager, "Save current");
                else if (ui.Application.TopRunnable is TextPromptDialog prompt)
                {
                    Assert.Single (prompt.SubViews.OfType<TextField> ()).Text = "UI test selection";
                    Click (ui, prompt, "Save"); named = true; return false;
                }
                return true;
            });
            ui.Press (new Key ('g'));
            Assert.True (named);
            Assert.Equal (4, Assert.Single (store.Sets ()).Packages.Count);
            ui.Press (KeyCode.Esc);
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                Click (ui, Assert.IsType<SetManagerDialog> (ui.Application.TopRunnable), "Load"); return false;
            });
            ui.Press (new Key ('g'));
            Assert.Equal (4, ui.Get<StatusBar> ("_statusBar").InstallSelectionCount);
            Assert.Contains ("4 selected", ui.Get<FrameView> ("_listFrame").Title);
            Assert.Equal (0, backend.Mutations);
            foreach (bool approve in new [] { false, true })
            {
                bool confirmed = false;
                ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
                {
                    if (ui.Application.TopRunnable is SetManagerDialog manager) Click (ui, manager, "Delete");
                    else if (ui.Application.TopRunnable is Dialog review)
                    {
                        Assert.Contains ("Delete set", review.Title);
                        confirmed = true;
                        if (approve) Click (ui, review, "Yes");
                        else ui.Press (KeyCode.Esc);
                        return false;
                    }
                    return true;
                });
                ui.Press (new Key ('g'));
                Assert.True (confirmed);
                Assert.Equal (approve ? 0 : 1, store.Sets ().Count);
                Assert.Equal (4, ui.Get<StatusBar> ("_statusBar").InstallSelectionCount);
            }
            Assert.Equal (0, backend.Mutations);
        }
        finally { if (Directory.Exists (root)) Directory.Delete (root, true); }
    }

    public static IEnumerable<object []> ListSizes () =>
        from mode in Enum.GetValues<AppMode> ()
        from width in new [] { 80, 90, 100, 120, 140, 190 }
        select new object [] { mode, width };

    [Theory]
    [MemberData (nameof (ListSizes))]
    public void NarrowListsKeepVersionColumnsVisibleAndRestoreColumnsWhenWidened (AppMode mode, int width)
    {
        using ListNavigationTests.UiSession ui = new (mode, 30);
        ui.Press (KeyCode.End);
        string id = ui.State.CurrentDetail!.Id;
        ui.Window.Width = width;
        ui.Window.Height = 24;
        ui.Application.LayoutAndDraw ();
        Assert.True (ui.Table.GetContentSize ().Width <= ui.Table.Viewport.Width,
            $"{mode} content {ui.Table.GetContentSize ()} exceeds viewport {ui.Table.Viewport} at {width} columns.");
        foreach (int column in Enumerable.Range (0, ui.Table.Table!.Columns))
        {
            string header = ui.Table.Table.ColumnNames [column];
            if (header.StartsWith ("Version") || header.StartsWith ("Available"))
                Assert.True (ui.Table.Style.GetOrCreateColumnStyle (column).Visible);
        }
        for (int i = 0; i < 10; i++)
        {
            ui.Press (new Key ('S'));
            Assert.True (ui.Table.GetContentSize ().Width <= ui.Table.Viewport.Width);
            foreach (int column in Enumerable.Range (0, ui.Table.Table.Columns))
            {
                ColumnStyle style = ui.Table.Style.GetOrCreateColumnStyle (column);
                if (style.Visible)
                    Assert.True (style.MinWidth >= ui.Table.Table.ColumnNames [column].GetColumns (),
                        $"{ui.Table.Table.ColumnNames [column]} cannot show its header/sort direction at {width} columns.");
            }
        }
        Assert.Equal (id, ui.State.SelectedPackage (ui.Table.Value!.SelectedCell.Y)!.Id);
        ui.Window.Width = 190;
        ui.Application.LayoutAndDraw ();
        Assert.All (Enumerable.Range (0, ui.Table.Table.Columns),
            column => Assert.True (ui.Table.Style.GetOrCreateColumnStyle (column).Visible));
        ui.Press (KeyCode.CursorUp);
        ui.AssertSelection ();
    }

    internal static void Click (ListNavigationTests.UiSession ui, View dialog, string text)
    {
        IEnumerable<Button> buttons = dialog is Dialog modal ? modal.Buttons! : dialog.SubViews.OfType<Button> ();
        Button button = Assert.Single (buttons, b => b.Text.Replace ("_", "").Contains (text, StringComparison.Ordinal));
        Assert.True (button.Enabled);
        button.SetFocus ();
        ui.Press (KeyCode.Enter);
    }
}
