using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinGetScout;

public sealed record SourceDiagnostic (string Name, bool Succeeded, int MatchCount, string? Error);

public sealed record BackendDiagnosticReport (
    DateTimeOffset CheckedAtUtc, string Backend, string Description, string? BackendError,
    string? ComFallbackReason, string? ActiveSource, string? LastViewError, string ProcessArchitecture,
    bool CanVerify, bool CanRepair, IReadOnlyList<SourceDiagnostic> Sources, string? SourceError)
{
    public string DetailText ()
    {
        StringBuilder text = new ($"Checked: {CheckedAtUtc.ToLocalTime ():G}\nBackend: {Backend}\n{Description}\nProcess architecture: {ProcessArchitecture}\n");
        text.AppendLine ($"Verify: {(CanVerify ? "available" : "unavailable")} · Repair: {(CanRepair ? "available" : "unavailable")}");
        text.AppendLine ($"COM fallback: {ComFallbackReason ?? "None recorded"}");
        text.AppendLine ($"Active source filter: {ActiveSource ?? "All"}");
        text.AppendLine ($"Last inventory load error: {LastViewError ?? "None recorded"}");
        if (BackendError is not null) text.AppendLine ($"Backend version query failed: {BackendError}");
        text.AppendLine ("\nSources — read-only catalog query:");
        text.AppendLine ($"Probe: search for {BackendDiagnostics.ProbeId}. Zero matches still means the query completed.");
        if (SourceError is not null) text.AppendLine ($"Source discovery failed: {SourceError}");
        else if (Sources.Count == 0) text.AppendLine ("No configured sources reported.");
        foreach (SourceDiagnostic source in Sources)
            text.AppendLine (source.Succeeded ? $"{source.Name}: query succeeded ({source.MatchCount} matches)"
                : $"{source.Name}: query failed — {source.Error}");
        text.AppendLine ("\nThese checks do not install, upgrade, remove, or repair packages.");
        return text.ToString ();
    }
}

public static class BackendDiagnostics
{
    public const string ProbeId = "Microsoft.PowerToys";

    public static async Task<BackendDiagnosticReport> CaptureAsync (IBackend backend, string? fallback,
        string? activeSource, string? lastViewError, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested ();
        string description = "Version unavailable";
        string? backendError = null, sourceError = null;
        try { description = DiagnosticText.Clean (await backend.DescribeAsync (ct), 1024); }
        catch (Exception ex) when (!ct.IsCancellationRequested) { backendError = RunRecord.CompactReason (ex.Message); }
        ct.ThrowIfCancellationRequested ();
        SourceDiagnostic [] sources = [];
        try
        {
            IReadOnlyList<string> names = await backend.ListSourcesAsync (ct);
            if (names.Count > BackendLimits.Catalogs || names.Any (name => BackendLimits.ExactIdentity (name) is null)
                || names.Sum (name => (long)name.Length) > BackendLimits.SourceResultCharacters)
                throw new InvalidDataException ("Source discovery exceeded safe limits or returned an invalid name.");
            string [] unique = names.Distinct (StringComparer.OrdinalIgnoreCase).ToArray ();
            sources = new SourceDiagnostic [unique.Length];
            await Parallel.ForEachAsync (Enumerable.Range (0, unique.Length),
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (index, token) =>
                {
                    string source = unique [index];
                    try
                    {
                        IReadOnlyList<Package> matches = await backend.SearchAsync (ProbeId, source, token);
                        token.ThrowIfCancellationRequested ();
                        sources [index] = new (source, true, matches.Count, null);
                    }
                    catch (Exception ex) when (!token.IsCancellationRequested)
                    {
                        sources [index] = new (source, false, 0, RunRecord.CompactReason (ex.Message));
                    }
                });
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            sourceError = RunRecord.CompactReason (ex.Message);
            sources = [];
        }
        ct.ThrowIfCancellationRequested ();
        return new (DateTimeOffset.UtcNow, backend.GetType ().Name, description, backendError,
            fallback is null ? null : DiagnosticText.Clean (fallback, 1024), activeSource,
            lastViewError is null ? null : RunRecord.CompactReason (lastViewError),
            RuntimeInformation.ProcessArchitecture.ToString (), backend.CanVerify, backend.CanRepair, sources, sourceError);
    }
}

public sealed record DiagnosticDocument (string Title, string Text, string Json)
{
    public static DiagnosticDocument ForRun (RunItem item) => new ("Run diagnostics", item.DetailText (),
        JsonSerializer.Serialize (item with { Diagnostics = DiagnosticText.Clean (item.Diagnostics ?? item.Reason) }, DiagnosticsJsonContext.Default.RunItem));
    public static DiagnosticDocument ForBackend (BackendDiagnosticReport report) => new ("Backend and source diagnostics", report.DetailText (),
        JsonSerializer.Serialize (report, DiagnosticsJsonContext.Default.BackendDiagnosticReport));
}

[JsonSourceGenerationOptions (WriteIndented = true)]
[JsonSerializable (typeof (RunItem))]
[JsonSerializable (typeof (BackendDiagnosticReport))]
internal partial class DiagnosticsJsonContext : JsonSerializerContext;

internal static class DiagnosticExport
{
    internal const int MaxCharacters = 2 * 1024 * 1024;

    internal static async Task WriteAsync (string path, string content, bool overwrite, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested ();
        if (content.Length > MaxCharacters) throw new InvalidDataException ("Diagnostics exceed the export size limit.");
        string destination = Path.GetFullPath (path);
        string temporary = Path.Combine (Path.GetDirectoryName (destination)!, $".{Path.GetFileName (destination)}.{Guid.NewGuid ():N}.tmp");
        Exception? primaryFailure = null;
        try
        {
            await using (FileStream stream = new (temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (StreamWriter writer = new (stream, new UTF8Encoding (false)))
            {
                await writer.WriteAsync (content.AsMemory (), ct);
                await writer.FlushAsync (ct);
                stream.Flush (true);
            }
            ct.ThrowIfCancellationRequested ();
            File.Move (temporary, destination, overwrite);
        }
        catch (Exception ex) { primaryFailure = ex; throw; }
        finally
        {
            try { if (File.Exists (temporary)) File.Delete (temporary); }
            catch when (primaryFailure is not null) { }
        }
    }
}
