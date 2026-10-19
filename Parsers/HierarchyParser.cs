// Parsers/HierarchyParser.cs
using System.Text;

namespace EU4FLO.Parsers;

public static class HierarchyParser
{
    private sealed class Node
    {
        public string? Key;              // имя блока ("ruined_isles_region", "areas", ...)
        public string? Value;            // значение, если это лист ("mykx_isles_area", "1234", ...)
        public List<Node> Children = new();
    }

    // --- Токенизация с поддержкой кавычек и комментариев ---
    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        bool inComment = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inComment)
            {
                if (c == '\n' || c == '\r') inComment = false;
                continue;
            }
            if (inQuotes)
            {
                if (c == '"') { inQuotes = false; continue; }
                sb.Append(c);
                continue;
            }
            if (c == '#') { inComment = true; continue; }
            if (c == '"') { inQuotes = true; continue; }

            if (char.IsWhiteSpace(c))
            {
                if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
            }
            else if (c == '{' || c == '}' || c == '=')
            {
                if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
                tokens.Add(c.ToString());
            }
            else sb.Append(c);
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens;
    }

    // --- Рекурсивный разбор блока: последовательность key = value / key = { ... } / bare-token ---
    private static List<Node> ParseBlock(List<string> tokens, ref int pos)
    {
        var nodes = new List<Node>();
        while (pos < tokens.Count)
        {
            string tok = tokens[pos];

            if (tok == "}") { pos++; return nodes; }

            // key = ...
            if (pos + 1 < tokens.Count && tokens[pos + 1] == "=")
            {
                string name = tok;
                pos += 2;

                if (pos < tokens.Count && tokens[pos] == "{")
                {
                    pos++; // съесть '{'
                    var node = new Node { Key = name };
                    node.Children = ParseBlock(tokens, ref pos);
                    nodes.Add(node);
                }
                else
                {
                    var val = pos < tokens.Count ? tokens[pos] : "";
                    pos++;
                    nodes.Add(new Node { Key = name, Value = val });
                }
            }
            else if (tok == "{")
            {
                // анонимный блок (редко, но бывает)
                pos++;
                var node = new Node();
                node.Children = ParseBlock(tokens, ref pos);
                nodes.Add(node);
            }
            else
            {
                // голый токен: id провинции, имя области/региона, флаг restrict_charter и т.п.
                nodes.Add(new Node { Value = tok });
                pos++;
            }
        }
        return nodes;
    }

    private static List<Node> Parse(string text)
    {
        var tokens = Tokenize(text);
        int pos = 0;
        return ParseBlock(tokens, ref pos);
    }

    private static IEnumerable<string> BareValues(Node node)
    {
        foreach (var c in node.Children)
            if (c.Value != null)
                yield return c.Value;
    }

    private static Node? FindChild(Node node, string key)
    {
        foreach (var c in node.Children)
            if (c.Key == key) return c;
        return null;
    }

    // --- area.txt ---
    // Формат: area_name = { province_ids = { 1 2 3 } }
    //   или:  area_name = { 1 2 3 }
    public static List<Area> ParseAreas(string path)
    {
        var top = Parse(File.ReadAllText(path));
        var result = new List<Area>();

        foreach (var node in top)
        {
            if (node.Key == null) continue;

            var area = new Area { Name = node.Key };
            var idsNode = FindChild(node, "province_ids");
            var source = idsNode ?? node;

            foreach (var v in BareValues(source))
                if (int.TryParse(v, out var pid))
                    area.ProvinceIds.Add(pid);

            if (area.ProvinceIds.Count > 0)
                result.Add(area);
        }
        return result;
    }

    // --- region.txt ---
    // Формат: region_name = { areas = { area1 area2 ... } [monsoon = {...}] }
    public static List<Region> ParseRegions(string path)
    {
        var top = Parse(File.ReadAllText(path));
        var result = new List<Region>();

        foreach (var node in top)
        {
            if (node.Key == null) continue;

            var region = new Region { Name = node.Key };
            var areasNode = FindChild(node, "areas");
            if (areasNode == null) continue; // пустые регионы (france_region и т.п.) пропускаем

            foreach (var v in BareValues(areasNode))
                region.AreaNames.Add(v);

            if (region.AreaNames.Count > 0)
                result.Add(region);
        }
        return result;
    }

    // --- superregion.txt ---
    // Формат: superregion_name = { restrict_charter region1 region2 ... }
    // Регион-имена — это голые токены, оканчивающиеся на "_region".
    public static List<Superregion> ParseSuperregions(string path)
    {
        var top = Parse(File.ReadAllText(path));
        var result = new List<Superregion>();

        foreach (var node in top)
        {
            if (node.Key == null) continue;

            var sr = new Superregion { Name = node.Key };
            foreach (var v in BareValues(node))
                if (v.EndsWith("_region", StringComparison.Ordinal))
                    sr.RegionNames.Add(v);

            if (sr.RegionNames.Count > 0)
                result.Add(sr);
        }
        return result;
    }
}