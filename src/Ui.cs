// Widget views and modal dialogs

namespace WinGetScout;

/// <summary>Clear resize guidance instead of overlapping two-pane content in a small terminal.</summary>
public sealed class TerminalSizeGuardView : FrameView
{
    public const int MinimumWidth = 80;
    public const int MinimumHeight = 24;

    private readonly Label _message;

    public TerminalSizeGuardView ()
    {
        X = 0;
        Y = 0;
        Width = Dim.Fill ();
        Height = Dim.Fill ();
        BorderStyle = LineStyle.None;
        SchemeName = Theme.AppSchemeName;
        CanFocus = false;
        Visible = false;
        _message = new ()
        {
            X = Pos.Center (),
            Y = Pos.Center (),
            TextAlignment = Alignment.Center,
            Text = BuildMessage (MinimumWidth, MinimumHeight)
        };
        Add (_message);
    }

    public void UpdateForSize (int width, int height)
    {
        Visible = IsTooSmall (width, height);

        if (Visible)
        {
            _message.Text = BuildMessage (width, height);
            SetNeedsDraw ();
        }
    }

    public static bool IsTooSmall (int width, int height) =>
        width > 0 && height > 0 && (width < MinimumWidth || height < MinimumHeight);

    public static string BuildMessage (int width, int height) =>
        $"Terminal too small ({width}×{height})\nResize to at least {MinimumWidth}×{MinimumHeight}\nCtrl+Q quits";
}

/// <summary>
/// Compact, right-aligned top navigation with mouse hit-testing.
/// </summary>
public sealed class TabBar : View
{
    private static readonly (AppMode Mode, string Label) [] _tabs =
    [
        (AppMode.Search, "◇ Search"),
        (AppMode.Installed, "▣ Installed"),
        (AppMode.Upgrades, "△ Upgrades")
    ];

    private AppMode _active = AppMode.Installed;

    public TabBar ()
    {
        CanFocus = false;
        Height = 1;
        SchemeName = Theme.NavbarInactiveSchemeName;
    }

    public AppMode Active
    {
        get => _active;
        set
        {
            if (_active == value)
            {
                return;
            }

            _active = value;
            SetNeedsDraw ();
        }
    }

    public event EventHandler<AppMode>? TabClicked;

    private static int TabsWidth => _tabs.Sum (tab => tab.Label.Length + 2) + _tabs.Length - 1;

    private static int StartX (int width) => Math.Max (0, width - TabsWidth - 1);

    /// <inheritdoc />
    protected override bool OnDrawingContent (DrawContext? context)
    {
        SetAttribute (new (Theme.AccentDim, Theme.Bg));

        for (int x = 0; x < Viewport.Width; x++)
        {
            Move (x, 0);
            AddStr (" ");
        }

        int cursor = StartX (Viewport.Width);

        foreach ((AppMode mode, string label) in _tabs)
        {
            string padded = $" {label} ";

            if (cursor + padded.Length > Viewport.Width)
            {
                break;
            }

            Attribute attr = mode == _active
                                 ? new Attribute (Theme.Accent, Theme.Bg, TextStyle.Bold)
                                 : new Attribute (Theme.AccentDim, Theme.Bg);
            SetAttribute (attr);
            Move (cursor, 0);
            AddStr (padded);
            cursor += padded.Length + 1;
        }

        return true;
    }

    /// <inheritdoc />
    protected override bool OnMouseEvent (Mouse mouse)
    {
        if (!mouse.IsSingleClicked || mouse.Position is null)
        {
            return false;
        }

        int x = mouse.Position.Value.X;
        int cursor = StartX (Viewport.Width);

        foreach ((AppMode mode, string label) in _tabs)
        {
            string padded = $" {label} ";

            if (x >= cursor && x < cursor + padded.Length)
            {
                Active = mode;
                TabClicked?.Invoke (this, mode);
                mouse.Handled = true;

                return true;
            }

            cursor += padded.Length + 1;
        }

        return false;
    }
}

/// <summary>
/// Two footer rows keep operation outcomes and progress separate from filters and shortcuts.
/// </summary>
public sealed class StatusBar : View
{
    public StatusBar ()
    {
        Height = 2;
        CanFocus = false;
        SchemeName = Theme.StatusSchemeName;
    }

