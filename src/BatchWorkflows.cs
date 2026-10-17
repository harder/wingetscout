namespace WinGetScout;

public sealed record BatchPlanItem (Package Package, bool CanRun, string Reason)
{
    public RunRequest? Request { get; init; }
}

public sealed record BatchPlan (string Action, List<BatchPlanItem> Items)
{
    public int Ready => Items.Count (x => x.CanRun);
    public int Skipped => Items.Count - Ready;
}

public static class BatchPlanning
{
    public static string Key (Package package) => $"{package.Source}\u001f{package.Id}";

    public static List<Package> SelectedPackages (IEnumerable<Package> packages, IReadOnlySet<string> keys) =>
        [.. packages.Where (p => keys.Contains (Key (p)))];

    internal static Package? ExactMatch (IEnumerable<Package> packages, string id, string source, out bool ambiguous)
    {
        Package [] matches = packages.Where (p => p.Id.Equals (id, StringComparison.OrdinalIgnoreCase)
            && p.Source.Equals (source, StringComparison.OrdinalIgnoreCase)).Take (2).ToArray ();
        ambiguous = matches.Length > 1;
        return matches.Length == 1 ? matches [0] : null;
    }

    public static async Task<BatchPlan> ReviewInstallsAsync (IBackend backend, IReadOnlyList<Package> selected, CancellationToken ct)
    {
        IReadOnlyList<Package> installed = await backend.ListInstalledAsync (null, ct);
        IReadOnlyList<string> sources = await backend.ListSourcesAsync (ct);
        BatchPlanItem [] items = new BatchPlanItem [selected.Count];
        await Parallel.ForEachAsync (Enumerable.Range (0, selected.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (index, token) =>
            {
                Package original = selected [index];
                if (string.IsNullOrWhiteSpace (original.Id) || original.IsTruncated
                    || !sources.Contains (original.Source, StringComparer.OrdinalIgnoreCase))
                {
                    items [index] = ForInstalls ([original], installed, sources).Items [0];
                    return;
                }
                IReadOnlyList<Package> matches = await backend.SearchAsync (original.Id, original.Source, token);
                Package? current = ExactMatch (matches, original.Id, original.Source, out bool ambiguous);
                items [index] = current is null
                    ? new (original, false, ambiguous ? "Package ID and source are ambiguous: multiple exact matches were returned."
                        : "Exact package and source are no longer available.")
                    : ForInstalls ([current], installed, sources).Items [0];
            });
        return new ("Install", [.. items]);
    }

    public static async Task<BatchPlan> ReviewUpgradesAsync (IBackend backend, IReadOnlySet<string> selected, CancellationToken ct)
    {
        IReadOnlyList<Package> current = await backend.ListUpgradesAsync (null, ct);
        IReadOnlyDictionary<string, PinState> pins = await backend.ListPinsAsync (ct);
        List<BatchPlanItem> items = [];
        foreach (string key in selected)
        {
            int separator = key.IndexOf ('\u001f');
            string id = separator < 0 ? key : key [(separator + 1)..];
            string source = separator < 0 ? string.Empty : key [..separator];
            Package? package = ExactMatch (current, id, source, out bool ambiguous);
            if (package is null)
            {
                items.Add (new (new () { Id = id, Name = id, Source = source }, false,
                    ambiguous ? "Package ID and source are ambiguous: multiple exact matches were returned."
                        : "Package is no longer present in the current upgrade inventory."));
                continue;
            }
            package.PinState = pins.FirstOrDefault (x => x.Key.Equals (package.Id, StringComparison.OrdinalIgnoreCase)).Value;
            items.AddRange (ForUpgrades ([package], pinsFresh: true).Items);
        }
        return new ("Upgrade", items);
    }

    public static BatchPlan ForUpgrades (IEnumerable<Package> selected, bool pinsFresh)
    {
        List<BatchPlanItem> items = [];
        foreach (Package package in selected)
        {
            string? skip = string.IsNullOrWhiteSpace (package.Id) || package.IsTruncated
                ? "Package ID is unavailable."
                : !pinsFresh
                    ? "Pin status could not be verified. Refresh before upgrading."
                    : package.PinState.IsPinned
                        ? $"Pinned: {package.PinState.DisplayLabel ()}. Unpin to upgrade."
                        : string.IsNullOrWhiteSpace (package.AvailableVersion)
                            ? "No available version was reported."
                            : null;
            items.Add (new (package, skip is null, skip ?? $"{package.Version} → {package.AvailableVersion}"));
        }
        return new ("Upgrade", items);
    }

    public static BatchPlan ForInstalls (
        IEnumerable<Package> selected,
        IReadOnlyList<Package> installed,
        IReadOnlyList<string> sources)
    {
        HashSet<string> installedKeys = new (installed.Select (Key), StringComparer.OrdinalIgnoreCase);
        HashSet<string> unknownSourceIds = new (
            installed.Where (x => string.IsNullOrWhiteSpace (x.Source)).Select (x => x.Id),
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> knownSources = new (sources, StringComparer.OrdinalIgnoreCase);
        List<BatchPlanItem> items = [];
        foreach (Package package in selected)
        {
            string? skip = string.IsNullOrWhiteSpace (package.Id) || package.IsTruncated
                ? "Package ID is unavailable."
                : string.IsNullOrWhiteSpace (package.Source) || !knownSources.Contains (package.Source)
                    ? "Source is unavailable. Refresh sources and try again."
                    : installedKeys.Contains (Key (package)) || unknownSourceIds.Contains (package.Id)
                        ? "Already installed. Use Upgrades for a newer version."
                        : string.IsNullOrWhiteSpace (package.Version)
                            ? "Package has not been resolved in the catalog."
                            : null;
            items.Add (new (package, skip is null, skip ?? $"Install {package.Version} from {package.Source}"));
        }
        return new ("Install", items);
    }
}
