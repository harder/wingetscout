using System.Drawing;
using Terminal.Gui.App;

namespace WinGetScout.Tests;

[Collection ("Terminal UI")]
public sealed class TerminalGuiCompatibilityTests
{
    [Fact]
    public void DetailLinksSurvivePartialRedrawScrollbarHoverAndResize ()
    {
        string? previous = Environment.GetEnvironmentVariable ("DisableRealDriverIO");
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        try
        {
            // Exercise Scout's real Markdown children against the ANSI screen buffer,
            // without connecting the test to the user's terminal or clipboard.
            using IApplication application = Application.Create ().Init (DriverRegistry.Names.ANSI);
            application.Driver!.SetScreenSize (100, 30);
            using Runnable window = new () { Width = Dim.Fill (), Height = Dim.Fill () };
            DetailPanel panel = new () { Width = Dim.Fill (), Height = Dim.Fill () };
            window.Add (panel);
            SessionToken session = application.Begin (window)!;
            try
            {
                panel.SetDetail (new ()
                {
                    Id = "Example.Linked", Name = "Linked package", Version = "1",
                    Homepage = "https://example.invalid/home",
                    ReleaseNotesUrl = "https://example.invalid/releases",
                    Description = string.Join ('\n', Enumerable.Repeat ("Long package description", 80))
                }, loading: false);
                application.LayoutAndDraw ();
                Markdown[] links = panel.SubViews.OfType<Markdown> ().ToArray ();
                Assert.Equal (2, links.Length);
                Assert.Contains ("https://example.invalid/home", application.Driver.ToString ());
                List<Cell> expected = SnapshotLinks (application.Driver, links);

                foreach (Markdown link in links)
                    link.SetNeedsDraw (new Rectangle (0, 0, 1, 1));
                application.LayoutAndDraw ();
                Assert.Equal (expected, SnapshotLinks (application.Driver, links));

                ScrollSlider slider = panel.VerticalScrollBar.Slider;
                Assert.True (slider.Visible);
                application.Mouse.RaiseMouseEvent (new Mouse
                {
                    ScreenPosition = slider.ViewportToScreen (Point.Empty), Flags = MouseFlags.PositionReport
                });
                application.LayoutAndDraw ();
                Assert.True (slider.MouseState.HasFlag (MouseState.In));
                Assert.Equal (expected, SnapshotLinks (application.Driver, links));

                application.Driver.SetScreenSize (64, 24);
                application.LayoutAndDraw ();
                panel.ScrollToEnd ();
                application.LayoutAndDraw ();
                panel.ScrollToStart ();
                application.LayoutAndDraw ();
                Assert.Contains ("https://example.invalid/home", application.Driver.ToString ());
                Assert.Contains ("https://example.invalid/releases", application.Driver.ToString ());
            }
            finally { application.End (session); }
        }
        finally { Environment.SetEnvironmentVariable ("DisableRealDriverIO", previous); }
    }

    private static List<Cell> SnapshotLinks (IDriver driver, IEnumerable<Markdown> links)
    {
        List<Cell> cells = [];
        foreach (Markdown link in links)
        {
            Rectangle area = link.ViewportToScreen (new Rectangle (Point.Empty, link.Viewport.Size));
            for (int y = area.Top; y < area.Bottom; y++)
                for (int x = area.Left; x < area.Right; x++)
                    cells.Add (driver.Contents! [y, x] with { IsDirty = false });
        }
        Assert.NotEmpty (cells);
        return cells;
    }
}