    public AppMode Mode { get; set; } = AppMode.Installed;
    public InputMode InputMode { get; set; } = InputMode.Normal;
    public string? SourceFilter { get; set; }
    public PinFilter PinFilter { get; set; } = PinFilter.All;
    public string Message { get; set; } = string.Empty;
    public bool IsError { get; set; }
    public bool IsLoading { get; set; }
    public int Tick { get; set; }
    public int InstallSelectionCount { get; set; }
    public int UpgradeSelectionCount { get; set; }
    public int HiddenSelectionCount { get; set; }

    /// <summary>When set, a determinate progress bar replaces the spinner (install/upgrade/uninstall).</summary>
    public OpProgress? Op { get; set; }

    private static readonly char [] _spinner = ['⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏'];
    internal static char SpinnerGlyph (int tick) => _spinner [tick % _spinner.Length];
    private static readonly string [] _searchInputHints = ["Esc Close", "Enter Search", "Ctrl+U Clear", "←→ Tabs"];
    private static readonly string [] _filterInputHints = ["Esc Clear", "Enter Done", "Ctrl+U Clear", "Bksp Del", "←→ Tabs"];
    private static readonly string [] _versionInputHints = ["Esc Cancel", "Enter Confirm", "Ctrl+U Clear"];
    private static readonly string [] _searchHints = ["/ Search", "f Source", "r Refresh", "Spc Select", "g Sets", "B Install sel", "L Runs", "m Actions", "←→ Tabs", "? Help", "q Quit"];
    private static readonly string [] _installedHints = ["/ Filter", "f Source", "p Pin", "P Pins", "r Refresh", "L Runs", "m Actions", "←→ Tabs", "? Help", "q Quit"];
    private static readonly string [] _upgradeHints = ["/ Filter", "f Source", "p Pin", "P Pins", "r Refresh", "Spc Select", "U Upgrade sel", "C Checks", "L Runs", "m Actions", "←→ Tabs", "? Help", "q Quit"];

    /// <summary>Render a compact fixed-width progress bar like <c>▕████░░░░░░▏  42%</c>.</summary>
    private static string RenderBar (double fraction)
    {
        const int width = 10;
        fraction = Math.Clamp (fraction, 0, 1);
        int filled = (int)Math.Round (fraction * width);

        return $"▕{new string ('█', filled)}{new string ('░', width - filled)}▏ {fraction * 100,3:0}%";
    }

    /// <inheritdoc />
    protected override bool OnDrawingContent (DrawContext? context)
    {
        SetAttribute (new (Theme.TextPrimary, Theme.Surface));

        for (int y = 0; y < Viewport.Height; y++)
        {
            Move (0, y);
            AddStr (new string (' ', Viewport.Width));
        }

        string msg = ComposeMessage (Viewport.Width);
        Attribute msgAttr = IsError
            ? new (Theme.Danger, Theme.Surface, TextStyle.Bold)
            : IsLoading || Op is not null
                ? new (Theme.Accent, Theme.Surface)
                : new (Theme.TextPrimary, Theme.Surface);
        SetAttribute (msgAttr);
        Move (0, 0);
        AddStr (msg);

        int x0 = 0;

        // Source filter badge
        string srcLabel = AppState.SourceLabel (SourceFilter);
        Attribute srcAttr = SourceFilter switch
        {
            "winget" => new (Theme.TextOnAccent, Theme.Info, TextStyle.Bold),
            "msstore" => new (Theme.TextOnAccent, Theme.Accent, TextStyle.Bold),

            // null = "All", and any custom source, share the neutral badge.
            _ => new (Theme.TextOnAccent, Theme.TextSecondary, TextStyle.Bold)
        };
        SetAttribute (srcAttr);
        Move (x0, 1);
        AddStr (srcLabel);
        x0 += srcLabel.Length + 1;

        // Pin filter badge (skip in Search mode)
        if (Mode != AppMode.Search)
        {
            string pinLabel = AppState.PinLabel (PinFilter);
            Attribute pinAttr = PinFilter switch
            {
                PinFilter.PinnedOnly => new (Theme.TextOnAccent, Theme.Selection, TextStyle.Bold),
                PinFilter.UnpinnedOnly => new (Theme.TextOnAccent, Theme.TextSecondary, TextStyle.Bold),
                _ => new (Theme.TextOnAccent, Theme.AccentDim, TextStyle.Bold)
            };
            SetAttribute (pinAttr);
            Move (x0, 1);
            AddStr (pinLabel);
            x0 += pinLabel.Length + 1;
        }

        int selected = Mode == AppMode.Upgrades ? UpgradeSelectionCount : Mode == AppMode.Search ? InstallSelectionCount : 0;
        string selection = selected > 0
            ? $" {selected} selected{(HiddenSelectionCount > 0 ? $" ({HiddenSelectionCount} hidden)" : string.Empty)} " : string.Empty;
        if (selection.Length > 0 && x0 + selection.Length + 1 < Viewport.Width)
        {
            SetAttribute (new (Theme.TextOnAccent, Theme.Selection, TextStyle.Bold));
            Move (x0, 1);
            AddStr (selection);
            x0 += selection.Length + 1;
        }

        // The selection's primary action sits at the right end, so it survives truncation.
        string [] hintPairs = ComposeHintPairs ();
        int hintsAvailable = Viewport.Width - x0 - 1;
        (int firstHint, bool elided) = TruncateHintPairs (hintPairs, hintsAvailable);
        int hintsWidth = HintsWidth (hintPairs, firstHint, elided);
        int hintsStart = Math.Max (x0 + 1, Viewport.Width - hintsWidth);

        DrawHintPairs (hintsStart, hintPairs, firstHint, elided);

        return true;
    }

