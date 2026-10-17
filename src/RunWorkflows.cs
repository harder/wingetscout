using System.Text;

namespace WinGetScout;

/// <summary>The user's requested operation, retained separately from the backend's result.</summary>
public sealed record RunRequest (OperationKind Kind, string? Version = null, InstallSettings? Settings = null)
{
    public bool IsSupported => Kind is OperationKind.Install or OperationKind.Upgrade or OperationKind.Download
        or OperationKind.Uninstall or OperationKind.Repair;
    public string Description => (Kind is OperationKind.Install or OperationKind.Download
        ? $"{Kind} {Version ?? "latest / backend default"}" : Kind.ToString ())
        + (Settings is null ? string.Empty : $" · scope {Settings.Scope} · mode {Settings.Mode} · architecture {Settings.Architecture} · arguments: {Settings.CustomArgs}");

    public Task<OpResult> ExecuteAsync (IBackend backend, Package package, IProgress<OpProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested ();
        return Kind switch
        {
            OperationKind.Install => backend.InstallAsync (package.Id, Version, Settings, progress, ct, package.Source),
            OperationKind.Upgrade => backend.UpgradeAsync (package.Id, progress, ct, package.Source),
            OperationKind.Download => backend.DownloadAsync (package.Id, Version, progress, ct),
            OperationKind.Uninstall => backend.UninstallAsync (package.Id, progress, ct),
            OperationKind.Repair => backend.RepairAsync (package.Id, progress, ct),
            _ => throw new InvalidOperationException ("This operation cannot be retried from history.")
        };
    }
}

public static class RetryPlanning
{
    /// <summary>Read-only preflight. The returned plan still requires explicit approval.</summary>
    public static async Task<BatchPlan> ReviewAsync (IBackend backend, IReadOnlyList<RunItem> items, CancellationToken ct)
    {
        List<BatchPlanItem> plan = [];
        RunItem [] retryable = items.Where (x => x.CanRetry).ToArray ();
        IReadOnlyList<Package> installed = retryable.Any (x => x.Request!.Kind is OperationKind.Install or OperationKind.Uninstall or OperationKind.Repair)
            ? await backend.ListInstalledAsync (null, ct) : [];
        IReadOnlyList<string> sources = retryable.Any (x => x.Request!.Kind is OperationKind.Install or OperationKind.Download)
            ? await backend.ListSourcesAsync (ct) : [];
        bool upgradesNeeded = retryable.Any (x => x.Request!.Kind == OperationKind.Upgrade);
        IReadOnlyList<Package> upgrades = upgradesNeeded ? await backend.ListUpgradesAsync (null, ct) : [];
        IReadOnlyDictionary<string, PinState> pins = upgradesNeeded
            ? await backend.ListPinsAsync (ct) : new Dictionary<string, PinState> ();
        foreach (RunItem item in items)
        {
            ct.ThrowIfCancellationRequested ();
            Package original = new () { Id = item.Id, Name = item.Name, Source = item.Source };
            RunRequest? request = item.Request;
            if (!item.CanRetry || request is null)
            {
                plan.Add (new (original, false, "No safe retry request was recorded."));
                continue;
            }
            if ((request.Kind is OperationKind.Install or OperationKind.Download)
                && !sources.Contains (item.Source, StringComparer.OrdinalIgnoreCase))
            {
                plan.Add (new (original, false, "Source is unavailable. Refresh sources and try again.") { Request = request });
                continue;
            }
            bool catalog = request.Kind is OperationKind.Install or OperationKind.Download;
            IReadOnlyList<Package> matches = catalog
                ? await backend.SearchAsync (item.Id, item.Source, ct)
                : request.Kind == OperationKind.Upgrade
                    ? upgrades : installed;
            Package? package = BatchPlanning.ExactMatch (matches, item.Id, item.Source, out bool ambiguous);
            string? skip = ambiguous
                ? "Package ID and source are ambiguous: multiple exact matches were returned."
                : package is null || package.IsTruncated ? "Exact package and source are no longer available." : null;
            if (skip is null && request.Kind == OperationKind.Install)
            {
                BatchPlan check = BatchPlanning.ForInstalls ([package!], installed, sources);
                if (!check.Items [0].CanRun) skip = check.Items [0].Reason;
            }
            if (skip is null && request.Kind == OperationKind.Upgrade)
            {
                package!.PinState = pins.FirstOrDefault (x => x.Key.Equals (item.Id, StringComparison.OrdinalIgnoreCase)).Value;
                BatchPlan check = BatchPlanning.ForUpgrades ([package], pinsFresh: true);
                if (!check.Items [0].CanRun) skip = check.Items [0].Reason;
            }
            if (skip is null && request.Kind == OperationKind.Repair && !backend.CanRepair)
                skip = "The current backend does not support repair.";
            if (skip is null && request.Version is { } version
                && !(await backend.ListVersionsAsync (item.Id, ct, item.Source)).Contains (version, StringComparer.OrdinalIgnoreCase))
                skip = $"Requested version {version} is no longer available.";
            // These APIs accept an ID without a source. Reject ambiguous IDs instead of changing targets.
            if (skip is null && (request.Kind is OperationKind.Download or OperationKind.Uninstall or OperationKind.Repair))
            {
                IReadOnlyList<Package> all = catalog ? await backend.SearchAsync (item.Id, null, ct)
                    : installed;
                Package [] targets = all.Where (p => p.Id.Equals (item.Id, StringComparison.OrdinalIgnoreCase)).Take (2).ToArray ();
                if (targets.Length != 1 || !targets [0].Source.Equals (item.Source, StringComparison.OrdinalIgnoreCase))
                    skip = "Package ID is ambiguous across sources or its source has changed.";
            }
            string reason = request.Description;
            if (request.Kind == OperationKind.Upgrade && package is not null)
                reason += $" · {package.Version} → {package.AvailableVersion}";
            plan.Add (new (package ?? original, skip is null, skip ?? reason) { Request = request });
        }
        return new ("Retry", plan);
    }
}

