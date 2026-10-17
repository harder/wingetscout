namespace WinGetScout;

public sealed class BatchPlanDialog : Runnable<bool>
{
    public BatchPlanDialog (BatchPlan plan)
    {
        Title = $" Review {plan.Action.ToLowerInvariant ()} plan ";
        BorderStyle = LineStyle.Rounded;
        Width = Dim.Percent (78);
        Height = Dim.Percent (65);
        X = Pos.Center ();
        Y = Pos.Center ();
        SchemeName = Theme.SurfaceSchemeName;

        KeyDown += (_, key) =>
        {
            if (key.KeyCode != KeyCode.Esc) return;
            Result = false; RequestStop (); key.Handled = true;
        };

        Label summary = new () { X = 1, Y = 0, Width = Dim.Fill (1), Text = $"{plan.Ready} ready · {plan.Skipped} skipped  —  review each package before continuing" };
        ListView rows = new PackageListView () { X = 1, Y = 2, Width = Dim.Fill (1), Height = Dim.Fill (3), SchemeName = Theme.SurfaceSchemeName };
        rows.SetSource (new ObservableCollection<string> (plan.Items.Select (x =>
            $"{(x.CanRun ? "READY" : "SKIP ")}  {x.Package.Name} [{x.Package.Id}] ({x.Package.Source})  —  {x.Reason}").ToList ()));
        rows.SelectedItem = plan.Items.Count > 0 ? 0 : null;
        Button details = new () { X = 1, Y = Pos.AnchorEnd (1), Text = "_Details", Enabled = plan.Items.Count > 0 };
        details.Accepting += (_, e) =>
        {
            if (App is not null && rows.SelectedItem is { } i && i >= 0 && i < plan.Items.Count)
            {
                BatchPlanItem item = plan.Items [i];
                using RunDiagnosticsDialog detail = new ("Plan details",
                    $"{item.Package.Name} [{item.Package.Id}]\nSource: {item.Package.Source}\n"
                    + $"Current/catalog version: {item.Package.Version}\nAvailable version: {item.Package.AvailableVersion}\n\n"
                    + $"{item.Reason}\n{item.Request?.Description}");
                App.Run (detail);
            }
            e.Handled = true;
        };
        Button run = new () { X = Pos.Center () - 8, Y = Pos.AnchorEnd (1), Text = $"_{plan.Action}", IsDefault = true, Enabled = plan.Ready > 0 };
        Button cancel = new () { X = Pos.Center () + 3, Y = Pos.AnchorEnd (1), Text = "Cancel" };
        run.Accepting += (_, e) => { Result = true; RequestStop (); e.Handled = true; };
        cancel.Accepting += (_, e) => { Result = false; RequestStop (); e.Handled = true; };
        Add (summary, rows, details, run, cancel);
        rows.SetFocus ();
    }
}