    internal string ComposeMessage (int width)
    {
        string msg = Message;

        if (Op is { } op)
        {
            msg = $"{RenderBar (op.Fraction)} {op.Label}  {msg}";
        }
        else if (IsLoading)
        {
            char spin = SpinnerGlyph (Tick);
            msg = $"{spin} {msg}";
        }

        return StatusOwnership.TruncateScalarSafe (msg, Math.Max (1, width));
    }

    private void DrawHintPairs (int xStart, string [] pairs, int first, bool elided)
    {
        Attribute primary = new (Theme.TextPrimary, Theme.Surface);
        Attribute dim = new (Theme.TextSecondary, Theme.Surface);
        int x = xStart;

        if (elided)
        {
            SetAttribute (dim);
            Move (x, 1);
            AddStr ("… ");
            x += 2;
        }

        for (int i = first; i < pairs.Length; i++)
        {
            SetAttribute (primary);
            Move (x, 1);
            AddStr (pairs [i]);
            x += pairs [i].Length;

            if (i < pairs.Length - 1)
            {
                SetAttribute (dim);
                Move (x, 1);
                AddStr (" │ ");
                x += 3;
            }
        }
    }

    private static int HintsWidth (string [] pairs, int first, bool elided)
    {
        if (first >= pairs.Length)
        {
            return elided ? 1 : 0;
        }

        int width = (elided ? 2 : 0) + 3 * (pairs.Length - first - 1);

        for (int i = first; i < pairs.Length; i++)
        {
            width += pairs [i].Length;
        }

        return width;
    }

    /// <summary>
    /// Drop hint pairs from the left (lowest priority first; the right end has q Quit / ? Help)
    /// until what's left fits in <paramref name="available"/> columns including the " │ "
    /// separators between pairs and a leading "… " when anything was elided.
    /// </summary>
    private static (int First, bool Elided) TruncateHintPairs (string [] pairs, int available)
    {
        if (pairs.Length == 0 || available <= 0)
        {
            return (pairs.Length, false);
        }

        if (HintsWidth (pairs, 0, false) <= available)
        {
            return (0, false);
        }

        for (int start = 1; start < pairs.Length; start++)
        {
            if (HintsWidth (pairs, start, true) <= available)
            {
                return (start, true);
            }
        }

        return (pairs.Length, true);
    }

