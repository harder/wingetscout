using System.Runtime.CompilerServices;

namespace WinGetScout.Tests;

[Collection ("Terminal UI")]
public sealed class ApplicationLifetimeTests
{
    [Theory]
    [InlineData (false, false)]
    [InlineData (true, false)]
    [InlineData (false, true)]
    [InlineData (true, true)]
    public void RunAndWindowDisposalFailuresStillAttemptApplicationDisposal (bool failRun, bool failWindow)
    {
        List<string> attempts = [];
        FailureApplication application = new (attempts) { FailRun = failRun };
        App window = new (new MockBackend ());
        window.Add (new DisposalView (attempts, failWindow));
        using StringWriter diagnostics = new AfterDisposalWriter (attempts);

        int result = ApplicationLifetime.Run (() => application, () => window, diagnostics);

        Assert.Equal (new [] { "Init", "Run", "Window.Dispose", "Application.Dispose" }, attempts);
        Assert.Equal (failRun || failWindow ? 1 : 0, result);
        if (failRun)
        {
            Assert.Contains (nameof (ThrowRunFailure), diagnostics.ToString ());
            Assert.Contains ("inner cause", diagnostics.ToString ());
        }
        if (failWindow) Assert.Contains ("window failure", diagnostics.ToString ());
    }

    [Fact]
    public void PartialInitAndApplicationDisposalFailuresAreBothDiagnosable ()
    {
        List<string> attempts = [];
        FailureApplication application = new (attempts) { FailInit = true, FailDispose = true };
        using StringWriter diagnostics = new AfterDisposalWriter (attempts);
        int result = ApplicationLifetime.Run (() => application,
            () => throw new InvalidOperationException ("Window must not be constructed"), diagnostics);

        Assert.Equal (1, result);
        Assert.Equal (new [] { "Init", "Application.Dispose" }, attempts);
        Assert.Contains ("init failure", diagnostics.ToString ());
        Assert.Contains ("application disposal failure", diagnostics.ToString ());
    }

    [MethodImpl (MethodImplOptions.NoInlining)]
    private static void ThrowRunFailure () => throw new InvalidOperationException ("run failure", new Exception ("inner cause"));

    private sealed class FailureApplication (List<string> attempts) : ITerminalApplication
    {
        public bool FailRun { get; set; }
        public bool FailInit { get; set; }
        public bool FailDispose { get; set; }

        public void Init ()
        {
            attempts.Add ("Init");
            if (FailInit) throw new InvalidOperationException ("init failure");
        }

        public void Run (App window)
        {
            attempts.Add ("Run");
            if (FailRun) ThrowRunFailure ();
        }

        public void Dispose ()
        {
            attempts.Add ("Application.Dispose");
            if (FailDispose) throw new InvalidOperationException ("application disposal failure");
        }
    }

    private sealed class DisposalView (List<string> attempts, bool fail) : View
    {
        protected override void Dispose (bool disposing)
        {
            attempts.Add ("Window.Dispose");
            base.Dispose (disposing);
            if (fail) throw new InvalidOperationException ("window failure");
        }
    }

    private sealed class AfterDisposalWriter (List<string> attempts) : StringWriter
    {
        public override void WriteLine (string? value)
        {
            Assert.Contains ("Application.Dispose", attempts);
            base.WriteLine (value);
        }
    }
}