public sealed class RunRecordDialog : Runnable<IReadOnlyList<RunItem>?>
{
    public RunRecordDialog (RunRecord record, Action<RunItem>? showDiagnostics = null)
    {
        Title = $" {StatusOwnership.TruncateScalarSafe (record.Action, 24)} results ";
        BorderStyle = LineStyle.Rounded;
        Width = Dim.Percent (78);
        Height = Dim.Percent (65);
        X = Pos.Center ();
        Y = Pos.Center ();
        SchemeName = Theme.SurfaceSchemeName;
        KeyDown += (_, key) =>
        {
            if (key.KeyCode != KeyCode.Esc) return;
            Result = null; RequestStop (); key.Handled = true;
        };
        Label summary = new () { X = 1, Y = 0, Width = Dim.Fill (1), Text = $"{record.Summary} · {record.FinishedAtUtc.ToLocalTime ():g}" };
        ListView rows = new PackageListView () { X = 1, Y = 2, Width = Dim.Fill (1), Height = Dim.Fill (5), SchemeName = Theme.SurfaceSchemeName };
        rows.SetSource (new ObservableCollection<string> (record.Items.Select (x =>
            $"{x.Status,-9} {x.Name} [{x.Id}] ({x.Source})  —  {x.Reason}").ToList ()));
        rows.SelectedItem = record.Items.Count > 0 ? 0 : null;
        Button diagnostics = new () { X = 1, Y = Pos.AnchorEnd (3), Text = "_Details", Enabled = record.Items.Count > 0 };
        Button retry = new () { X = 14, Y = Pos.AnchorEnd (3), Text = "_Retry selected…" };
        Button retryAll = new () { X = 1, Y = Pos.AnchorEnd (1), Text = "Retry _failed…", Enabled = record.Items.Any (x => x.Status == "Failed" && x.CanRetry) };
        Button close = new () { X = Pos.AnchorEnd (10), Y = Pos.AnchorEnd (1), Text = "_Close", IsDefault = true };
        RunItem? Selected () => rows.SelectedItem is { } i && i >= 0 && i < record.Items.Count ? record.Items [i] : null;
        void UpdateRetry () => retry.Enabled = Selected ()?.CanRetry == true;
        UpdateRetry ();
        rows.ValueChanged += (_, _) => UpdateRetry ();
        void Details ()
        {
            if (Selected () is not { } item || App is null) return;
            if (showDiagnostics is not null) { showDiagnostics (item); return; }
            using RunDiagnosticsDialog detail = new (item);
            App.Run (detail);
        }
        diagnostics.Accepting += (_, e) => { Details (); e.Handled = true; };
        rows.Accepting += (_, e) => { Details (); e.Handled = true; };
        retry.Accepting += (_, e) =>
        {
            if (Selected () is { CanRetry: true } item) { Result = new [] { item }; RequestStop (); }
            e.Handled = true;
        };
        retryAll.Accepting += (_, e) =>
        {
            Result = record.Items.Where (x => x.Status == "Failed" && x.CanRetry).ToArray ();
            RequestStop (); e.Handled = true;
        };
        close.Accepting += (_, e) => { RequestStop (); e.Handled = true; };
        Add (summary, rows, diagnostics, retry, retryAll, close);
        rows.SetFocus ();
    }
}

public sealed class RunDiagnosticsDialog : Runnable
{
    private readonly Code _text;
    private readonly Label _feedback;
    private readonly Button _copy, _saveText, _saveJson, _refresh;
    private bool _configured;
    public DiagnosticDocument Document { get; private set; }
    public string StatusText => _feedback.Text;

    public RunDiagnosticsDialog (RunItem item) : this ("Run diagnostics", item.DetailText ()) { }

    public RunDiagnosticsDialog (DiagnosticDocument document) : this (document.Title, document.Text) => Document = document;

    public RunDiagnosticsDialog (string title, string content)
    {
        Document = new (title, content, string.Empty);
        Title = $" {title} ";
        BorderStyle = LineStyle.Rounded;
        Width = Dim.Percent (85); Height = Dim.Percent (80);
        X = Pos.Center (); Y = Pos.Center (); SchemeName = Theme.SurfaceSchemeName;
        KeyDown += (_, key) =>
        {
            if (key.KeyCode != KeyCode.Esc) return;
            RequestStop (); key.Handled = true;
        };
        Code text = new () { X = 1, Y = 0, Width = Dim.Fill (1), Height = Dim.Fill (5), Text = content };
        _text = text;
        text.KeyDown += (_, key) =>
        {
            int page = Math.Max (1, text.Viewport.Height - 1);
            switch (key.KeyCode)
            {
                case KeyCode.CursorDown: text.ScrollVertical (1); break;
                case KeyCode.CursorUp: text.ScrollVertical (-1); break;
                case KeyCode.PageDown: text.ScrollVertical (page); break;
                case KeyCode.PageUp: text.ScrollVertical (-page); break;
                case KeyCode.Home: text.ScrollVertical (-text.Viewport.Y); break;
                case KeyCode.End: text.ScrollVertical (text.GetContentSize ().Height); break;
                case KeyCode.CursorLeft: text.ScrollHorizontal (-1); break;
                case KeyCode.CursorRight: text.ScrollHorizontal (1); break;
                default: return;
            }
            key.Handled = true;
        };
        _copy = new () { X = 1, Y = Pos.AnchorEnd (1), Text = "_Copy", Enabled = false };
        _saveText = new () { X = Pos.Right (_copy) + 1, Y = Pos.AnchorEnd (1), Text = "Save _text…", Enabled = false };
        _saveJson = new () { X = Pos.Right (_saveText) + 1, Y = Pos.AnchorEnd (1), Text = "Save _JSON…", Enabled = false };
        _refresh = new () { X = 1, Y = Pos.AnchorEnd (3), Text = "_Refresh", Visible = false };
        _feedback = new () { X = 14, Y = Pos.AnchorEnd (3), Width = Dim.Fill (12), Height = 1 };
        _feedback.TextFormatter.WordWrap = false;
        Button close = new () { X = Pos.AnchorEnd (10), Y = Pos.AnchorEnd (3), Text = "C_lose", IsDefault = true };
        close.Accepting += (_, e) => { RequestStop (); e.Handled = true; };
        Add (text, _copy, _saveText, _saveJson, _refresh, _feedback, close); text.SetFocus ();
    }

