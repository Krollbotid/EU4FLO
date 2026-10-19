// Parsers/ClimateParser.cs
using System.Text;

namespace EU4FLO.Parsers;

public static class ClimateParser
{
    /// <summary>
    /// Читает map/climate.txt и возвращает множество ID пустошей
    /// из блока impassable = { ... }.
    /// </summary>
    public static HashSet<int> ParseImpassable(string path)
    {
        var result = new HashSet<int>();
        if (!File.Exists(path)) return result;

        var text = File.ReadAllText(path);

        int idx = text.IndexOf("impassable", StringComparison.Ordinal);
        if (idx < 0) return result;

        int open = text.IndexOf('{', idx);
        if (open < 0) return result;

        int close = text.IndexOf('}', open);
        if (close < 0) return result;

        var inner = text.Substring(open + 1, close - open - 1);
        var sb = new StringBuilder();
        foreach (char c in inner)
        {
            if (char.IsDigit(c)) sb.Append(c);
            else if (sb.Length > 0)
            {
                if (int.TryParse(sb.ToString(), out var v)) result.Add(v);
                sb.Clear();
            }
        }
        if (sb.Length > 0 && int.TryParse(sb.ToString(), out var last))
            result.Add(last);

        return result;
    }
}