namespace WinGetScout.Tests;

public sealed class RunWorkflowTests
{
    internal static Package Tool (string source = "winget") => new ()
        { Id = "Example.Tool", Name = "Example", Source = source, Version = "1", AvailableVersion = "2" };
    internal static RunItem Failed (OperationKind kind = OperationKind.Install, string? version = null, InstallSettings? settings = null) =>
        new ("Example.Tool", "Example", "winget", "Failed", "Installer failed") { Request = new (kind, version, settings) };

    [Fact]
    public void History_RoundTripsDiagnosticsAndExactInstallRequestAlongsideLegacyRecords ()
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-history-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (root);
        try
        {
            File.WriteAllText (Path.Combine (root, "runs.json"), """
                {"Runs":[{"StartedAtUtc":"2026-01-01T00:00:00Z","FinishedAtUtc":"2026-01-01T00:00:01Z","Action":"Install",
                "Items":[{"Id":"Old.Tool","Name":"Old","Source":"winget","Status":"Failed","Reason":"Old failure"}]}]}
                """);
            WorkflowStore store = new (root);
            Assert.False (store.Runs () [0].Items [0].CanRetry);
            InstallSettings settings = new () { Scope = InstallScopePref.User, Architecture = InstallArchPref.Arm64,
                Mode = InstallModePref.Silent, CustomArgs = "/feature=Tools" };
            DateTimeOffset started = DateTimeOffset.UtcNow;
            OpResult result = CliBackend.CliResult (new () { Kind = OperationKind.Install }, 1603,
                "Starting\n\u001b[31mInstaller failed\u001b[0m\nCheck log C:\\Temp\\setup.log");
            RunRecord record = RunRecord.SinglePackage (Tool (), OperationKind.Install, started, started.AddSeconds (12),
                result, false, new (OperationKind.Install, "1.2.3", settings));
            store.SaveRun (record);
            RunItem item = store.Runs () [0].Items [0];
            Assert.True (item.CanRetry);
            Assert.Equal (1603, item.ExitCode);
            Assert.Equal ("WinGet CLI", item.Backend);
            Assert.Equal ("1.2.3", item.TargetVersion);
            Assert.Equal (InstallScopePref.User, item.Request!.Settings!.Scope);
            Assert.Equal ("/feature=Tools", item.Request.Settings.CustomArgs);
            Assert.Contains ("Installer failed\nCheck log", item.Diagnostics);
            Assert.DoesNotContain ('\u001b', item.DetailText ());
            Assert.Contains ("12.0s", item.DetailText ());
            Assert.Equal (2, store.Runs ().Count);
            Assert.Null (store.Runs () [1].Items [0].Request);
        }
        finally { Directory.Delete (root, true); }
    }

    [Fact]
    public void Diagnostics_PreserveLinesStripEscapeSequencesAndBoundLargeOutput ()
    {
        Assert.Equal ("First\nSecond\tvalue", DiagnosticText.Clean ("\u001b]0;title\aFirst\r\n\u001b[31mSecond\u001b[0m\tvalue\0"));
        string large = DiagnosticText.Clean ("Command started\n" + new string ('x', DiagnosticText.MaxCharacters * 5) + "\nFinal failure: 1603");
        Assert.True (large.Length <= DiagnosticText.MaxCharacters);
        Assert.Contains ("output truncated", large);
        Assert.StartsWith ("Command started", large);
        Assert.EndsWith ("Final failure: 1603", large);
    }

    [Theory]
    [InlineData (9000)]
    [InlineData (17000)]
    [InlineData (50000)]
    public void Diagnostics_TruncationKeepsValidUnicodeAndTheFinalError (int count)
    {
        string text = DiagnosticText.Clean (string.Concat (Enumerable.Repeat ("🚀", count)) + "\nFinal error");
        Assert.True (text.Length <= DiagnosticText.MaxCharacters);
        Assert.EndsWith ("Final error", text);
        _ = new UTF8Encoding (false, true).GetBytes (text);
    }

    [Fact]
    public async Task CancelledRequestCannotReachTheBackend ()
    {
        RecordingBackend backend = new ();
        using CancellationTokenSource cancellation = new ();
        cancellation.Cancel ();
        await Assert.ThrowsAsync<OperationCanceledException> (() => new RunRequest (OperationKind.Install)
            .ExecuteAsync (backend, Tool (), null, cancellation.Token));
        Assert.Equal (0, backend.Mutations);
    }

    [Theory]
    [InlineData (OperationKind.Pin)]
    [InlineData (OperationKind.Unpin)]
    [InlineData (OperationKind.BatchUpgrade)]
    public void History_UnsupportedOrSuccessfulRequestsAreNotRetryable (OperationKind kind)
    {
        Assert.False (Failed (kind).CanRetry);
        Assert.False ((Failed () with { Status = "Succeeded" }).CanRetry);
        Assert.True ((Failed () with { Status = "Skipped" }).CanRetry);
        Assert.False ((Failed () with { Source = string.Empty }).CanRetry);
    }

    [Fact]
    public async Task RetryPreflight_IsReadOnlyAndPreservesVersionOptionsAndSource ()
    {
        RecordingBackend backend = new ();
        InstallSettings options = new () { Scope = InstallScopePref.Machine, Mode = InstallModePref.Silent,
            Architecture = InstallArchPref.X64, CustomArgs = "/custom" };
        RunItem item = Failed (version: "2", settings: options);
        BatchPlan plan = await RetryPlanning.ReviewAsync (backend, [item], CancellationToken.None);
        Assert.Equal (0, backend.Mutations);
        BatchPlanItem ready = Assert.Single (plan.Items);
        Assert.True (ready.CanRun);
        Assert.Contains ("Machine", ready.Reason);
        Assert.Contains ("/custom", ready.Reason);
        await ready.Request!.ExecuteAsync (backend, ready.Package, null, CancellationToken.None);
        Assert.Equal (1, backend.Mutations);
        Assert.Equal ("winget", backend.LastSource);
        Assert.Equal ("2", backend.LastVersion);
        Assert.Same (options, backend.LastSettings);
    }

    [Theory]
    [InlineData (OperationKind.Install)]
    [InlineData (OperationKind.Download)]
    public async Task RetryCatalogRequestSkipsRemovedSourceWithoutQueryingItAndKeepsOtherRequests (OperationKind kind)
    {
        RecordingBackend backend = new ()
        {
            SearchFailure = source => source == "removed" ? new InvalidOperationException ("Source was removed") : null
        };
        BatchPlan plan = await RetryPlanning.ReviewAsync (backend,
            [Failed (kind) with { Source = "removed" }, Failed (kind)], CancellationToken.None);
        Assert.Equal (2, plan.Items.Count);
        Assert.False (plan.Items [0].CanRun);
        Assert.Contains ("Source is unavailable", plan.Items [0].Reason);
        Assert.True (plan.Items [1].CanRun);
        Assert.DoesNotContain ("removed", backend.SearchSources);
        Assert.Equal (0, backend.Mutations);

        backend.SearchFailure = _ => new InvalidOperationException ("Catalog query failed");
        await Assert.ThrowsAsync<InvalidOperationException> (() => RetryPlanning.ReviewAsync (backend,
            [Failed (kind) with { Source = "removed" }, Failed (kind)], CancellationToken.None));
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public void CliShowNotFoundReturnsNoManifestButOtherFailuresRemainErrors ()
    {
        string [] args = CliBackend.ShowArgs ("Missing.Tool");
        ProcessRunner.RunResult missing = new (unchecked ((int)0x8A150014),
            "No package found matching input criteria.", false, false, false);
        string output = CliBackend.VerifiedQueryOutput (missing, "winget", args);
        Assert.Null (CliBackend.ParseShow ("Missing.Tool", output));

        ProcessRunner.RunResult failed = new (unchecked ((int)0x8A150012),
            "No sources match the given value: Missing.Source", false, false, false);
        Assert.Throws<InvalidOperationException> (() => CliBackend.VerifiedQueryOutput (failed, "winget", args));
    }

    [Theory]
    [InlineData (OperationKind.Install)]
    [InlineData (OperationKind.Download)]
    [InlineData (OperationKind.Upgrade)]
    public async Task RetryReviewSkipsDuplicateExactMatchesAndKeepsUnrelatedRequests (OperationKind kind)
    {
        Package other = new () { Id = "Other.Tool", Name = "Other", Source = "winget", Version = "1", AvailableVersion = "2" };
        RecordingBackend backend = new () { Catalog = [Tool (), Tool (), other] };
        RunItem duplicate = Failed (kind);
        RunItem valid = duplicate with { Id = other.Id, Name = other.Name };

        BatchPlan plan = await RetryPlanning.ReviewAsync (backend, [duplicate, valid], CancellationToken.None);

        Assert.Equal (2, plan.Items.Count);
        Assert.Equal (1, plan.Ready);
        Assert.False (plan.Items [0].CanRun);
        Assert.Contains ("ambiguous", plan.Items [0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True (plan.Items [1].CanRun);
        Assert.Equal (other.Id, plan.Items [1].Package.Id);
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public async Task MockDownloadRetryCanReviewDuplicateCatalogEntriesWithoutThrowing ()
    {
        RunItem item = new ("Microsoft.VisualStudioCode", "Visual Studio Code", "winget", "Skipped", "Cancelled.")
            { Request = new (OperationKind.Download) };
        BatchPlan plan = await RetryPlanning.ReviewAsync (new MockBackend (), [item], CancellationToken.None);
        Assert.Equal (0, plan.Ready);
        Assert.Contains ("ambiguous", Assert.Single (plan.Items).Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CliInventoryErrorsCannotBeParsedAsEmptyResults ()
    {
        ProcessRunner.RunResult failed = new (unchecked ((int)0x8A150012),
            "No sources match the given value: Scout.Nonexistent.Source", false, false, false);
        InvalidOperationException error = Assert.Throws<InvalidOperationException> (() =>
            CliBackend.VerifiedQueryOutput (failed, "winget", CliBackend.ListInstalledArgs ("Scout.Nonexistent.Source")));
        Assert.Contains ("0x8A150012", error.Message);
        Assert.Contains ("No sources match", error.Message);
    }

    [Theory]
    [InlineData ("list")]
    [InlineData ("upgrade")]
    [InlineData ("search")]
    public void CliKnownEmptyPackageQueriesRemainValid (string command)
    {
        ProcessRunner.RunResult empty = new (unchecked ((int)0x8A150014),
            "No installed package found matching input criteria.", false, false, false);
        string output = CliBackend.VerifiedQueryOutput (empty, "winget", [command]);
        Assert.Empty (CliBackend.ParseTable (output, hasAvailable: command == "upgrade"));
        Assert.Throws<InvalidOperationException> (() => CliBackend.VerifiedQueryOutput (empty, "winget", ["source", "list"]));
    }

    [Fact]
    public async Task RetryPreflight_RequiresVerifiedInventoryEvenWhenCatalogLookupSucceeds ()
    {
        RecordingBackend backend = new () { InstalledFailure = new InvalidOperationException ("Inventory source unavailable") };
        Assert.Single (await backend.SearchAsync ("Example.Tool", "winget", CancellationToken.None));
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException> (() =>
            RetryPlanning.ReviewAsync (backend, [Failed ()], CancellationToken.None));
        Assert.Contains ("Inventory source unavailable", error.Message);
        Assert.Equal (0, backend.Mutations);
    }

    [Theory]
    [InlineData ("winget", 0)]
    [InlineData ("msstore", 1)]
    public async Task RetryPreflight_ChecksVersionsInTheRecordedSource (string source, int ready)
    {
        RecordingBackend backend = new ()
        {
            Catalog = [Tool (), Tool ("msstore")],
            Sources = ["winget", "msstore"],
            Versions = selectedSource => selectedSource == "winget" ? ["1"] : ["2"]
        };
        BatchPlan plan = await RetryPlanning.ReviewAsync (backend,
            [Failed (version: "2") with { Source = source }], CancellationToken.None);
        Assert.Equal (ready, plan.Ready);
        Assert.Equal (source, Assert.Single (backend.VersionSources));
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public async Task RetryPreflight_DoesNotReinstallAnAlreadyInstalledPackage ()
    {
        RecordingBackend backend = new () { Installed = [Tool ()] };
        BatchPlan plan = await RetryPlanning.ReviewAsync (backend, [Failed ()], CancellationToken.None);
        Assert.Equal (0, plan.Ready);
        Assert.Contains ("Already installed", plan.Items [0].Reason);
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public async Task RetryPreflight_RejectsChangedSourceUnavailableVersionAndAmbiguousId ()
    {
        RecordingBackend backend = new () { Catalog = [Tool ("other")] };
        BatchPlan changed = await RetryPlanning.ReviewAsync (backend, [Failed ()], CancellationToken.None);
        Assert.Equal (0, changed.Ready);
        backend.Catalog = [Tool ()];
        BatchPlan missing = await RetryPlanning.ReviewAsync (backend, [Failed (version: "99")], CancellationToken.None);
        Assert.Equal (0, missing.Ready);
        Assert.Contains ("99", missing.Items [0].Reason);
        backend.Catalog = [Tool (), Tool ("other")];
        BatchPlan ambiguous = await RetryPlanning.ReviewAsync (backend, [Failed (OperationKind.Download)], CancellationToken.None);
        Assert.Equal (0, ambiguous.Ready);
        Assert.Contains ("ambiguous", ambiguous.Items [0].Reason);
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public async Task RetryUpgrade_RechecksPinsAndCurrentAvailableVersion ()
    {
        RecordingBackend backend = new () { Pins = new Dictionary<string, PinState> { ["example.tool"] = new (PinStateKind.Blocking) } };
        BatchPlan pinned = await RetryPlanning.ReviewAsync (backend, [Failed (OperationKind.Upgrade)], CancellationToken.None);
        Assert.Equal (0, pinned.Ready);
        Assert.Contains ("Pinned", pinned.Items [0].Reason);
        backend.Pins = new Dictionary<string, PinState> ();
        BatchPlan ready = await RetryPlanning.ReviewAsync (backend, [Failed (OperationKind.Upgrade)], CancellationToken.None);
        Assert.Equal (1, ready.Ready);
        Assert.Contains ("1 → 2", ready.Items [0].Reason);
        backend.PinFailure = new InvalidOperationException ("Pins unavailable");
        await Assert.ThrowsAsync<InvalidOperationException> (() => RetryPlanning.ReviewAsync (backend,
            [Failed (OperationKind.Upgrade)], CancellationToken.None));
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public async Task RetryPreflight_RejectsRemovedInstallsAndUnavailableRepair ()
    {
        RecordingBackend backend = new ();
        Assert.Equal (0, (await RetryPlanning.ReviewAsync (backend, [Failed (OperationKind.Uninstall)], CancellationToken.None)).Ready);
        backend.Installed = [Tool ()];
        Assert.Equal (0, (await RetryPlanning.ReviewAsync (backend, [Failed (OperationKind.Repair)], CancellationToken.None)).Ready);
        Assert.Equal (1, (await RetryPlanning.ReviewAsync (backend, [Failed (OperationKind.Uninstall)], CancellationToken.None)).Ready);
    }

    [Fact]
    public async Task InstallReviewRefreshesCatalogAndSkipsRemovedMissingAndDuplicateSelections ()
    {
        Package other = new () { Id = "Other.Tool", Name = "Other", Source = "winget", Version = "3" };
        Package missing = new () { Id = "Gone.Tool", Name = "Gone", Source = "winget", Version = "1" };
        RecordingBackend backend = new ()
        {
            Catalog = [Tool (), Tool (), other],
            SearchFailure = source => source == "removed" ? new InvalidOperationException ("Source removed") : null
        };
        BatchPlan plan = await BatchPlanning.ReviewInstallsAsync (backend,
            [Tool ("removed"), missing, Tool (), new () { Id = other.Id, Name = other.Name, Source = other.Source, Version = "1" }], CancellationToken.None);
        Assert.Equal (4, plan.Items.Count);
        Assert.Equal (1, plan.Ready);
        Assert.Contains ("Source is unavailable", plan.Items [0].Reason);
        Assert.Contains ("no longer available", plan.Items [1].Reason);
        Assert.Contains ("ambiguous", plan.Items [2].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal ("3", plan.Items [3].Package.Version);
        Assert.DoesNotContain ("removed", backend.SearchSources);
        Assert.Equal (0, backend.Mutations);

        backend.SearchFailure = _ => new InvalidOperationException ("Catalog failed");
        await Assert.ThrowsAsync<InvalidOperationException> (() => BatchPlanning.ReviewInstallsAsync (backend, [Tool ()], CancellationToken.None));
        backend.SearchFailure = null;
        backend.SourceFailure = new InvalidOperationException ("Sources failed");
        await Assert.ThrowsAsync<InvalidOperationException> (() => BatchPlanning.ReviewInstallsAsync (backend, [Tool ()], CancellationToken.None));
        Assert.Equal (0, backend.Mutations);
    }

    [Theory]
    [InlineData (null)]
    [InlineData ("winget")]
    [InlineData ("Custom Source")]
    public void DetailCommandKeepsExactIdAndSelectedSource (string? source)
    {
        string [] args = CliBackend.ShowArgs ("Example.Tool", source);
        Assert.Equal ("Example.Tool", args [Array.IndexOf (args, "--id") + 1]);
        Assert.Contains ("--exact", args);
        if (source is null) Assert.DoesNotContain ("--source", args);
        else Assert.Equal (source, args [Array.IndexOf (args, "--source") + 1]);
    }

    [Fact]
    public async Task MockDetailsAndPreviewsRejectAnUnrelatedSource ()
    {
        MockBackend backend = new ();
        const string id = "Microsoft.VisualStudioCode";
        Assert.Equal ("winget", (await backend.ShowAsync (id, CancellationToken.None, "winget"))?.Source);
        Assert.Null (await backend.ShowAsync (id, CancellationToken.None, "missing"));
        Assert.NotNull (await backend.GetInstallerPreviewAsync (id, null, null, CancellationToken.None, "winget"));
        Assert.Null (await backend.GetInstallerPreviewAsync (id, null, null, CancellationToken.None, "missing"));
    }

    [Fact]
    public async Task UpgradeReviewSkipsDuplicateMatchesAndKeepsUnrelatedPackages ()
    {
        Package other = new () { Id = "Other.Tool", Name = "Other", Source = "winget", Version = "1", AvailableVersion = "2" };
        RecordingBackend backend = new () { Catalog = [Tool (), Tool (), other] };
        HashSet<string> selected = new (StringComparer.OrdinalIgnoreCase) { BatchPlanning.Key (Tool ()), BatchPlanning.Key (other) };
        BatchPlan plan = await BatchPlanning.ReviewUpgradesAsync (backend, selected, CancellationToken.None);
        Assert.Equal (1, plan.Ready);
        Assert.Contains ("ambiguous", plan.Items [0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True (plan.Items [1].CanRun);
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public async Task DownloadRetryRejectsSourceChangeBetweenScopedAndUnscopedQueries ()
    {
        RecordingBackend backend = new () { SearchResults = source => [Tool (source ?? "other")] };
        BatchPlan plan = await RetryPlanning.ReviewAsync (backend, [Failed (OperationKind.Download)], CancellationToken.None);
        Assert.Equal (0, plan.Ready);
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public async Task UpgradeReview_IncludesSelectionsHiddenBySourceAndExplainsMissingPackages ()
    {
        RecordingBackend backend = new () { Catalog = [Tool (), Tool ("msstore")] };
        HashSet<string> selected = new (StringComparer.OrdinalIgnoreCase)
            { BatchPlanning.Key (Tool ()), BatchPlanning.Key (Tool ("msstore")), "other\u001fMissing.Tool" };
        BatchPlan plan = await BatchPlanning.ReviewUpgradesAsync (backend, selected, CancellationToken.None);
        Assert.Equal (3, plan.Items.Count);
        Assert.Equal (2, plan.Ready);
        Assert.Equal ("winget", plan.Items [0].Package.Source);
        Assert.Equal ("msstore", plan.Items [1].Package.Source);
        Assert.Equal ("Missing.Tool", plan.Items [2].Package.Id);
        Assert.Contains ("no longer present", plan.Items [2].Reason);
        Assert.Equal (0, backend.Mutations);
        backend.PinFailure = new InvalidOperationException ("Cannot verify pins");
        await Assert.ThrowsAsync<InvalidOperationException> (() => BatchPlanning.ReviewUpgradesAsync (backend, selected, CancellationToken.None));
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public void Counts_IncludeSelectionsHiddenByFiltersOrEarlierSearchesAndDistinguishSources ()
    {
        Package visible = Tool ();
        ListCounts counts = ListCounts.Capture ([visible], 10,
            [BatchPlanning.Key (visible), BatchPlanning.Key (Tool ("msstore")), "winget\u001fAnother.Tool"]);
        Assert.Equal (3, counts.Selected);
        Assert.Equal (2, counts.HiddenSelected);
        Assert.Equal ("1 of 10 shown · 3 selected (2 hidden)", counts.Label);
        Assert.Equal ("0 of 10 shown · 3 selected (3 hidden)",
            ListCounts.Capture ([], 10, ["a", "b", "c"]).Label);
    }

    [Theory]
    [InlineData ("EXAMPLE.TOOL", "WINGET", true)]
    [InlineData ("Other.Tool", "winget", false)]
    [InlineData ("Example.Tool", "msstore", false)]
    [InlineData ("Example.Tool", "", false)]
    public void ContextLinksRequireMatchingPackageIdAndSource (string id, string source, bool expected)
    {
        Package package = new () { Id = "Example.Tool", Name = "Tool", Source = "winget" };
        PackageDetail detail = new ()
        {
            Id = id, Name = "Tool", Source = source,
            Homepage = "https://example.invalid/home", ReleaseNotesUrl = "https://example.invalid/releases"
        };
        string [] actions = PackageActions.For (AppMode.Search, package, true, false, false, 0, detail)
            .Select (action => action.Id).ToArray ();
        Assert.Equal (expected, actions.Contains ("homepage"));
        Assert.Equal (expected, actions.Contains ("changelog"));
        Assert.Contains ("install", actions);
    }

    [Fact]
    public void ContextActions_RespectModeCapabilitiesPinsAndEmptyLists ()
    {
        string [] Ids (AppMode mode, Package? package, bool fresh = true, bool verify = false, bool repair = false, int selected = 0) =>
            PackageActions.For (mode, package, fresh, verify, repair, selected, null).Select (x => x.Id).ToArray ();
        string [] search = Ids (AppMode.Search, Tool ());
        Assert.Contains ("install", search);
        Assert.DoesNotContain ("uninstall", search);
        Assert.DoesNotContain ("verify", search);
        Assert.DoesNotContain ("pin", search);
        string [] installed = Ids (AppMode.Installed, Tool (), verify: true, repair: true);
        Assert.Contains ("verify", installed);
        Assert.Contains ("repair", installed);
        Assert.Contains ("uninstall", installed);
        Assert.DoesNotContain ("install", installed);
        Assert.DoesNotContain ("pin", Ids (AppMode.Upgrades, Tool (), fresh: false));
        Assert.DoesNotContain ("upgrade", Ids (AppMode.Upgrades, Tool (), fresh: false));
        Package pinned = Tool (); pinned.PinState = new (PinStateKind.Blocking);
        Assert.DoesNotContain ("upgrade", Ids (AppMode.Upgrades, pinned));
        Assert.Equal (["history", "diagnostics"], Ids (AppMode.Search, null));
        Assert.Contains ("batch", Ids (AppMode.Search, null, selected: 2));
    }

    internal sealed class RecordingBackend : IBackend
    {
        public IReadOnlyList<Package> Catalog { get; set; } = [Tool ()];
        public IReadOnlyList<Package> Installed { get; set; } = [];
        public IReadOnlyList<string> Sources { get; set; } = ["winget"];
        public IReadOnlyDictionary<string, PinState> Pins { get; set; } = new Dictionary<string, PinState> ();
        public Exception? PinFailure { get; set; }
        public Exception? InstalledFailure { get; set; }
        public Exception? SourceFailure { get; set; }
        public Exception? DescriptionFailure { get; set; }
        public InstallerPreview? Preview { get; set; }
        public InstallModePref DefaultInstallMode { get; set; }
        public Func<CancellationToken, Task<OpResult>>? InstallResult { get; set; }
        public Func<OperationKind, string, IProgress<OpProgress>?, CancellationToken, Task<OpResult>>? OperationResult { get; set; }
        public Func<string?, CancellationToken, Task<IReadOnlyList<Package>>>? UpgradeQuery { get; set; }
        public Func<string?, IReadOnlyList<string>>? Versions { get; set; }
        public Func<string, string?, CancellationToken, Task<IReadOnlyList<string>>>? VersionQuery { get; set; }
        public Func<string?, Exception?>? SearchFailure { get; set; }
        public Func<string?, IReadOnlyList<Package>>? SearchResults { get; set; }
        public Func<string, CancellationToken, Task<IReadOnlyList<Package>>>? CatalogQuery { get; set; }
        public List<string?> SearchSources { get; } = [];
        public List<string?> VersionSources { get; } = [];
        public int Mutations { get; private set; }
        public string? LastSource { get; private set; }
        public string? LastVersion { get; private set; }
        public InstallSettings? LastSettings { get; private set; }
        public OperationKind? LastKind { get; private set; }
        public Task<IReadOnlyList<Package>> SearchAsync (string query, string? source, CancellationToken ct)
        {
            lock (SearchSources) SearchSources.Add (source);
            if (CatalogQuery is not null) return CatalogQuery (query, ct);
            return SearchFailure?.Invoke (source) is { } failure ? Task.FromException<IReadOnlyList<Package>> (failure)
                : Task.FromResult (SearchResults?.Invoke (source) ?? Catalog.Where (p => source is null || p.Source == source).ToArray ());
        }
        public Task<IReadOnlyList<Package>> ListInstalledAsync (string? source, CancellationToken ct) => InstalledFailure is null
            ? Task.FromResult (Installed) : Task.FromException<IReadOnlyList<Package>> (InstalledFailure);
        public Task<IReadOnlyList<Package>> ListUpgradesAsync (string? source, CancellationToken ct) => UpgradeQuery?.Invoke (source, ct) ?? Task.FromResult (Catalog);
        public Task<PackageDetail?> ShowAsync (string id, CancellationToken ct, string? source = null) => Task.FromResult<PackageDetail?> (null);
        public Task<IReadOnlyList<string>> ListSourcesAsync (CancellationToken ct) => SourceFailure is null
            ? Task.FromResult (Sources) : Task.FromException<IReadOnlyList<string>> (SourceFailure);
        public Task<IReadOnlyList<string>> ListVersionsAsync (string id, CancellationToken ct, string? source = null)
        {
            VersionSources.Add (source);
            if (VersionQuery is not null) return VersionQuery (id, source, ct);
            return Task.FromResult (Versions?.Invoke (source) ?? (IReadOnlyList<string>)["1", "2"]);
        }
        public List<string?> PreviewSources { get; } = [];
        public Task<InstallerPreview?> GetInstallerPreviewAsync (string id, string? version, InstallSettings? settings, CancellationToken ct, string? source = null)
        {
            PreviewSources.Add (source);
            return Task.FromResult (Preview);
        }
        private Task<OpResult> Execute (OperationKind kind, string id, string? version, InstallSettings? settings, CancellationToken ct, string? source = null, IProgress<OpProgress>? progress = null)
        {
            Mutations++; LastKind = kind; LastSource = source; LastVersion = version; LastSettings = settings;
            return OperationResult?.Invoke (kind, id, progress, ct) ?? InstallResult?.Invoke (ct) ?? Task.FromResult (CliBackend.CliResult (new () { Kind = kind, PackageId = id }, 0, "Completed"));
        }
        public Task<OpResult> InstallAsync (string id, string? version, InstallSettings? settings, IProgress<OpProgress>? progress, CancellationToken ct, string? source = null) => Execute (OperationKind.Install, id, version, settings, ct, source, progress);
        public Task<OpResult> UpgradeAsync (string id, IProgress<OpProgress>? progress, CancellationToken ct, string? source = null) => Execute (OperationKind.Upgrade, id, null, null, ct, source, progress);
        public Task<OpResult> UninstallAsync (string id, IProgress<OpProgress>? progress, CancellationToken ct) => Execute (OperationKind.Uninstall, id, null, null, ct);
        public Task<OpResult> DownloadAsync (string id, string? version, IProgress<OpProgress>? progress, CancellationToken ct) => Execute (OperationKind.Download, id, version, null, ct);
        public Task<OpResult> RepairAsync (string id, IProgress<OpProgress>? progress, CancellationToken ct) => Execute (OperationKind.Repair, id, null, null, ct);
        public Task<OpResult> PinAsync (string id, CancellationToken ct) => Execute (OperationKind.Pin, id, null, null, ct);
        public Task<OpResult> UnpinAsync (string id, CancellationToken ct) => Execute (OperationKind.Unpin, id, null, null, ct);
        public InstallVerification? Verification { get; set; }
        public bool CanVerify => Verification is not null;
        public bool CanRepair { get; set; }
        public Task<InstallVerification?> VerifyInstalledAsync (string id, CancellationToken ct) => Task.FromResult (Verification);
        public Task<IReadOnlyDictionary<string, PinState>> ListPinsAsync (CancellationToken ct) => PinFailure is null ? Task.FromResult (Pins) : Task.FromException<IReadOnlyDictionary<string, PinState>> (PinFailure);
        public Task<string> DescribeAsync (CancellationToken ct) => DescriptionFailure is null
            ? Task.FromResult ("Test backend") : Task.FromException<string> (DescriptionFailure);
    }
}