    public void ConfigureActions (Action copy, Action<bool> save, Action? refresh)
    {
        _configured = true;
        _copy.Accepting += (_, e) => { copy (); e.Handled = true; };
        _saveText.Accepting += (_, e) => { save (false); e.Handled = true; };
        _saveJson.Accepting += (_, e) => { save (true); e.Handled = true; };
        _refresh.Visible = refresh is not null;
        _refresh.Accepting += (_, e) => { refresh?.Invoke (); e.Handled = true; };
        SetBusy (false);
    }

    public void SetBusy (bool busy)
    {
        _copy.Enabled = _saveText.Enabled = _saveJson.Enabled = _refresh.Enabled = _configured && !busy;
    }

    public void SetFeedback (string message) => _feedback.Text = message;

    public void SetDocument (DiagnosticDocument document)
    {
        Document = document;
        _text.Text = document.Text;
    }
}

public sealed class PackageActionDialog : Runnable<string?>
{
    public PackageActionDialog (string context, IReadOnlyList<PackageAction> actions)
    {
        Title = " Actions "; BorderStyle = LineStyle.Rounded;
        Width = Dim.Percent (65); Height = Math.Clamp (actions.Count + 7, 10, 23);
        X = Pos.Center (); Y = Pos.Center (); SchemeName = Theme.SurfaceSchemeName;
        KeyDown += (_, key) =>
        {
            if (key.KeyCode != KeyCode.Esc) return;
            Result = null; RequestStop (); key.Handled = true;
        };
        Label label = new () { X = 1, Y = 0, Width = Dim.Fill (1), Text = context };
        ListView rows = new PackageListView () { X = 1, Y = 2, Width = Dim.Fill (1), Height = Dim.Fill (3), SchemeName = Theme.SurfaceSchemeName };
        rows.SetSource (new ObservableCollection<string> (actions.Select (x => x.Label).ToList ()));
        rows.SelectedItem = actions.Count > 0 ? 0 : null;
        void Choose ()
        {
            if (rows.SelectedItem is not { } i || i < 0 || i >= actions.Count) return;
            Result = actions [i].Id; RequestStop ();
        }
        rows.Accepting += (_, e) => { Choose (); e.Handled = true; };
        Button choose = new () { X = Pos.Center () - 10, Y = Pos.AnchorEnd (1), Text = "_Choose", IsDefault = true };
        Button cancel = new () { X = Pos.Center () + 3, Y = Pos.AnchorEnd (1), Text = "Cancel" };
        choose.Accepting += (_, e) => { Choose (); e.Handled = true; };
        cancel.Accepting += (_, e) => { RequestStop (); e.Handled = true; };
        Add (label, rows, choose, cancel); rows.SetFocus ();
    }
}

