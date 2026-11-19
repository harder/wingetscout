namespace WinGetScout;

/// <summary>Cell-aware clipping and literal text at the Markdown presentation boundary.</summary>
internal static class UiText
{
    internal static string TakeColumns (string text, int width)
    {
        if (width <= 0) return string.Empty;
        ReadOnlySpan<char> remaining = text;
        int kept = 0;
        int used = 0;
        while (!remaining.IsEmpty)
        {
            int length = System.Globalization.StringInfo.GetNextTextElementLength (remaining);
            int columns = 0;
            // Match Terminal.Gui's GetColumns: ignore control widths and cap each grapheme at two cells.
            foreach (Rune rune in remaining [..length].EnumerateRunes ())
            {
                columns += Math.Max (0, rune.GetColumns ());
                if (columns >= 2) break;
            }

            used += Math.Min (columns, 2);
            if (used > width) break;
            kept += length;
            remaining = remaining [length..];
        }

        return text [..kept];
    }

    internal static string Ellipsize (string text, int width) => text.GetColumns () <= width ? text
        : width <= 0 ? string.Empty : TakeColumns (text, width - 1) + "…";

    internal static string MarkdownLiteral (string text)
    {
        StringBuilder result = new ();
        foreach (char character in text)
        {
            switch (character)
            {
                case '&': result.Append ("&amp;"); continue;
                case '<': result.Append ("&lt;"); continue;
                case '>': result.Append ("&gt;"); continue;
            }
            // CommonMark permits backslash-escaping every ASCII punctuation character.
            if (character is >= '!' and <= '/' or >= ':' and <= '@' or >= '[' and <= '`' or >= '{' and <= '~')
                result.Append ('\\');
            result.Append (character);
        }
        return result.ToString ();
    }

    internal static string MarkdownDestination (string url) => url.Replace ("&", "&amp;")
        .Replace ("\\", "%5C").Replace ("<", "%3C").Replace (">", "%3E");
}
