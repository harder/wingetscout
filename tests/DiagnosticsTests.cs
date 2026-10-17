using System.Text.Json;
using static WinGetScout.Tests.RunWorkflowTests;

namespace WinGetScout.Tests;

public sealed class DiagnosticsTests
{
    [Fact]
    public async Task SourceChecksAreReadOnlyAndKeepFailuresSeparateFromEmptyResults ()
    {
        RecordingBackend backend = new ()
        {
            Sources = ["winget", "Empty Source", "Broken Source"],
            SearchResults = source => source == "Empty Source" ? [] : [Tool ()],
            SearchFailure = source => source == "Broken Source" ? new InvalidOperationException ("Source unavailable") : null
        };
        BackendDiagnosticReport report = await BackendDiagnostics.CaptureAsync (backend, "COM unavailable", "winget", "Last refresh failed", CancellationToken.None);
        Assert.Equal (3, report.Sources.Count);
        Assert.True (report.Sources [0].Succeeded);
        Assert.True (report.Sources [1].Succeeded);
        Assert.Equal (0, report.Sources [1].MatchCount);
        Assert.False (report.Sources [2].Succeeded);
        Assert.Contains ("Source unavailable", report.Sources [2].Error);
        Assert.Contains ("COM unavailable", report.DetailText ());
        Assert.Contains ("Last refresh failed", report.DetailText ());
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public async Task DiscoveryFailureIsNotReportedAsNoConfiguredSources ()
    {
        RecordingBackend backend = new () { SourceFailure = new InvalidOperationException ("Discovery failed") };
        BackendDiagnosticReport report = await BackendDiagnostics.CaptureAsync (backend, null, null, null, CancellationToken.None);
        Assert.Contains ("Discovery failed", report.SourceError);
        Assert.Empty (report.Sources);
        Assert.Empty (backend.SearchSources);
        Assert.Equal (0, backend.Mutations);
    }

    [Fact]
    public async Task CancellationCannotBecomeAHealthyReport ()
    {
        using CancellationTokenSource cancellation = new ();
        cancellation.Cancel ();
        await Assert.ThrowsAnyAsync<OperationCanceledException> (() => BackendDiagnostics.CaptureAsync (new RecordingBackend (), null, null, null, cancellation.Token));
    }

    [Fact]
    public async Task TextAndJsonExportsKeepDiagnosticIdentityAndPreserveExistingFiles ()
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-diagnostic-export-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (root);
        try
        {
            DiagnosticDocument document = DiagnosticDocument.ForRun (Failed () with { Diagnostics = "First line\nFinal error", ExitCode = 1603 });
            string textPath = Path.Combine (root, "run.txt"), jsonPath = Path.Combine (root, "run.json");
            await DiagnosticExport.WriteAsync (textPath, document.Text, false, CancellationToken.None);
            await DiagnosticExport.WriteAsync (jsonPath, document.Json, false, CancellationToken.None);
            Assert.Contains ("Final error", await File.ReadAllTextAsync (textPath, TestContext.Current.CancellationToken));
            using JsonDocument json = JsonDocument.Parse (await File.ReadAllTextAsync (jsonPath, TestContext.Current.CancellationToken));
            Assert.Equal ("Example.Tool", json.RootElement.GetProperty ("Id").GetString ());
            Assert.Equal (1603, json.RootElement.GetProperty ("ExitCode").GetInt32 ());
            await Assert.ThrowsAsync<IOException> (() => DiagnosticExport.WriteAsync (textPath, "replacement", false, CancellationToken.None));
            Assert.Equal (document.Text, await File.ReadAllTextAsync (textPath, TestContext.Current.CancellationToken));
            using CancellationTokenSource cancellation = new ();
            cancellation.Cancel ();
            await Assert.ThrowsAnyAsync<OperationCanceledException> (() => DiagnosticExport.WriteAsync (textPath, "replacement", true, cancellation.Token));
            Assert.Equal (document.Text, await File.ReadAllTextAsync (textPath, TestContext.Current.CancellationToken));
            await DiagnosticExport.WriteAsync (textPath, "Confirmed replacement", true, CancellationToken.None);
            Assert.Equal ("Confirmed replacement", await File.ReadAllTextAsync (textPath, TestContext.Current.CancellationToken));
            Assert.Equal (2, Directory.GetFiles (root).Length);
        }
        finally { Directory.Delete (root, true); }
    }