public sealed class TextPromptDialog : Runnable<string?>
{
    public TextPromptDialog (string title, string prompt)
    {
        Title = $" {title} ";
        BorderStyle = LineStyle.Rounded;
        Width = 64;
        Height = 8;
        X = Pos.Center ();
        Y = Pos.Center ();
        SchemeName = Theme.SurfaceSchemeName;
        Label label = new () { X = 1, Y = 0, Text = prompt };
        TextField input = new () { X = 1, Y = 2, Width = Dim.Fill (1) };
        Button save = new () { X = Pos.Center () - 8, Y = Pos.AnchorEnd (1), Text = "_Save", IsDefault = true };
        Button cancel = new () { X = Pos.Center () + 2, Y = Pos.AnchorEnd (1), Text = "Cancel" };
        save.Accepting += (_, e) => { Result = input.Text?.Trim (); RequestStop (); e.Handled = true; };
        cancel.Accepting += (_, e) => { Result = null; RequestStop (); e.Handled = true; };
        Add (label, input, save, cancel);
        input.SetFocus ();
    }
}

public sealed record SetManagerChoice (string Action, string? Name);

public sealed class SetManagerDialog : Runnable<SetManagerChoice?>
{
    public SetManagerDialog (IReadOnlyList<PackageSet> sets, int selectedCount)
    {
        Title = " Saved package sets ";
        BorderStyle = LineStyle.Rounded;
        Width = Dim.Percent (65);
        Height = Math.Clamp (sets.Count + 8, 11, 21);
        X = Pos.Center ();
        Y = Pos.Center ();
        SchemeName = Theme.SurfaceSchemeName;
        Label hint = new () { X = 1, Y = 0, Width = Dim.Fill (1), Text = $"{selectedCount} currently selected · choose a set or save this selection" };
        ListView rows = new PackageListView () { X = 1, Y = 2, Width = Dim.Fill (1), Height = Dim.Fill (3), SchemeName = Theme.SurfaceSchemeName };
        rows.SetSource (new ObservableCollection<string> (sets.Select (x => $"{x.Name} ({x.Packages.Count})").ToList ()));
        Button save = new () { X = 1, Y = Pos.AnchorEnd (1), Text = "_Save current", Enabled = selectedCount > 0 };
        Button load = new () { X = 19, Y = Pos.AnchorEnd (1), Text = "_Load", Enabled = sets.Count > 0 };
        Button delete = new () { X = 30, Y = Pos.AnchorEnd (1), Text = "_Delete", Enabled = sets.Count > 0 };
        Button close = new () { X = 43, Y = Pos.AnchorEnd (1), Text = "Close" };
        void Select (string action)
        {
            int index = rows.SelectedItem ?? -1;
            Result = new (action, index >= 0 && index < sets.Count ? sets [index].Name : null);
            RequestStop ();
        }
        save.Accepting += (_, e) => { Select ("Save"); e.Handled = true; };
        load.Accepting += (_, e) => { Select ("Load"); e.Handled = true; };
        delete.Accepting += (_, e) => { Select ("Delete"); e.Handled = true; };
        close.Accepting += (_, e) => { RequestStop (); e.Handled = true; };
        Add (hint, rows, save, load, delete, close);
        (sets.Count > 0 ? (View)rows : save).SetFocus ();
    }
}

public sealed class RunHistoryDialog : Runnable<int?>
{
    public RunHistoryDialog (IReadOnlyList<RunRecord> runs)
    {
        Title = " Recent runs ";
        BorderStyle = LineStyle.Rounded;
        Width = Dim.Percent (70);
        Height = Math.Clamp (runs.Count + 6, 9, 22);
        X = Pos.Center ();
        Y = Pos.Center ();
        SchemeName = Theme.SurfaceSchemeName;
        KeyDown += (_, key) =>
        {
            if (key.KeyCode != KeyCode.Esc) return;
            Result = null; RequestStop (); key.Handled = true;
        };
        ListView rows = new PackageListView () { X = 1, Y = 1, Width = Dim.Fill (1), Height = Dim.Fill (3), SchemeName = Theme.SurfaceSchemeName };
        rows.SetSource (new ObservableCollection<string> (runs.Select (x =>
            $"{x.FinishedAtUtc.ToLocalTime ():g}  {StatusOwnership.TruncateScalarSafe (x.Action, 10),-10} {x.Summary}").ToList ()));
        rows.SelectedItem = runs.Count > 0 ? 0 : null;
        Button open = new () { X = Pos.Center () - 7, Y = Pos.AnchorEnd (1), Text = "_Open", IsDefault = true };
        Button close = new () { X = Pos.Center () + 2, Y = Pos.AnchorEnd (1), Text = "Close" };
        open.Accepting += (_, e) => { Result = rows.SelectedItem; RequestStop (); e.Handled = true; };
        close.Accepting += (_, e) => { Result = null; RequestStop (); e.Handled = true; };
        Add (rows, open, close);
        rows.SetFocus ();
    }
}