    private string [] ComposeHintPairs ()
    {
        if (InputMode == InputMode.Normal)
        {
            if (Mode == AppMode.Upgrades && UpgradeSelectionCount > 0)
                return ["/ Filter", "a Toggle visible", "Spc Select", "L Runs", "? Help", "Esc Clear sel", "m Actions", $"u Upgrade {UpgradeSelectionCount}"];
            if (Mode == AppMode.Search && InstallSelectionCount > 0)
                return ["/ Search", "a Toggle visible", "Spc Select", "L Runs", "? Help", "Esc Clear sel", "m Actions", $"i Install {InstallSelectionCount}"];
        }
        return InputMode switch
        {
            InputMode.Search => _searchInputHints,
            InputMode.LocalFilter => _filterInputHints,
            InputMode.VersionInput => _versionInputHints,
            _ => Mode switch
            {
                AppMode.Search => _searchHints,

                AppMode.Upgrades => _upgradeHints,
                _ => _installedHints
            }
        };
    }
}
/// <summary>
/// Modal dialog asking for a specific version string before install.
/// Returns the string via <see cref="Runnable{TResult}.Result"/>; null if cancelled.
/// </summary>
public sealed class VersionInputDialog : Runnable<string?>
{
    private readonly TextField _input;

    public VersionInputDialog (string packageName)
    {
        Title = $" Install specific version of {packageName} ";
        BorderStyle = LineStyle.Rounded;
        Width = 60;
        Height = 8;
        X = Pos.Center ();
        Y = Pos.Center ();
        SchemeName = Theme.SurfaceSchemeName;
        Arrangement = ViewArrangement.Movable;

        Label prompt = new () { X = 1, Y = 1, Text = "Version:" };
        _input = new () { X = Pos.Right (prompt) + 1, Y = 1, Width = Dim.Fill (1) };
        Label error = new () { X = 1, Y = 2, Width = Dim.Fill (1), SchemeName = Theme.DangerSchemeName };
        _input.KeyDown += (_, key) =>
                          {
                              if (key.KeyCode == (KeyCode.U | KeyCode.CtrlMask))
                              {
                                  _input.Text = string.Empty;
                                  key.Handled = true;
                              }
                          };

        Button confirm = new () { X = Pos.Center () - 8, Y = 4, Text = "_Confirm", IsDefault = true };
        Button cancel = new () { X = Pos.Right (confirm) + 2, Y = 4, Text = "Cancel" };

        confirm.Accepting += (_, e) =>
                             {
                                 e.Handled = true;
                                 string version = _input.Text?.Trim () ?? string.Empty;
                                 if (version.Length == 0)
                                 {
                                     error.Text = "Enter a version, or Cancel to go back.";
                                     _input.SetFocus ();
                                     return;
                                 }
                                 Result = version;
                                 RequestStop ();
                             };

        cancel.Accepting += (_, e) =>
                            {
                                Result = null;
                                RequestStop ();
                                e.Handled = true;
                            };

        Add (prompt, _input, error, confirm, cancel);
        _input.SetFocus ();
    }
}

/// <summary>
/// Modal that lets the user pick a real version from a list (newest first), used when the backend
/// can enumerate available versions (the COM backend). Falls back to <see cref="VersionInputDialog"/>
/// when no versions are available (e.g. the CLI backend). Result is the chosen version, or null on cancel.
/// </summary>
public sealed class VersionPickerDialog : Runnable<string?>
{
    public VersionPickerDialog (string packageName, IReadOnlyList<string> versions)
    {
        Title = $" Select version of {packageName} ";
        BorderStyle = LineStyle.Rounded;
        Width = 60;
        Height = Math.Clamp (versions.Count + 6, 10, 22);
        X = Pos.Center ();
        Y = Pos.Center ();
        SchemeName = Theme.SurfaceSchemeName;
        Arrangement = ViewArrangement.Movable;

        Label prompt = new () { X = 1, Y = 0, Text = "Pick a version (newest first):" };

        ListView list = new PackageListView ()
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill (1),
            Height = Dim.Fill (2),
            SchemeName = Theme.SurfaceSchemeName
        };
        list.SetSource (new ObservableCollection<string> (versions));
        list.SelectedItem = versions.Count > 0 ? 0 : null;

        Button install = new () { X = Pos.Center () - 8, Y = Pos.AnchorEnd (1), Text = "_Install", IsDefault = true };
        Button cancel = new () { X = Pos.Right (install) + 2, Y = Pos.AnchorEnd (1), Text = "Cancel" };

