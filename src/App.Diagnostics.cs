namespace WinGetScout;

public sealed partial class App
{
    private readonly Func<string, bool> _copyDiagnosticText;
    private readonly Func<bool, string?>? _diagnosticSavePath;

    private void ShowBackendDiagnostics ()
    {
        if (App is null) return;
        ShowDiagnosticDocument (
            new ("Backend and source diagnostics", "Collecting read-only diagnostics…", "{}"), backendDiagnostics: true);
    }

    private void ShowRunDiagnostics (RunItem item)
    {
        if (App is null) return;
        ShowDiagnosticDocument (DiagnosticDocument.ForRun (item));
    }

    private void ShowDiagnosticDocument (DiagnosticDocument document, bool backendDiagnostics = false)
    {
        if (App is null) return;
        using CancellationTokenSource request = CreateLifetimeLinkedSource ();
        CancellationToken ct = request.Token;
        using RunDiagnosticsDialog dialog = new (document);
        bool closed = false, busy = false;

        void Work (Func<CancellationToken, Task<(DiagnosticDocument? Document, string Message)>> action)
        {
            if (busy || closed) return;
            busy = true;
            dialog.SetBusy (true);
            bool admitted = _background.TryRun (async lifetimeToken =>
            {
                try
                {
                    (DiagnosticDocument? next, string message) = await action (ct);
                    await DispatchAsync (() =>
                    {
                        if (next is not null) dialog.SetDocument (next);
                        dialog.SetFeedback (message);
                        busy = false; dialog.SetBusy (false);
                    }, ct, () => !closed);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    await DispatchAsync (() =>
                    {
                        dialog.SetFeedback ($"Failed: {RunRecord.CompactReason (ex.Message)}");
                        busy = false; dialog.SetBusy (false);
                    }, lifetimeToken, () => !closed);
                }
            });
            if (!admitted)
            {
                busy = false; dialog.SetBusy (false);
                dialog.SetFeedback ("Too many background requests. Try again.");
            }
        }

        void Refresh ()
        {
            if (!backendDiagnostics) return;
            // Snapshot UI context before leaving the UI thread.
            string? source = _state.SourceFilter, error = _state.LastInventoryError ?? _state.ViewError;
            string? fallback = StartupDiagnostics.ComFallbackReason;
            Work (async token =>
            {
                BackendDiagnosticReport report = await BackendDiagnostics.CaptureAsync (_state.Backend, fallback, source, error, token);
                return (DiagnosticDocument.ForBackend (report), "Read-only checks complete.");
            });
        }

        dialog.ConfigureActions (() =>
        {
            if (busy) return;
            try { dialog.SetFeedback (_copyDiagnosticText (dialog.Document.Text) ? "Copied diagnostics." : "Clipboard unavailable. Use Save text."); }
            catch (Exception ex) { dialog.SetFeedback ($"Copy failed: {RunRecord.CompactReason (ex.Message)}"); }
        }, json =>
        {
            if (busy) return;
            try
            {
                string? path = _diagnosticSavePath is null ? SelectDiagnosticSavePath (json) : _diagnosticSavePath (json);
                if (string.IsNullOrWhiteSpace (path)) { dialog.SetFeedback ("Save cancelled."); return; }
                bool overwrite = File.Exists (path);
                if (overwrite && !Confirm ("Replace diagnostics file", $"Replace {path}?"))
                { dialog.SetFeedback ("Save cancelled."); return; }
                string snapshot = json ? dialog.Document.Json : dialog.Document.Text;
                Work (async token =>
                {
                    await DiagnosticExport.WriteAsync (path, snapshot, overwrite, token);
                    return (null, $"Saved to {path}");
                });
            }
            catch (Exception ex) { dialog.SetFeedback ($"Save failed: {RunRecord.CompactReason (ex.Message)}"); }
        }, backendDiagnostics ? Refresh : null);
        if (backendDiagnostics) Refresh ();
        try { App.Run (dialog); }
        finally { closed = true; CancelSource (request); }
    }

    private string? SelectDiagnosticSavePath (bool json)
    {
        if (App is null) return null;
        using SaveDialog save = new ()
        {
            Title = " Save diagnostics ", OpenMode = OpenMode.File, MustExist = false,
            SchemeName = Theme.SurfaceSchemeName, BorderStyle = LineStyle.Rounded,
            Path = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments),
                $"wingetscout-diagnostics.{(json ? "json" : "txt")}"),
            FileOperationsHandler = null!
        };
        App.Run (save);
        return save.Canceled ? null : save.Path;
    }
}
