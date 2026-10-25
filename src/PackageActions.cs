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
        bool hasSelection = selectedCount > 0 && (mode is AppMode.Search or AppMode.Upgrades);
        if (hasSelection)
        {
            string action = mode == AppMode.Search ? "install" : "upgrade";
            actions.Add (new ("batch", $"Review {action} plan ({selectedCount} selected)…"));
        }
        if (package is not null)
        {
            bool exact = !package.IsTruncated && !string.IsNullOrWhiteSpace (package.Id);
            if (mode == AppMode.Search && exact)
            {
                actions.Add (new ("install", hasSelection ? "Install highlighted package…" : "Install latest…"));
                actions.Add (new ("version", "Install specific version…"));
                actions.Add (new ("advanced", "Advanced install…"));
            }
            if (mode != AppMode.Search)
            {
                if (pinsFresh && !package.PinState.IsPinned && !string.IsNullOrWhiteSpace (package.AvailableVersion))
                    actions.Add (new ("upgrade", hasSelection ? "Upgrade highlighted package…" : "Upgrade…"));
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
            if (detail?.Id.Equals (package.Id, StringComparison.OrdinalIgnoreCase) == true
                && detail.Source.Equals (package.Source, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace (detail.Homepage)) actions.Add (new ("homepage", "Open homepage"));
                if (!string.IsNullOrWhiteSpace (detail.ReleaseNotesUrl)) actions.Add (new ("changelog", "Open release notes"));
                if (SupplementalLinks (detail).Count > 0) actions.Add (new ("links", "More links…"));
            }
        }
        if (hasSelection) actions.Add (new ("clear-selection", $"Clear selection ({selectedCount} {(selectedCount == 1 ? "package" : "packages")})"));
        actions.Add (new ("history", "Recent runs…"));
        actions.Add (new ("diagnostics", "Backend and source diagnostics…"));
        return actions;
    }

    internal static IReadOnlyList<DocLink> SupplementalLinks (PackageDetail detail)
    {
        List<DocLink> links = [];
        HashSet<string> seen = new (StringComparer.Ordinal);
        void Add (string label, string? url)
        {
            if (Uri.TryCreate (url, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https" && seen.Add (url!))
                links.Add (new (label, url!));
        }
        Add ("Support", detail.SupportUrl);
        Add ("Privacy", detail.PrivacyUrl);
        Add ("Purchase", detail.PurchaseUrl);
        foreach (DocLink link in detail.Documentation ?? []) Add (link.Label, link.Url);
        return links;
    }
}
