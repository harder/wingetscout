namespace WinGetScout;

public sealed record ListCounts (int Visible, int Total, int Selected, int HiddenSelected)
{
    public string SelectionLabel => $"{Selected} selected" + (HiddenSelected > 0 ? $" ({HiddenSelected} hidden)" : string.Empty);
    public string Label => $"{Visible} of {Total} shown" + (Selected > 0 ? $" · {SelectionLabel}" : string.Empty);

    public static ListCounts Capture (IReadOnlyList<Package> visible, int total, IEnumerable<string> selected)
    {
        HashSet<string> keys = new (selected, StringComparer.OrdinalIgnoreCase);
        int shownSelected = visible.Select (BatchPlanning.Key).Distinct (StringComparer.OrdinalIgnoreCase).Count (keys.Contains);
        return new (visible.Count, total, keys.Count, keys.Count - shownSelected);
    }
}

public sealed record PackageAction (string Id, string Label);

public static class PackageActions
{
    public static IReadOnlyList<PackageAction> For (AppMode mode, Package? package, bool pinsFresh,
        bool canVerify, bool canRepair, int selectedCount, PackageDetail? detail)
    {
        List<PackageAction> actions = [];
        if (package is not null)
        {
            bool exact = !package.IsTruncated && !string.IsNullOrWhiteSpace (package.Id);
            if (mode == AppMode.Search && exact)
            {
                actions.Add (new ("install", "Install latest…"));
                actions.Add (new ("version", "Install specific version…"));
                actions.Add (new ("advanced", "Advanced install…"));
            }
            if (mode != AppMode.Search)
            {
                if (pinsFresh && !package.PinState.IsPinned && !string.IsNullOrWhiteSpace (package.AvailableVersion))
                    actions.Add (new ("upgrade", "Upgrade…"));
                if (exact)
                {
                    actions.Add (new ("uninstall", "Uninstall…"));
                    if (canVerify) actions.Add (new ("verify", "Verify installation…"));
                    if (canRepair) actions.Add (new ("repair", "Repair installation…"));
                    if (pinsFresh) actions.Add (new ("pin", package.PinState.IsPinned ? "Unpin…" : "Pin…"));
                }
            }
            if (exact) actions.Add (new ("download", "Download installer…"));
            if (mode is AppMode.Search or AppMode.Upgrades)
                actions.Add (new ("select", "Toggle selection"));
            if (detail?.Id.Equals (package.Id, StringComparison.OrdinalIgnoreCase) == true)
            {
                if (!string.IsNullOrWhiteSpace (detail.Homepage)) actions.Add (new ("homepage", "Open homepage"));
                if (!string.IsNullOrWhiteSpace (detail.ReleaseNotesUrl)) actions.Add (new ("changelog", "Open release notes"));
            }
        }
        if (selectedCount > 0 && (mode is AppMode.Search or AppMode.Upgrades))
        {
            string action = mode == AppMode.Search ? "install" : "upgrade";
            actions.Add (new ("batch", $"Review {action} plan ({selectedCount} selected)…"));
        }
        actions.Add (new ("history", "Recent runs…"));
        return actions;
    }
}
