namespace WinGetScout;

internal interface ITerminalApplication : IDisposable
{
    void Init ();
    void Run (App window);
}

internal sealed class TerminalApplication (IApplication application) : ITerminalApplication
{
    public void Init () => application.Init ();
    public void Run (App window) => application.Run (window);
    public void Dispose () => application.Dispose ();
}

/// <summary>Owns terminal initialization, the UI loop, and independent cleanup attempts.</summary>
internal static class ApplicationLifetime
{
    internal static int Run (Func<ITerminalApplication> createApplication, Func<App> createWindow, TextWriter diagnostics)
    {
        ITerminalApplication? application = null;
        App? window = null;
        List<(string Phase, Exception? Failure)> failures = [];

        try
        {
            // Retain a partially initialized application so Dispose can restore the terminal.
            application = createApplication ();
            application.Init ();
            window = createWindow ();
            application.Run (window);
        }
        catch (Exception ex)
        {
            failures.Add (("Application failed", ex));
        }
        finally
        {
            if (window is not null)
            {
                try
                {
                    // The UI synchronization context stops pumping when Run returns.
                    bool drained = window.ShutdownAsync (TimeSpan.FromSeconds (5))
                                         .ConfigureAwait (false).GetAwaiter ().GetResult ();
                    if (!drained) failures.Add (("Shutdown timed out while waiting for background work", null));
                    foreach (Exception ex in window.BackgroundFailures)
                        failures.Add (("Background work failed", ex));
                    if (window.DroppedBackgroundFailureCount > 0)
                        failures.Add (($"{window.DroppedBackgroundFailureCount} additional background failure(s) were not retained", null));
                }
                catch (Exception ex)
                {
                    failures.Add (("Shutdown failed", ex));
                }

                try { window.Dispose (); }
                catch (Exception ex) { failures.Add (("Window disposal failed", ex)); }
            }

            // Window cleanup cannot bypass application/driver disposal and terminal restoration.
            try { application?.Dispose (); }
            catch (Exception ex) { failures.Add (("Application disposal failed", ex)); }
        }

        // Print after terminal restoration so the UI cannot overwrite the failure. ToString
        // retains the original exception type, stack trace, and inner exceptions.
        foreach ((string phase, Exception? failure) in failures)
            diagnostics.WriteLine (failure is null ? $"{phase}." : $"{phase}: {failure}");

        return failures.Count == 0 ? 0 : 1;
    }
}
