// Parsers/DefinitionParser.cs
public static class DefinitionParser
{
    public static List<Province> Parse(string path)
    {
        var result = new List<Province>();
        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(';');
            if (parts.Length < 4) continue;
            if (!int.TryParse(parts[0], out var id)) continue;
            if (!byte.TryParse(parts[1], out var r)) continue;
            if (!byte.TryParse(parts[2], out var g)) continue;
            if (!byte.TryParse(parts[3], out var b)) continue;
    
            string rawName = parts.Length > 4 ? parts[4] : "";
            result.Add(new Province
            {
                Id = id, R = r, G = g, B = b,
                Name = CleanName(rawName)
            });
        }
        return result;
    }
    
    private static string CleanName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        int hash = raw.IndexOf('#');
        if (hash >= 0) raw = raw.Substring(0, hash);
        return raw.TrimEnd('_').Trim();
    }
}