        install.Accepting += (_, e) =>
                             {
                                 int idx = list.SelectedItem ?? -1;
                                 Result = idx >= 0 && idx < versions.Count ? versions [idx] : null;
                                 RequestStop ();
                                 e.Handled = true;
                             };

        cancel.Accepting += (_, e) =>
                            {
                                Result = null;
                                RequestStop ();
                                e.Handled = true;
                            };

        Add (prompt, list, install, cancel);
        list.SetFocus ();
    }
}

/// <summary>
/// Theme picker: lets the user switch among <see cref="Theme.Palettes"/> at runtime. Result is
/// the chosen palette's <c>Id</c> (e.g. "sage"), or null on cancel.
/// </summary>
public sealed class ThemePickerDialog : Runnable<string?>
{
    public ThemePickerDialog ()
    {
        Title = " Select theme ";
        BorderStyle = LineStyle.Rounded;
        Width = 40;
        Height = Math.Clamp (Theme.Palettes.Count + 6, 10, 14);
        X = Pos.Center ();
        Y = Pos.Center ();
        SchemeName = Theme.SurfaceSchemeName;
        Arrangement = ViewArrangement.Movable;

        Label prompt = new () { X = 1, Y = 0, Text = "Pick a theme:" };

        ListView list = new ()
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill (1),
            Height = Dim.Fill (2),
            SchemeName = Theme.SurfaceSchemeName
        };
        List<string> names = [.. Theme.Palettes.Select (p => p.DisplayName)];
        list.SetSource (new ObservableCollection<string> (names));

        int currentIndex = 0;

        for (int i = 0; i < Theme.Palettes.Count; i++)
        {
            if (Theme.Palettes [i].Id == Theme.CurrentPaletteName)
            {
                currentIndex = i;

                break;
            }
        }

        list.SelectedItem = currentIndex;

        Button apply = new () { X = Pos.Center () - 8, Y = Pos.AnchorEnd (1), Text = "_Apply", IsDefault = true };
        Button cancel = new () { X = Pos.Right (apply) + 2, Y = Pos.AnchorEnd (1), Text = "Cancel" };

        apply.Accepting += (_, e) =>
                           {
                               int idx = list.SelectedItem ?? -1;
                               Result = idx >= 0 && idx < Theme.Palettes.Count ? Theme.Palettes [idx].Id : null;
                               RequestStop ();
                               e.Handled = true;
                           };

        cancel.Accepting += (_, e) =>
                            {
                                Result = null;
                                RequestStop ();
                                e.Handled = true;
                            };

        Add (prompt, list, apply, cancel);
        list.SetFocus ();
    }
}

