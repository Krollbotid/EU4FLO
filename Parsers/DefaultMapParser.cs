// Parsers/DefaultMapParser.cs
using System.Text;

namespace EU4FLO.Parsers;

public static class DefaultMapParser
{
    /// <summary>
    /// Возвращает множество ID морских и озёрных провинций из default.map.
    /// </summary>
    public static HashSet<int> ParseSeaProvinces(string path)
    {
        var result = new HashSet<int>();
        var text = File.ReadAllText(path);
        ParseList(text, "sea_starts", result);
        ParseList(text, "lakes", result);
        return result;
    }

    private static void ParseList(string text, string key, HashSet<int> output)
    {
        int idx = text.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return;
        int open = text.IndexOf('{', idx);
        if (open < 0) return;
        int close = text.IndexOf('}', open);
        if (close < 0) return;

        var inner = text.Substring(open + 1, close - open - 1);
        var sb = new StringBuilder();
        foreach (char c in inner)
        {
            if (char.IsDigit(c)) sb.Append(c);
            else if (sb.Length > 0)
            {
                if (int.TryParse(sb.ToString(), out var v)) output.Add(v);
                sb.Clear();
            }
        }
        if (sb.Length > 0 && int.TryParse(sb.ToString(), out var last))
            output.Add(last);
    }
}