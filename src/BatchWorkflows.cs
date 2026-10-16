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

    public static async Task<BatchPlan> ReviewUpgradesAsync (IBackend backend, IReadOnlySet<string> selected, CancellationToken ct)
    {
        IReadOnlyList<Package> current = await backend.ListUpgradesAsync (null, ct);
        IReadOnlyDictionary<string, PinState> pins = await backend.ListPinsAsync (ct);
        List<BatchPlanItem> items = [];
        foreach (string key in selected)
        {
            Package? package = current.FirstOrDefault (p => Key (p).Equals (key, StringComparison.OrdinalIgnoreCase));
            if (package is null)
            {
                int separator = key.IndexOf ('\u001f');
                string id = separator < 0 ? key : key [(separator + 1)..];
                items.Add (new (new () { Id = id, Name = id, Source = separator < 0 ? string.Empty : key [..separator] },
                    false, "Package is no longer present in the current upgrade inventory."));
                continue;
            }
            package.PinState = pins.FirstOrDefault (x => x.Key.Equals (package.Id, StringComparison.OrdinalIgnoreCase)).Value;
            items.AddRange (ForUpgrades ([package], pinsFresh: true).Items);
        }
        return new ("Upgrade", items);
    }

    /// <summary>Resolve saved entries concurrently while preserving their order in the review plan.</summary>
    public static async Task<List<Package>> ResolveUnresolvedAsync (
        IReadOnlyList<Package> selected,
        Func<Package, CancellationToken, Task<Package?>> resolve,
        CancellationToken ct)
    {
        Package [] resolved = [.. selected];
        await Parallel.ForEachAsync (Enumerable.Range (0, resolved.Length),
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (index, token) =>
            {
                Package item = resolved [index];
                if (!string.IsNullOrEmpty (item.Version)) return;
                Package? match = await resolve (item, token);
                if (match is not null) resolved [index] = match;
            });
        return [.. resolved];
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