public sealed record ScheduleChoice (string Action, string DailyAt, bool NotifyOnChange);

public sealed class ScheduleDialog : Runnable<ScheduleChoice?>
{
    public ScheduleDialog (UpdateCheckSettings settings, UpdateCheckSnapshot? latest, bool canSchedule = true)
    {
        Title = " Scheduled update checks ";
        BorderStyle = LineStyle.Rounded;
        Width = 72;
        Height = 13;
        X = Pos.Center ();
        Y = Pos.Center ();
        SchemeName = Theme.SurfaceSchemeName;
        Label state = new () { X = 1, Y = 0, Width = Dim.Fill (1), Text = !canSchedule
            ? "Mock session: daily scheduling is unavailable. Use Check now."
            : settings.Enabled ? "Daily checks enabled" : "Daily checks disabled" };
        Label last = new () { X = 1, Y = 1, Width = Dim.Fill (1), Text = latest is null
            ? "No check has run yet."
            : latest.Status == "Succeeded"
                ? $"Last check: {latest.CheckedAtUtc.ToLocalTime ():g} · {latest.Actionable} available · {latest.NewOrChanged} new or changed"
                : $"Last check failed: {latest.Error}" };
        Label timeLabel = new () { X = 1, Y = 3, Text = "Daily time (HH:mm):" };
        TextField time = new () { X = 21, Y = 3, Width = 8, Text = settings.DailyAt, Enabled = canSchedule };
        Label notifyLabel = new () { X = 1, Y = 5, Text = "Notify on changes or failures:" };
        OptionSelector notify = new () { X = 32, Y = 5, Width = 20, Labels = ["Yes", "No"], Enabled = canSchedule };
        notify.Value = settings.NotifyOnChange ? 0 : 1;
        Label error = new () { X = 1, Y = 7, Width = Dim.Fill (1), Text = string.Empty, SchemeName = Theme.DangerSchemeName };
        Button save = new () { X = 1, Y = Pos.AnchorEnd (1), Text = "_Enable / save", IsDefault = canSchedule, Enabled = canSchedule };
        Button disable = new () { X = 21, Y = Pos.AnchorEnd (1), Text = "_Disable", Enabled = canSchedule };
        Button check = new () { X = 35, Y = Pos.AnchorEnd (1), Text = "Check _now" };
        Button close = new () { X = 54, Y = Pos.AnchorEnd (1), Text = "Close" };
        save.Accepting += (_, e) =>
        {
            e.Handled = true;
            if (!canSchedule) return;
            if (!UpdateChecks.TryParseDailyTime (time.Text ?? string.Empty, out TimeOnly ignoredTime))
            {
                error.Text = "Enter a 24-hour time, for example 09:00.";
            }
            else
            {
                Result = new ("Save", time.Text!, notify.Value == 0);
                RequestStop ();
            }
        };
        disable.Accepting += (_, e) =>
        {
            e.Handled = true;
            if (!canSchedule) return;
            Result = new ("Disable", settings.DailyAt, settings.NotifyOnChange); RequestStop ();
        };
        check.Accepting += (_, e) => { Result = new ("Check", settings.DailyAt, settings.NotifyOnChange); RequestStop (); e.Handled = true; };
        close.Accepting += (_, e) => { RequestStop (); e.Handled = true; };
        Add (state, last, timeLabel, time, notifyLabel, notify, error, save, disable, check, close);
        (canSchedule ? (View)time : check).SetFocus ();
    }
}
