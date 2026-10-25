namespace WinGetScout;

/// <summary>Retains navigation keys at list endpoints while allowing Tab to reach dialog actions.</summary>
internal sealed class PackageListView : ListView
{
    private (string Status, string Name, string Reason) []? _summaries;
    private int _summaryWidth = -1;

    public PackageListView () => ViewportChanged += (_, _) => RefreshSummaries ();

    internal void SetSummaryRows (IEnumerable<(string Status, string Name, string Reason)> rows)
    {
        _summaries = [.. rows];
        _summaryWidth = -1;
        RefreshSummaries ();
    }

    private void RefreshSummaries ()
    {
        int width = Math.Max (1, Viewport.Width);
        if (_summaries is null || width == _summaryWidth) return;
        _summaryWidth = width;
        int? selected = SelectedItem;
        SetSource (new ObservableCollection<string> (_summaries.Select (row => FormatSummary (row, width))));
        SelectedItem = _summaries.Length == 0 ? null : Math.Clamp (selected ?? 0, 0, _summaries.Length - 1);
    }

    private static string FormatSummary ((string Status, string Name, string Reason) row, int width)
    {
        int available = Math.Max (0, width - row.Status.GetColumns () - 4);
        int nameMinimum = Math.Min (12, row.Name.GetColumns ());
        int reasonWidth = Math.Min (row.Reason.GetColumns (), Math.Max (0, available - nameMinimum));
        string name = Clip (row.Name, available - reasonWidth);
        return Clip ($"{row.Status} {name} — {row.Reason}", width);
    }

    private static string Clip (string value, int width)
    {
        if (value.GetColumns () <= width) return value;
        if (width <= 0) return string.Empty;
        StringBuilder result = new ();
        int used = 0;
        System.Globalization.TextElementEnumerator elements = System.Globalization.StringInfo.GetTextElementEnumerator (value);
        while (elements.MoveNext ())
        {
            string element = elements.GetTextElement ();
            if (used + element.GetColumns () >= width) break;
            result.Append (element);
            used += element.GetColumns ();
        }
        return result + "…";
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
