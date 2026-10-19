// Parsers/AdjacenciesCsvParser.cs
using System.Text;

namespace EU4FLO.Parsers;

public static class AdjacenciesCsvParser
{
    public static List<(int From, int To)> Parse(string path)
    {
        var result = new List<(int, int)>();
        if (!File.Exists(path)) return result;

        var lines = File.ReadAllLines(path, Encoding.UTF8);
        foreach (var raw in lines)
        {
            var line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0) continue;
            if (line.StartsWith("#", StringComparison.Ordinal)) continue;
            if (line.StartsWith("From", StringComparison.OrdinalIgnoreCase)) continue;

            // EU4 использует ';' как разделитель; на всякий случай поддержим и ','.
            var parts = line.Split(new[] { ';', ',' }, StringSplitOptions.None);
            if (parts.Length < 2) continue;
            if (!int.TryParse(parts[0].Trim(), out var from)) continue;
            if (!int.TryParse(parts[1].Trim(), out var to)) continue;
            if (from < 0 || to < 0 || from == to) continue;

            result.Add((from, to));
        }
        return result;
    }
}