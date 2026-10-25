using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinGetScout;

/// <summary>Frozen public metadata and version observations; mock sessions never query this machine.</summary>
internal static class MockCatalog
{
    internal static IReadOnlyList<PackageDetail> Packages { get; } = Load ();

    private static PackageDetail [] Load ()
    {
        using Stream stream = typeof (MockCatalog).Assembly.GetManifestResourceStream ("WinGetScout.MockData.catalog.json")
                              ?? throw new InvalidOperationException ("The mock catalog resource is missing.");
        return JsonSerializer.Deserialize (stream, MockCatalogJsonContext.Default.PackageDetailArray)
               ?? throw new InvalidOperationException ("The mock catalog is empty.");
    }
}

[JsonSerializable (typeof (PackageDetail []))]
internal partial class MockCatalogJsonContext : JsonSerializerContext;