public static class DiagnosticText
{
    public const int MaxCharacters = 16_384;
    public static string Clean (string text, int maximumCharacters = MaxCharacters)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan (maximumCharacters, 64);
        ArgumentOutOfRangeException.ThrowIfGreaterThan (maximumCharacters, MaxCharacters);
        StringBuilder clean = new ();
        bool truncated = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text [i];
            if (ch == '\u001b')
            {
                if (i + 1 < text.Length && text [i + 1] == '[')
                {
                    i += 2;
                    while (i < text.Length && text [i] is < '@' or > '~') i++;
                }
                else if (i + 1 < text.Length && text [i + 1] == ']')
                {
                    i += 2;
                    while (i < text.Length && text [i] != '\a')
                    {
                        if (text [i] == '\u001b' && i + 1 < text.Length && text [i + 1] == '\\') { i++; break; }
                        i++;
                    }
                }
                continue;
            }
            if (!char.IsControl (ch) || ch is '\n' or '\t') clean.Append (ch);
            if (clean.Length >= maximumCharacters * 2)
            {
                clean.Remove (maximumCharacters / 2, maximumCharacters);
                truncated = true;
            }
        }
        if (!truncated && clean.Length <= maximumCharacters) return clean.ToString ();
        const string marker = "\n… output truncated …\n";
        int headLength = (maximumCharacters - marker.Length) / 2;
        int tailStart = clean.Length - (maximumCharacters - marker.Length - headLength);
        if (char.IsHighSurrogate (clean [headLength - 1])) headLength--;
        if (char.IsLowSurrogate (clean [tailStart])) tailStart++;
        return clean.ToString (0, headLength) + marker + clean.ToString (tailStart, clean.Length - tailStart);
    }
}
