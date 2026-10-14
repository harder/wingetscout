namespace WinGetScout;

/// <summary>Retains navigation keys at list endpoints while allowing Tab to reach dialog actions.</summary>
internal sealed class PackageListView : ListView
{
    protected override bool OnKeyDownNotHandled (Key key)
    {
        if (key.KeyCode is KeyCode.CursorUp or KeyCode.CursorDown
            or KeyCode.PageUp or KeyCode.PageDown or KeyCode.Home or KeyCode.End)
        {
            key.Handled = true;
            return true;
        }

        return base.OnKeyDownNotHandled (key);
    }
}