/// <summary>
/// Advanced install options panel: install scope, mode, architecture, and custom installer args.
/// Result is the chosen <see cref="InstallSettings"/>, or null on cancel. The COM backend maps
/// these onto InstallOptions; the CLI backend onto winget flags. Each OptionSelector's index lines
/// up with the corresponding enum's member order.
/// </summary>
public sealed class AdvancedInstallDialog : Runnable<InstallSettings?>
{
    public AdvancedInstallDialog (string packageName, InstallModePref defaultMode = InstallModePref.Default)
    {
        Title = $" Advanced install: {packageName} ";
        BorderStyle = LineStyle.Rounded;
        Width = 74;
        Height = 20;
        X = Pos.Center ();
        Y = Pos.Center ();
        SchemeName = Theme.SurfaceSchemeName;
        Arrangement = ViewArrangement.Movable;

        Label explanation = new () { X = 1, Y = 0, Width = Dim.Fill (1), Text = "Choose overrides or keep the defaults explained below." };
        Label scopeLabel = new () { X = 1, Y = 2, Text = "Scope: " };
        OptionSelector scope = new ()
        {
            X = Pos.Right (scopeLabel), Y = 2, Width = Dim.Fill (1),
            Orientation = Orientation.Horizontal,
            Labels = ["Default", "User", "Machine"]
        };
        scope.Value = 0;

        Label scopeHint = new () { X = 1, Y = 3, Width = Dim.Fill (1) };
        Label modeLabel = new () { X = 1, Y = 5, Text = "Mode:  " };
        OptionSelector mode = new ()
        {
            X = Pos.Right (modeLabel), Y = 5, Width = Dim.Fill (1),
            Orientation = Orientation.Horizontal,
            Labels = ["Default", "Silent", "Interactive"]
        };
        mode.Value = 0;

        Label modeHint = new () { X = 1, Y = 6, Width = Dim.Fill (1) };
        Label archLabel = new () { X = 1, Y = 8, Text = "Arch:  " };
        OptionSelector arch = new ()
        {
            X = Pos.Right (archLabel), Y = 8, Width = Dim.Fill (1),
            Orientation = Orientation.Horizontal,
            Labels = ["Default", "x64", "x86", "arm64"]
        };
        arch.Value = 0;

        Label archHint = new () { X = 1, Y = 9, Width = Dim.Fill (1) };
        Label argsLabel = new () { X = 1, Y = 11, Text = "Custom installer args (optional override):" };
        TextField argsField = new () { X = 1, Y = 12, Width = Dim.Fill (1) };
        Label reviewHint = new () { X = 1, Y = 14, Width = Dim.Fill (1), Text = "Next: review requested options and the installer, then confirm." };

        void UpdateHints ()
        {
            InstallSettings selected = new ()
            {
                Scope = (InstallScopePref)(scope.Value ?? 0), Mode = (InstallModePref)(mode.Value ?? 0),
                Architecture = (InstallArchPref)(arch.Value ?? 0)
            };
            scopeHint.Text = InstallOptionPresentation.Scope (selected);
            modeHint.Text = InstallOptionPresentation.Mode (selected, defaultMode);
            archHint.Text = InstallOptionPresentation.Architecture (selected);
        }
        scope.ValueChanged += (_, _) => UpdateHints ();
        mode.ValueChanged += (_, _) => UpdateHints ();
        arch.ValueChanged += (_, _) => UpdateHints ();
        UpdateHints ();

        Button install = new () { X = Pos.Center () - 8, Y = Pos.AnchorEnd (1), Text = "_Review…", IsDefault = true };
        Button cancel = new () { X = Pos.Right (install) + 2, Y = Pos.AnchorEnd (1), Text = "Cancel" };

        install.Accepting += (_, e) =>
                             {
                                 Result = new InstallSettings
                                 {
                                     Scope = (InstallScopePref)(scope.Value ?? 0),
                                     Mode = (InstallModePref)(mode.Value ?? 0),
                                     Architecture = (InstallArchPref)(arch.Value ?? 0),
                                     CustomArgs = string.IsNullOrWhiteSpace (argsField.Text) ? null : argsField.Text
                                 };
                                 RequestStop ();
                                 e.Handled = true;
                             };

        cancel.Accepting += (_, e) =>
                            {
                                Result = null;
                                RequestStop ();
                                e.Handled = true;
                            };

        Add (explanation, scopeLabel, scope, scopeHint, modeLabel, mode, modeHint, archLabel, arch, archHint,
            argsLabel, argsField, reviewHint, install, cancel);
        scope.SetFocus ();
    }
}

/// <summary>
/// Help overlay shown by pressing <c>?</c>. Mirrors the contents from src/ui.rs::render_help.
/// </summary>
public sealed class HelpDialog : Runnable
{
    public HelpDialog (string backendDescription = "", string? comFallbackReason = null)
    {
        Title = " Help ";
        BorderStyle = LineStyle.Rounded;
        Width = Dim.Percent (90);
        Height = Dim.Percent (85);
        X = Pos.Center ();
        Y = Pos.Center ();
        SchemeName = Theme.SurfaceSchemeName;
        Arrangement = ViewArrangement.Movable;

        // Lead with which backend is live so it's discoverable that the COM build can fall back
        // to the CLI. Empty until resolved → omit the line entirely.
        string header = string.IsNullOrEmpty (backendDescription)
                            ? string.Empty
                            : $"Backend: {backendDescription}\n\n";

        // If a COM backend was requested but activation failed, the app silently fell back to CLI
        // (the stderr note is overdrawn by the TUI). Surface the reason here so it's discoverable.
        if (!string.IsNullOrEmpty (comFallbackReason))
        {
            header += $"COM unavailable: {comFallbackReason} — using CLI.\n\n";
        }

        Markdown content = new ()
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill (1),
            Height = Dim.Fill (1),
            Text = header + HelpText,
            CanFocus = true,
            UseThemeBackground = false,
            ShowHeadingPrefix = false
        };
        content.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Always;
        content.HorizontalScrollBar.VisibilityMode = ScrollBarVisibilityMode.None;
        ReadOnlyTextNavigation.Configure (content);
        Add (content);

