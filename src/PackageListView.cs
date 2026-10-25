namespace WinGetScout;

/// <summary>Retains navigation keys at list endpoints while allowing Tab to reach dialog actions.</summary>
internal sealed class PackageListView : ListView
{
    private (string Status, string Name, string Reason) []? _summaries;
    private int _summaryWidth = -1;
    private int _statusWidth;

    public PackageListView () => ViewportChanged += (_, _) => RefreshSummaries ();

    internal void SetSummaryRows (IEnumerable<(string Status, string Name, string Reason, string Id, string Source)> rows)
    {
        var items = rows.ToArray ();
        // Identity is useful when names collide, rather than on every review row.
        var collisions = items.GroupBy (row => row.Name, StringComparer.OrdinalIgnoreCase)
            .Where (group => group.Select (row => $"{row.Source}\u001f{row.Id}").Distinct (StringComparer.OrdinalIgnoreCase).Skip (1).Any ())
            .ToDictionary (group => group.Key,
                group => group.GroupBy (row => row.Source, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary (source => source.Key, source => source.Select (row => row.Id).Distinct (StringComparer.OrdinalIgnoreCase).Count (),
                        StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        string DisplayName ((string Status, string Name, string Reason, string Id, string Source) row)
        {
            if (!collisions.TryGetValue (row.Name, out var sources)) return row.Name;
            string identity = sources [row.Source] > 1
                ? sources.Count > 1 ? $"{row.Source} · {row.Id}" : row.Id : row.Source;
            return $"[{identity}] {row.Name}";
        }
        _summaries = items.Select (row => (row.Status, DisplayName (row), row.Reason)).ToArray ();
        _statusWidth = items.Select (row => row.Status.GetColumns ()).DefaultIfEmpty ().Max ();
        _summaryWidth = -1;
        RefreshSummaries ();
    }

    private void RefreshSummaries ()
    {
        int width = Math.Max (1, Viewport.Width);
        if (_summaries is null || width == _summaryWidth) return;
        _summaryWidth = width;
        int? selected = SelectedItem;
        SetSource (new ObservableCollection<string> (_summaries.Select (row => FormatSummary (row, width, _statusWidth))));
        SelectedItem = _summaries.Length == 0 ? null : Math.Clamp (selected ?? 0, 0, _summaries.Length - 1);
    }

    private static string FormatSummary ((string Status, string Name, string Reason) row, int width, int statusWidth)
    {
        int available = Math.Max (0, width - statusWidth - 4);
        int nameMinimum = Math.Min (12, row.Name.GetColumns ());
        int reasonWidth = Math.Min (row.Reason.GetColumns (), Math.Max (0, available - nameMinimum));
        string name = UiText.Ellipsize (row.Name, available - reasonWidth);
        string status = row.Status + new string (' ', statusWidth - row.Status.GetColumns ());
        return UiText.Ellipsize ($"{status} {name} — {row.Reason}", width);
    }

    protected override bool OnKeyDownNotHandled (Key key)
    {
        if (key.KeyCode is KeyCode.CursorUp or KeyCode.CursorDown
            or KeyCode.PageUp or KeyCode.PageDown or KeyCode.Home or KeyCode.End)
        {
            key.Handled = true;
            return true;
        }

        return base.OnKeyDownNotHandled (key);
    }
}
