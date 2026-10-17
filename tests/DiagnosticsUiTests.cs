using Terminal.Gui.App;
using static WinGetScout.Tests.RunWorkflowTests;

namespace WinGetScout.Tests;

[Collection ("Terminal UI")]
public sealed class DiagnosticsUiTests
{
    private static void PressButton (ListNavigationTests.UiSession ui, RunDiagnosticsDialog dialog, string contains)
    {
        Button button = Assert.Single (dialog.SubViews.OfType<Button> (), b => b.Text.Contains (contains));
        Assert.True (button.Enabled);
        button.SetFocus (); ui.Press (KeyCode.Enter);
    }

    [Fact]
    public void BackendPanelRefreshesPartialFailuresAndCopiesTheCurrentSnapshot ()
    {
        RecordingBackend backend = new ()
        {
            Sources = ["winget", "broken"],
            SearchFailure = source => source == "broken" ? new InvalidOperationException ("offline") : null
        };
        string? copied = null;
        using ListNavigationTests.UiSession ui = new (AppMode.Installed, 1, backend, copyDiagnosticText: text => { copied = text; return true; });
        ui.State.ViewError = "Earlier inventory error";
        int phase = 0;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (ui.Application.TopRunnable is not RunDiagnosticsDialog dialog) return true;
            if (phase == 0 && dialog.Document.Text.Contains ("broken: query failed"))
            {
                Assert.Contains ("Earlier inventory error", dialog.Document.Text);
                PressButton (ui, dialog, "Copy");
                Assert.Equal (dialog.Document.Text, copied);
                backend.SearchFailure = null;
                phase = 1; PressButton (ui, dialog, "Refresh");
            }
            else if (phase == 1 && dialog.Document.Text.Contains ("broken: query succeeded"))
            {
                Assert.Equal (0, backend.Mutations);
                phase = 2; ui.Press (KeyCode.Esc); return false;
            }
            return true;
        });
        ui.Press (new Key ('D'));
        Assert.Equal (2, phase);
        Assert.Equal (0, backend.Mutations);
        Assert.Same (ui.Window, ui.Application.TopRunnable);
    }

    [Theory]
    [InlineData (false)]
    [InlineData (true)]
    public void RunDiagnosticsCopyAndSaveTheReviewedSnapshot (bool json)
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-diagnostic-ui-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (root);
        try
        {
            string path = Path.Combine (root, json ? "run.json" : "run.txt");
            string? copied = null;
            RecordingBackend backend = new ();
            RunItem item = Failed () with { Diagnostics = "Full multiline\nFinal error", ExitCode = 1603 };
            using ListNavigationTests.UiSession ui = new (AppMode.Installed, 1, backend,
                copyDiagnosticText: text => { copied = text; return true; }, diagnosticSavePath: _ => path);
            int phase = 0;
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                if (ui.Application.TopRunnable is not RunDiagnosticsDialog dialog) return true;
                if (phase == 0)
                {
                    PressButton (ui, dialog, "Copy"); Assert.Equal (item.DetailText (), copied);
                    phase = 1; PressButton (ui, dialog, json ? "JSON" : "text");
                }
                else if (dialog.StatusText.StartsWith ("Saved to"))
                { phase = 2; ui.Press (KeyCode.Esc); return false; }
                return true;
            });
            ui.Invoke ("ShowRunDiagnostics", item);
            Assert.Equal (2, phase);
            Assert.Equal (json ? DiagnosticDocument.ForRun (item).Json : item.DetailText (), File.ReadAllText (path));
            Assert.Equal (0, backend.Mutations);
        }
        finally { Directory.Delete (root, true); }
    }

    [Theory]
    [InlineData (false)]
    [InlineData (true)]
    public void ClipboardFailuresRemainInTheViewerAndSaveCanBeCancelled (bool throws)
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Installed, 1,
            copyDiagnosticText: _ => throws ? throw new InvalidOperationException ("clipboard busy") : false,
            diagnosticSavePath: _ => null);
        bool checkedActions = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            RunDiagnosticsDialog dialog = Assert.IsType<RunDiagnosticsDialog> (ui.Application.TopRunnable);
            PressButton (ui, dialog, "Copy");
            Assert.Contains (throws ? "clipboard busy" : "Clipboard unavailable", dialog.StatusText);
            PressButton (ui, dialog, "text");
            Assert.Equal ("Save cancelled.", dialog.StatusText);
            checkedActions = true; ui.Press (KeyCode.Esc); return false;
        });
        ui.Invoke ("ShowRunDiagnostics", Failed ());
        Assert.True (checkedActions);
    }

    [Fact]
    public void FailedSaveKeepsTheReportAndReenablesActionsForAnotherSave ()
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-diagnostic-recovery-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (root);
        try
        {
            string path = Path.Combine (root, "missing", "report.txt");
            DiagnosticDocument document = DiagnosticDocument.ForRun (Failed ());
            using ListNavigationTests.UiSession ui = new (AppMode.Installed, 1, diagnosticSavePath: _ => path);
            int phase = 0;
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                if (ui.Application.TopRunnable is not RunDiagnosticsDialog viewer) return true;
                if (phase == 0)
                {
                    phase = 1; PressButton (ui, viewer, "text");
                }
                else if (phase == 1 && viewer.StatusText.StartsWith ("Failed:"))
                {
                    Assert.Equal (document, viewer.Document);
                    Assert.Empty (Directory.GetFiles (root));
                    path = Path.Combine (root, "report.txt");
                    phase = 2; PressButton (ui, viewer, "text");
                }
                else if (phase == 2 && viewer.StatusText.StartsWith ("Saved to"))
                {
                    Assert.Equal (document.Text, File.ReadAllText (path));
                    Assert.Equal (path, Assert.Single (Directory.GetFiles (root)));
                    phase = 3; ui.Press (KeyCode.Esc); return false;
                }
                return true;
            });
            ui.Invoke ("ShowRunDiagnostics", Failed ());
            Assert.Equal (3, phase);
        }
        finally { Directory.Delete (root, true); }
    }

    [Fact]
    public void AdvancedOptionsExplainDefaultsAndReturnToReviewWithoutExecuting ()
    {
        RecordingBackend backend = new () { DefaultInstallMode = InstallModePref.Silent };
        using ListNavigationTests.UiSession ui = new (AppMode.Search, 1, backend);
        bool reviewed = false, confirmed = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (ui.Application.TopRunnable is AdvancedInstallDialog options)
            {
                ui.Application.LayoutAndDraw ();
                Assert.All (options.SubViews.OfType<OptionSelector> (), selector => Assert.Equal (1, selector.Frame.Height));
                Assert.Contains (options.SubViews.OfType<Label> (), label => label.Text.Contains ("Default — Silent"));
                OptionSelector scope = options.SubViews.OfType<OptionSelector> ().First ();
                scope.Value = (int)InstallScopePref.User;
                Assert.Contains (options.SubViews.OfType<Label> (), label => label.Text.Contains ("User (override)"));
                reviewed = true;
                Button review = Assert.Single (options.SubViews.OfType<Button> (), b => b.Text.Contains ("Review"));
                review.SetFocus (); ui.Press (KeyCode.Enter);
            }
            else if (reviewed && ui.Application.TopRunnable is Dialog confirmation)
            {
                Assert.Equal (0, backend.Mutations);
                confirmed = true; ui.Press (KeyCode.Esc); return false;
            }
            return true;
        });
        ui.Invoke ("AskAdvancedInstall", ui.State.Packages [0]);
        ui.PumpUntil (() => confirmed && !ui.State.Loading);
        Assert.Equal (0, backend.Mutations);
    }

    [Theory]
    [InlineData ("arm64")]
    [InlineData ("arm")]
    public void ConflictingInstallerPreviewCannotReachConfirmationOrExecution (string architecture)
    {
        RecordingBackend backend = new () { Preview = new () { Architecture = architecture, Scope = "machine" } };
        using ListNavigationTests.UiSession ui = new (AppMode.Search, 1, backend);
        bool reachedConfirmation = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (ui.Application.TopRunnable is not Dialog) return true;
            reachedConfirmation = true; ui.Press (KeyCode.Esc); return false;
        });
        ui.Invoke ("ConfirmAndInstall", ui.State.Packages [0], null!, new InstallSettings { Architecture = InstallArchPref.X64 });
        ui.PumpUntil (() => !ui.State.Loading);
        Assert.False (reachedConfirmation);
        Assert.Contains ("Requested X64", ui.State.StatusMessage);
        Assert.Same (ui.Window, ui.Application.TopRunnable);
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public async Task ClosingThePanelCancelsPendingChecksAndLateResultsCannotReopenIt ()
    {
        TaskCompletionSource<IReadOnlyList<Package>> query = new (TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken probeToken = default;
        RecordingBackend backend = new () { CatalogQuery = (_, ct) => { probeToken = ct; return query.Task; } };
        using ListNavigationTests.UiSession ui = new (AppMode.Installed, 1, backend);
        bool closed = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            if (!probeToken.CanBeCanceled) return true;
            RunDiagnosticsDialog viewer = Assert.IsType<RunDiagnosticsDialog> (ui.Application.TopRunnable);
            Assert.All (viewer.SubViews.OfType<Button> ().Where (b => !b.Text.Contains ("lose")), button => Assert.False (button.Enabled));
            closed = true; ui.Press (KeyCode.Esc); return false;
        });
        ui.Press (new Key ('D'));
        Assert.True (closed);
        Assert.True (probeToken.IsCancellationRequested);
        Task completion = ui.CaptureBackgroundWorkCompletion ();
        Assert.False (completion.IsCompleted);
        query.SetResult ([Tool ()]);
        ui.PumpUntil (() => completion.IsCompleted);
        Assert.True (completion.IsCompletedSuccessfully);
        Assert.Same (ui.Window, ui.Application.TopRunnable);
        Assert.True (await ui.Window.ShutdownAsync (TimeSpan.FromSeconds (2)));
        Assert.Empty (ui.Window.BackgroundFailures);
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public void ReplacingAnExistingExportRequiresExplicitConfirmation ()
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-diagnostic-replace-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (root);
        try
        {
            string path = Path.Combine (root, "existing.txt");
            File.WriteAllText (path, "Keep this file");
            using ListNavigationTests.UiSession ui = new (AppMode.Installed, 1, diagnosticSavePath: _ => path);
            bool declined = false;
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                RunDiagnosticsDialog viewer = Assert.IsType<RunDiagnosticsDialog> (ui.Application.TopRunnable);
                // The outer callback is suspended inside a synchronous nested modal.
                // Give that modal its own timer, as the real app loop would.
                ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
                {
                    Dialog confirmation = Assert.IsType<Dialog> (ui.Application.TopRunnable);
                    Assert.Contains ("Replace", confirmation.Title);
                    Assert.Equal ("Keep this file", File.ReadAllText (path));
                    declined = true; ui.Press (KeyCode.Esc); return false;
                });
                PressButton (ui, viewer, "text");
                Assert.Equal ("Save cancelled.", viewer.StatusText);
                ui.Press (KeyCode.Esc); return false;
            });
            ui.Invoke ("ShowRunDiagnostics", Failed ());
            Assert.True (declined);
            Assert.Equal ("Keep this file", File.ReadAllText (path));
            Assert.Single (Directory.GetFiles (root));
        }
        finally { Directory.Delete (root, true); }
    }

    [Fact]
    public void CancellingTheActualSaveDialogLeavesDiagnosticsOpen ()
    {
        using ListNavigationTests.UiSession ui = new (AppMode.Installed, 1);
        bool cancelled = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            RunDiagnosticsDialog viewer = Assert.IsType<RunDiagnosticsDialog> (ui.Application.TopRunnable);
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                SaveDialog save = Assert.IsType<SaveDialog> (ui.Application.TopRunnable);
                Assert.Equal (Theme.SurfaceSchemeName, save.SchemeName);
                cancelled = true; ui.Press (KeyCode.Esc); return false;
            });
            PressButton (ui, viewer, "JSON");
            Assert.Equal ("Save cancelled.", viewer.StatusText);
            ui.Press (KeyCode.Esc); return false;
        });
        ui.Invoke ("ShowRunDiagnostics", Failed ());
        Assert.True (cancelled);
    }

    [Fact]
    public void HistoryDetailsUseTheSameCopyAndSaveViewerWithoutStartingARetry ()
    {
        RecordingBackend backend = new ();
        using ListNavigationTests.UiSession ui = new (AppMode.Installed, 1, backend, copyDiagnosticText: _ => true);
        RunItem item = Failed ();
        bool opened = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            RunRecordDialog record = Assert.IsType<RunRecordDialog> (ui.Application.TopRunnable);
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                RunDiagnosticsDialog viewer = Assert.IsType<RunDiagnosticsDialog> (ui.Application.TopRunnable);
                Assert.Contains (item.Id, viewer.Document.Text);
                PressButton (ui, viewer, "Copy");
                Assert.Equal ("Copied diagnostics.", viewer.StatusText);
                opened = true; ui.Press (KeyCode.Esc); return false;
            });
            Button details = Assert.Single (record.SubViews.OfType<Button> (), b => b.Text.Contains ("Details"));
            details.SetFocus (); ui.Press (KeyCode.Enter);
            ui.Press (KeyCode.Esc); return false;
        });
        ui.Invoke ("ShowRunRecord", new RunRecord (DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Install", [item]));
        Assert.True (opened);
        Assert.Equal (0, backend.Mutations);
    }

    [Theory]
    [InlineData ((int)ForegroundWorkflow.Operation, false)]
    [InlineData ((int)ForegroundWorkflow.Operation, true)]
    [InlineData ((int)ForegroundWorkflow.Preflight, false)]
    [InlineData ((int)ForegroundWorkflow.Preflight, true)]
    [InlineData ((int)ForegroundWorkflow.Export, false)]
    [InlineData ((int)ForegroundWorkflow.Export, true)]
    public void HistoryReportsRemainAccessibleDuringForegroundWork (int workflow, bool enter)
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-busy-history-" + Guid.NewGuid ().ToString ("N"));
        try
        {
            WorkflowStore store = new (root);
            RunItem item = Failed () with { Diagnostics = "Saved failure report" };
            store.SaveRun (new (DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Install", [item]));
            string path = Path.Combine (root, "report.json");
            string? copied = null;
            RecordingBackend backend = new ();
            using ListNavigationTests.UiSession ui = new (AppMode.Installed, 1, backend, store,
                copyDiagnosticText: text => { copied = text; return true; }, diagnosticSavePath: _ => path);
            ForegroundWorkflowCoordinator coordinator = ui.Get<ForegroundWorkflowCoordinator> ("_foreground");
            Assert.True (coordinator.TryBegin ((ForegroundWorkflow)workflow, out ForegroundAdmission active));
            bool selectedRun = false, saved = false;
            ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
            {
                if (!selectedRun)
                {
                    Assert.IsType<RunHistoryDialog> (ui.Application.TopRunnable);
                    selectedRun = true; ui.Press (KeyCode.Enter); return true;
                }
                RunRecordDialog record = Assert.IsType<RunRecordDialog> (ui.Application.TopRunnable);
                bool saving = false;
                ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
                {
                    RunDiagnosticsDialog viewer = Assert.IsType<RunDiagnosticsDialog> (ui.Application.TopRunnable);
                    Assert.True (coordinator.IsCurrent (active));
                    Assert.False (coordinator.TryReserveOperation (out _));
                    if (!saving)
                    {
                        Assert.Contains ("Saved failure report", viewer.Document.Text);
                        PressButton (ui, viewer, "Copy");
                        Assert.Equal (item.DetailText (), copied);
                        saving = true; PressButton (ui, viewer, "JSON");
                    }
                    else if (viewer.StatusText.StartsWith ("Saved to"))
                    {
                        Assert.Equal (DiagnosticDocument.ForRun (item).Json, File.ReadAllText (path));
                        saved = true; ui.Press (KeyCode.Esc); return false;
                    }
                    return true;
                });
                if (enter) Assert.Single (record.SubViews.OfType<ListView> ()).SetFocus ();
                else Assert.Single (record.SubViews.OfType<Button> (), b => b.Text.Contains ("Details")).SetFocus ();
                ui.Press (KeyCode.Enter);
                Assert.True (saved, "History Details must open even when foreground admission is occupied.");
                ui.Press (KeyCode.Esc); return false;
            });
            ui.Press (new Key ('L'));
            Assert.True (saved);
            Assert.True (coordinator.IsCurrent (active));
            Assert.True (coordinator.Release (active));
            Assert.True (coordinator.TryReserveOperation (out OperationReservation? next));
            next!.Dispose ();
            Assert.Equal (0, backend.Mutations);
            Assert.Single (store.Runs ());
            Assert.Same (ui.Window, ui.Application.TopRunnable);
        }
        finally { if (Directory.Exists (root)) Directory.Delete (root, true); }
    }

    [Theory]
    [InlineData ((int)ForegroundWorkflow.Operation)]
    [InlineData ((int)ForegroundWorkflow.Preflight)]
    [InlineData ((int)ForegroundWorkflow.Export)]
    public void BackendDiagnosticsRemainAccessibleWithoutReleasingForegroundWork (int workflow)
    {
        RecordingBackend backend = new ();
        using ListNavigationTests.UiSession ui = new (AppMode.Installed, 1, backend);
        ForegroundWorkflowCoordinator coordinator = ui.Get<ForegroundWorkflowCoordinator> ("_foreground");
        Assert.True (coordinator.TryBegin ((ForegroundWorkflow)workflow, out ForegroundAdmission active));
        bool checkedReport = false;
        ui.Application.AddTimeout (TimeSpan.FromMilliseconds (1), () =>
        {
            RunDiagnosticsDialog viewer = Assert.IsType<RunDiagnosticsDialog> (ui.Application.TopRunnable);
            if (!viewer.Document.Text.Contains ("winget: query succeeded")) return true;
            Assert.True (coordinator.IsCurrent (active));
            Assert.False (coordinator.TryReserveOperation (out _));
            checkedReport = true; ui.Press (KeyCode.Esc); return false;
        });
        ui.Press (new Key ('D'));
        Assert.True (checkedReport);
        Assert.True (coordinator.IsCurrent (active));
        Assert.True (coordinator.Release (active));
        Assert.Equal (0, backend.Mutations);
        Assert.Same (ui.Window, ui.Application.TopRunnable);
    }

    [Fact]
    public void LongSaveFeedbackStaysAboveTheActionButtonsAfterResize ()
    {
        using IApplication application = Terminal.Gui.App.Application.Create ();
        using RunDiagnosticsDialog dialog = new (DiagnosticDocument.ForRun (Failed ())) { Width = 100, Height = 24 };
        SessionToken session = application.Begin (dialog)!;
        try
        {
            dialog.SetFeedback ("Saved to C:\\" + new string ('x', 250) + "\\report.json");
            dialog.Width = 68; dialog.Height = 19;
            application.LayoutAndDraw ();
            Label feedback = Assert.Single (dialog.SubViews.OfType<Label> ());
            Assert.Equal (1, feedback.Frame.Height);
            Button copy = Assert.Single (dialog.SubViews.OfType<Button> (), b => b.Text.Contains ("Copy"));
            Assert.True (feedback.Frame.Y + feedback.Frame.Height < copy.Frame.Y);
        }
        finally { application.End (session); }
    }
}