        Button close = new ()
        {
            X = Pos.Center (),
            Y = Pos.AnchorEnd (1),
            Text = "_Close",
            IsDefault = true
        };
        close.Accepting += (_, _) => RequestStop ();
        Add (close);
        content.SetFocus ();

        KeyDown += (_, key) =>
                   {
                       if (key.KeyCode is KeyCode.Esc or KeyCode.Q)
                       {
                           RequestStop ();
                           key.Handled = true;
                       }

                       if (key.AsRune.Value == '?')
                       {
                           RequestStop ();
                           key.Handled = true;
                       }
                   };
    }

    private const string HelpText =
        """
        ## Navigation
        - **↑ / k**, **↓ / j** — Move through rows or scroll Details.
        - **PgUp / PgDn**, **Home / End** — Page or jump to either end.
        - **← / →**, **1 / 2 / 3** — Switch Search, Installed, Upgrades tabs.
        - **Tab / Shift+Tab** — Move focus between the list and Details.
        - **/ or s** — Search the catalog, or filter Installed/Upgrades.
        - **f** — Cycle sources. **r** — Refresh.

        ## Package actions
        - **m / Shift+F10** — Open all actions for this package and selection.
        - **i** — Install the row or review marked Search packages.
        - **u** — Upgrade the row or review marked Upgrades packages.
        - **I** — Choose or enter a version. **A** — Advanced install options.
        - **d** — Download an installer without installing it.
        - **x** — Uninstall. **p** — Pin or unpin.
        - **V / R** — Verify or repair, when supported.
        - **o / c** — Open the homepage or release notes.

        ## Marked packages
        - **Space** — Toggle the row's mark in Search or Upgrades.
        - **a** — Toggle marks on visible rows; hidden marks are kept.
        - **B / U** — Review marked installs or upgrades before approving.
        - **Esc** — Clear all marks, including hidden packages.
        - **g** — Save, load, or delete a package set in Search.

        ## Other controls
        - **D** — Backend/source diagnostics. **L** — Recent runs and retries.
        - **C** — Scheduled update checks in Upgrades.
        - **e** — Export visible rows to CSV. **P** — Cycle the pin filter.
        - **S** — Cycle sorting. **t** — Choose a theme.
        - **?** — Toggle Help. Use arrows or Page Up/Down to scroll it.
        - **Esc** — Cancel pending work, clear a filter, or close a dialog.
        - **q / Ctrl+Q** — Quit. Ctrl+Q also works in fields and dialogs.

        ## Mouse
        Click a tab to switch views, click a row to inspect it, right-click for
        actions, and use the wheel to scroll the list or Details.
        """;
}

/// <summary>Keyboard scrolling for read-only text views, which do not bind navigation themselves.</summary>
internal static class ReadOnlyTextNavigation
{
    internal static void Configure (View text)
    {
        text.KeyDown += (_, key) =>
        {
            int page = Math.Max (1, text.Viewport.Height - 1);
            switch (key.KeyCode)
            {
                case KeyCode.CursorDown: text.ScrollVertical (1); break;
                case KeyCode.CursorUp: text.ScrollVertical (-1); break;
                case KeyCode.PageDown: text.ScrollVertical (page); break;
                case KeyCode.PageUp: text.ScrollVertical (-page); break;
                case KeyCode.Home: text.ScrollVertical (-text.Viewport.Y); break;
                case KeyCode.End: text.ScrollVertical (text.GetContentSize ().Height); break;
                case KeyCode.CursorLeft: text.ScrollHorizontal (-1); break;
                case KeyCode.CursorRight: text.ScrollHorizontal (1); break;
                default: return;
            }
            key.Handled = true;
        };
    }
}
