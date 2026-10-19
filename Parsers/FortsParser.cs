// Parsers/FortsParser.cs
namespace EU4FLO.Parsers;

public static class FortsParser
{
    public static HashSet<int> Parse(string path)
    {
        var result = new HashSet<int>();
        if (!File.Exists(path)) return result;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            int hash = line.IndexOf('#');
            if (hash == 0) continue;
            if (hash > 0) line = line.Substring(0, hash).Trim();

            if (int.TryParse(line, out var id))
                result.Add(id);
        }
        return result;
    }
}