    [Fact]
    public async Task BackendVersionFailureDoesNotHideSourceResultsAndJsonIsStructured ()
    {
        RecordingBackend backend = new () { DescriptionFailure = new InvalidOperationException ("Version failed") };
        BackendDiagnosticReport report = await BackendDiagnostics.CaptureAsync (backend, null, null, null, CancellationToken.None);
        Assert.Contains ("Version failed", report.BackendError);
        Assert.True (Assert.Single (report.Sources).Succeeded);
        DiagnosticDocument document = DiagnosticDocument.ForBackend (report);
        using JsonDocument json = JsonDocument.Parse (document.Json);
        Assert.Equal ("winget", json.RootElement.GetProperty ("Sources") [0].GetProperty ("Name").GetString ());
        Assert.Contains ("Version failed", document.Text);
        Assert.Equal (0, backend.Mutations);
    }

    [Theory]
    [InlineData ("arm64", "machine", true)]
    [InlineData ("x64", "user", true)]
    [InlineData ("X64", "MACHINE", false)]
    [InlineData ("Neutral", "Unknown", false)]
    [InlineData (null, null, false)]
    public void PreviewChecksRejectKnownConflictsWithoutInventingUnknownValues (string? arch, string? scope, bool conflict)
    {
        InstallSettings settings = new () { Architecture = InstallArchPref.X64, Scope = InstallScopePref.Machine };
        Assert.Equal (conflict, InstallOptionPresentation.PreviewConflict (settings, new () { Architecture = arch, Scope = scope }) is not null);
        Assert.Null (InstallOptionPresentation.PreviewConflict (null, new () { Architecture = arch, Scope = scope }));
        Assert.Null (InstallOptionPresentation.PreviewConflict (settings, null));
    }

    [Fact]
    public void OptionSummaryExplainsDefaultsAndDistinguishesOverrides ()
    {
        string defaults = InstallOptionPresentation.Describe (null, InstallModePref.Silent);
        Assert.Contains ("Default — Silent", defaults);
        Assert.Contains ("installer chooses scope", defaults);
        string overrides = InstallOptionPresentation.Describe (new ()
        {
            Scope = InstallScopePref.User, Mode = InstallModePref.Interactive, Architecture = InstallArchPref.Arm64, CustomArgs = "/feature"
        }, InstallModePref.Silent);
        Assert.Contains ("User (override)", overrides);
        Assert.Contains ("Interactive (override)", overrides);
        Assert.Contains ("Arm64 (override)", overrides);
        Assert.Contains ("/feature", overrides);
    }

    [Fact]
    public async Task OversizedExportsFailBeforeTouchingTheDestination ()
    {
        string root = Path.Combine (Path.GetTempPath (), "scout-diagnostic-size-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (root);
        try
        {
            string path = Path.Combine (root, "existing.txt");
            File.WriteAllText (path, "original");
            await Assert.ThrowsAsync<InvalidDataException> (() => DiagnosticExport.WriteAsync (path,
                new string ('x', DiagnosticExport.MaxCharacters + 1), true, CancellationToken.None));
            Assert.Equal ("original", File.ReadAllText (path));
            Assert.Single (Directory.GetFiles (root));
        }
        finally { Directory.Delete (root, true); }
    }

    [Fact]
    public async Task RetryingAdvancedOptionsChecksTheSelectedInstallerBeforeApproval ()
    {
        RecordingBackend backend = new () { Preview = new () { Architecture = "arm64" } };
        RunItem item = Failed (settings: new () { Architecture = InstallArchPref.X64 });
        BatchPlan plan = await RetryPlanning.ReviewAsync (backend, [item], CancellationToken.None);
        Assert.Equal (0, plan.Ready);
        Assert.Contains ("Requested X64", Assert.Single (plan.Items).Reason);
        Assert.Equal ("winget", Assert.Single (backend.PreviewSources));
        Assert.Equal (0, backend.Mutations);
        backend.Preview = new () { Architecture = "x64" };
        Assert.Equal (1, (await RetryPlanning.ReviewAsync (backend, [item], CancellationToken.None)).Ready);
        Assert.Equal (0, backend.Mutations);
    }
}
