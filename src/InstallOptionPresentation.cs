namespace WinGetScout;

public static class InstallOptionPresentation
{
    public static string Scope (InstallSettings? settings) => settings?.Scope is { } scope && scope != InstallScopePref.Default
        ? $"{scope} (override)" : "Default — installer chooses scope";
    public static string Mode (InstallSettings? settings, InstallModePref defaultMode) => settings?.Mode is { } mode && mode != InstallModePref.Default
        ? $"{mode} (override)" : defaultMode == InstallModePref.Default
            ? "Default — WinGet decides" : $"Default — {defaultMode} for this backend";
    public static string Architecture (InstallSettings? settings) => settings?.Architecture is { } arch && arch != InstallArchPref.Default
        ? $"{arch} (override)" : "Default — compatible installer chosen by WinGet";

    public static string Describe (InstallSettings? settings, InstallModePref defaultMode) =>
        $"Requested options:\nScope: {Scope (settings)}\nMode: {Mode (settings, defaultMode)}\nArchitecture: {Architecture (settings)}"
        + (string.IsNullOrWhiteSpace (settings?.CustomArgs) ? "\nCustom arguments: none" : $"\nCustom arguments: {settings.CustomArgs}");

    public static string? PreviewConflict (InstallSettings? settings, InstallerPreview? preview)
    {
        if (settings is null || preview is null) return null;
        string? architecture = preview.Architecture?.ToLowerInvariant ();
        if (settings.Architecture != InstallArchPref.Default && architecture is "x64" or "x86" or "arm64" or "arm"
            && !architecture.Equals (settings.Architecture.ToString (), StringComparison.OrdinalIgnoreCase))
            return $"Requested {settings.Architecture}, but the selected installer is {preview.Architecture}. Change the architecture option before installing.";
        string? scope = preview.Scope?.ToLowerInvariant ();
        if (settings.Scope != InstallScopePref.Default && scope is "user" or "machine"
            && !scope.Equals (settings.Scope.ToString (), StringComparison.OrdinalIgnoreCase))
            return $"Requested {settings.Scope} scope, but the selected installer reports {preview.Scope}. Change the scope option before installing.";
        return null;
    }
}
