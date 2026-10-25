
namespace WinGetScout;

/// <summary>
/// Mock backend used when winget is not available (e.g., running this on Linux/macOS for parity testing).
/// </summary>
public sealed class MockBackend : IBackend
{
    private readonly PackageDetail [] _catalog = MockCatalog.Load ();
    private IEnumerable<PackageDetail> InstalledTemplates =>
        _catalog.Where (package => !string.IsNullOrEmpty (package.InstalledVersion));
    private IReadOnlyList<PackageDetail> SearchTemplates => _catalog;

    private readonly object _pinsGate = new ();
    private readonly Dictionary<string, PinState> _pins = new (StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _upgradedVersions = new (StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<Package>> SearchAsync (string query, string? source, CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<Package>> (
            Materialize (
                SearchTemplates,
                template => (string.IsNullOrEmpty (query)
                             || template.Name.Contains (query, StringComparison.OrdinalIgnoreCase)
                             || template.Id.Contains (query, StringComparison.OrdinalIgnoreCase))
                            && (string.IsNullOrEmpty (source)
                                || template.Source.Equals (source, StringComparison.OrdinalIgnoreCase)),
                ct, search: true));
    }

    public Task<IReadOnlyList<Package>> ListInstalledAsync (string? source, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Package>> (
            Materialize (
                InstalledTemplates,
                template => string.IsNullOrEmpty (source)
                            || template.Source.Equals (source, StringComparison.OrdinalIgnoreCase),
                ct));

    public Task<IReadOnlyList<Package>> ListUpgradesAsync (string? source, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Package>> (
            Materialize (
                InstalledTemplates,
                template => template.AvailableVersion is not null
                            && (string.IsNullOrEmpty (source)
                                || template.Source.Equals (source, StringComparison.OrdinalIgnoreCase)),
                ct).Where (package => package.AvailableVersion is not null).ToArray ());

    public Task<IReadOnlyList<string>> ListSourcesAsync (CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested ();

        return Task.FromResult<IReadOnlyList<string>> (["winget", "msstore"]);
    }

    public Task<PackageDetail?> ShowAsync (string id, CancellationToken ct, string? source = null)
    {
        ct.ThrowIfCancellationRequested ();
        PackageDetail? template = SearchTemplates
                                                     .FirstOrDefault (x => x.Id.Equals (id, StringComparison.OrdinalIgnoreCase)
                                                         && (source is null || x.Source.Equals (source, StringComparison.OrdinalIgnoreCase)));

        if (template is null)
        {
            return Task.FromResult<PackageDetail?> (null);
        }

        Package p = CreatePackage (template, SnapshotPins (ct));

        PackageDetail detail = new ()
        {
            Id = p.Id,
            Name = p.Name,
            Version = p.Version,
            AvailableVersion = p.AvailableVersion,
            InstalledVersion = p.InstalledVersion,
            Source = p.Source,
            PinState = p.PinState,
            Publisher = template.Publisher,
            Description = template.Description,
            Homepage = template.Homepage,
            License = template.License,
            ReleaseNotesUrl = template.ReleaseNotesUrl,
            SupportUrl = template.SupportUrl,
            Author = template.Author,
            Copyright = template.Copyright,
            PrivacyUrl = template.PrivacyUrl,
            PurchaseUrl = template.PurchaseUrl,
            InstallationNotes = template.InstallationNotes,
            Tags = template.Tags?.ToArray (),
            Documentation = template.Documentation?.ToArray (),
            ProductCodes = template.ProductCodes?.ToArray (),
            PackageFamilyNames = template.PackageFamilyNames?.ToArray ()
        };

        return Task.FromResult<PackageDetail?> (detail);
    }

    public Task<IReadOnlyList<string>> ListVersionsAsync (string id, CancellationToken ct, string? source = null)
    {
        ct.ThrowIfCancellationRequested ();
        PackageDetail? p = SearchTemplates
                                            .FirstOrDefault (x => x.Id.Equals (id, StringComparison.OrdinalIgnoreCase)
                                                && (source is null || x.Source.Equals (source, StringComparison.OrdinalIgnoreCase)));
        string baseV = p?.Version ?? "1.0.0";

        // Use only observed catalog/installed versions, newest first; do not invent history.
        IReadOnlyList<string> versions = new [] { baseV, p?.InstalledVersion }
                                         .OfType<string> ()
                                         .Where (version => !version.StartsWith ('<'))
                                         .Distinct (StringComparer.OrdinalIgnoreCase)
                                         .OrderByDescending (version => version, Comparer<string>.Create (CliBackend.CompareVersionsLike))
                                         .ToList ();

        return Task.FromResult (versions);
    }

    public Task<InstallerPreview?> GetInstallerPreviewAsync (string id, string? version, InstallSettings? settings, CancellationToken ct, string? source = null)
    {
        ct.ThrowIfCancellationRequested ();
        PackageDetail? p = SearchTemplates
                                            .FirstOrDefault (x => x.Id.Equals (id, StringComparison.OrdinalIgnoreCase)
                                                && (source is null || x.Source.Equals (source, StringComparison.OrdinalIgnoreCase)));
        if (p is null) return Task.FromResult<InstallerPreview?> (null);

        InstallerPreview preview = new ()
        {
            InstallerType = p?.Source == "msstore" ? "Store" : "MSI",
            Architecture = settings?.Architecture is { } arch && arch != InstallArchPref.Default ? arch.ToString ().ToLowerInvariant () : "x64",
            Scope = settings?.Scope == InstallScopePref.User ? "user" : "machine",
            RequiresElevation = true,
            Version = version ?? p?.AvailableVersion ?? p?.Version
        };

        return Task.FromResult<InstallerPreview?> (preview);
    }

    public Task<InstallVerification?> VerifyInstalledAsync (string id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested ();
        // Deterministically fake a "corrupt" result for one package so the Issues path is visible.
        bool corrupt = id.Contains ("Firefox", StringComparison.OrdinalIgnoreCase);

        InstallVerification v = new ()
        {
            Outcome = corrupt ? VerifyOutcome.Issues : VerifyOutcome.Ok,
            Checks =
            [
                new ("Registry entry", true, @"HKLM\…\Uninstall"),
                new ("Install location", !corrupt, @"C:\Program Files\" + id),
                new ("Install-location file", !corrupt, corrupt ? "missing: app.exe" : "app.exe")
            ]
        };

        return Task.FromResult<InstallVerification?> (v);
    }

    public bool CanVerify => true;
    public bool CanRepair => true;

    public async Task<OpResult> RepairAsync (string id, IProgress<OpProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested ();
        // Synthesize a repair ramp so the flow is exercisable on Linux (mirrors how the mock's
        // Verify fakes a result). No download phase — repair re-runs the local installer.
        if (progress is not null)
        {
            for (int i = 0; i <= 10; i++)
            {
                progress.Report (new (OpPhase.Repairing, i / 10.0));
                await Task.Delay (45, ct);
            }

            progress.Report (new (OpPhase.Done, 1.0));
        }

        return new ()
        {
            Operation = new () { Kind = OperationKind.Repair, PackageId = id },
            Success = true,
            Message = $"[mock] Repaired {id}"
        };
    }

    public async Task<OpResult> InstallAsync (string id, string? version, InstallSettings? settings, IProgress<OpProgress>? progress, CancellationToken ct, string? source = null)
    {
        ct.ThrowIfCancellationRequested ();
        await SimulateProgressAsync (progress, downloads: true, ct);

        string note = settings is null
                          ? string.Empty
                          : $" [{settings.Scope}/{settings.Mode}/{settings.Architecture}{(string.IsNullOrWhiteSpace (settings.CustomArgs) ? string.Empty : $"/\"{settings.CustomArgs}\"")}]";

        return new ()
        {
            Operation = new () { Kind = OperationKind.Install, PackageId = id, Version = version },
            Success = true,
            Message = $"[mock] Installed {id}" + (version is null ? string.Empty : $" v{version}") + note
        };
    }

    public async Task<OpResult> DownloadAsync (string id, string? version, IProgress<OpProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested ();
        // Download-only: a download ramp with no install phase.
        if (progress is not null)
        {
            for (int i = 0; i <= 10; i++)
            {
                progress.Report (new (OpPhase.Downloading, i / 10.0));
                await Task.Delay (55, ct);
            }

            progress.Report (new (OpPhase.Done, 1.0));
        }

        return new ()
        {
            Operation = new () { Kind = OperationKind.Download, PackageId = id, Version = version },
            Success = true,
            Message = $"[mock] Downloaded {id} to (mock path)"
        };
    }

    public async Task<OpResult> UninstallAsync (string id, IProgress<OpProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested ();
        await SimulateProgressAsync (progress, downloads: false, ct);

        return new ()
        {
            Operation = new () { Kind = OperationKind.Uninstall, PackageId = id },
            Success = true,
            Message = $"[mock] Uninstalled {id}"
        };
    }

    public async Task<OpResult> UpgradeAsync (string id, IProgress<OpProgress>? progress, CancellationToken ct, string? source = null)
    {
        ct.ThrowIfCancellationRequested ();
        await SimulateProgressAsync (progress, downloads: true, ct);

        PackageDetail? template = InstalledTemplates.FirstOrDefault (x => x.Id.Equals (id, StringComparison.OrdinalIgnoreCase)
            && (source is null || x.Source.Equals (source, StringComparison.OrdinalIgnoreCase)));
        if (template?.AvailableVersion is { } upgradedVersion)
        {
            lock (_pinsGate) _upgradedVersions[$"{template.Source}\u001f{template.Id}"] = upgradedVersion;
        }

        return new ()
        {
            Operation = new () { Kind = OperationKind.Upgrade, PackageId = id },
            Success = true,
            Message = $"[mock] Upgraded {id}"
        };
    }

    /// <summary>
    /// Synthesize a believable progress ramp so the status-bar progress UI can be exercised on
    /// any host (the mock has no real work to do). Downloads ramp 0→1 then install ramps 0→1;
    /// uninstall skips the download phase.
    /// </summary>
    private static async Task SimulateProgressAsync (IProgress<OpProgress>? progress, bool downloads, CancellationToken ct)
    {
        if (progress is null)
        {
            return;
        }

        if (downloads)
        {
            for (int i = 0; i <= 10; i++)
            {
                progress.Report (new (OpPhase.Downloading, i / 10.0));
                await Task.Delay (55, ct);
            }
        }

        for (int i = 0; i <= 10; i++)
        {
            progress.Report (new (OpPhase.Installing, i / 10.0));
            await Task.Delay (45, ct);
        }

        progress.Report (new (OpPhase.Done, 1.0));
    }

    public Task<OpResult> PinAsync (string id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested ();

        lock (_pinsGate)
        {
            _pins [id] = new (PinStateKind.Blocking);
        }

        return Task.FromResult (new OpResult
        {
            Operation = new () { Kind = OperationKind.Pin, PackageId = id },
            Success = true,
            Message = $"[mock] Pinned {id}"
        });
    }

    public Task<OpResult> UnpinAsync (string id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested ();

        lock (_pinsGate)
        {
            _pins.Remove (id);
        }

        return Task.FromResult (new OpResult
        {
            Operation = new () { Kind = OperationKind.Unpin, PackageId = id },
            Success = true,
            Message = $"[mock] Unpinned {id}"
        });
    }

    public Task<IReadOnlyDictionary<string, PinState>> ListPinsAsync (CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<string, PinState>> (SnapshotPins (ct));

    public Task<string> DescribeAsync (CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested ();

        return Task.FromResult ("Mock backend");
    }

    private IReadOnlyList<Package> Materialize (
        IEnumerable<PackageDetail> templates,
        Func<PackageDetail, bool> predicate,
        CancellationToken ct, bool search = false)
    {
        IReadOnlyDictionary<string, PinState> pins = SnapshotPins (ct);
        List<Package> packages = [];

        foreach (PackageDetail template in templates)
        {
            ct.ThrowIfCancellationRequested ();

            if (predicate (template))
            {
                packages.Add (CreatePackage (template, pins, search));
            }
        }

        return packages;
    }

    private Dictionary<string, PinState> SnapshotPins (CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested ();

        lock (_pinsGate)
        {
            return new (_pins, StringComparer.OrdinalIgnoreCase);
        }
    }

    private Package CreatePackage (
        PackageDetail template,
        IReadOnlyDictionary<string, PinState> pins, bool search = false)
    {
        string? upgradedVersion;
        lock (_pinsGate) _upgradedVersions.TryGetValue ($"{template.Source}\u001f{template.Id}", out upgradedVersion);
        return new ()
        {
            Id = template.Id,
            Name = template.Name,
            Version = upgradedVersion ?? (search ? template.Version : template.InstalledVersion ?? template.Version),
            InstalledVersion = upgradedVersion ?? template.InstalledVersion,
            Source = template.Source,
            AvailableVersion = upgradedVersion is null ? template.AvailableVersion : null,
            PinState = pins.GetValueOrDefault (template.Id, PinState.Unpinned)
        };
    }

}
