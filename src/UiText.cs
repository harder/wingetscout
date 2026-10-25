namespace WinGetScout;

/// <summary>Cell-aware clipping and literal text at the Markdown presentation boundary.</summary>
internal static class UiText
{
    internal static string TakeColumns (string text, int width)
    {
        if (width <= 0) return string.Empty;
        StringBuilder result = new ();
        int used = 0;
        System.Globalization.TextElementEnumerator elements = System.Globalization.StringInfo.GetTextElementEnumerator (text);
        while (elements.MoveNext ())
        {
            string element = elements.GetTextElement ();
            int columns = element.GetColumns ();
            if (used + columns > width) break;
            result.Append (element);
            used += columns;
        }
        return result.ToString ();
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
