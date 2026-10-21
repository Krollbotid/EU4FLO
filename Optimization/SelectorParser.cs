using EU4FLO.Localization;

namespace EU4FLO.Optimization;

public sealed class SelectorContext
{
    public Dictionary<int, Province> ProvinceById { get; init; } = new();
    public Dictionary<string, int> ProvinceIdByName { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<Area> Areas { get; init; } = new();
    public List<Region> Regions { get; init; } = new();
    public List<Superregion> Superregions { get; init; } = new();
    public HashSet<int> SeaProvinces { get; init; } = new();
    public HashSet<int> Wastelands { get; init; } = new();
}

public sealed class FortGroup
{
    public string Name { get; set; } = "";
    public HashSet<int> Provinces { get; } = new();
    public int MinForts { get; set; }
}

public static class SelectorParser
{
    /// <summary>Читает строки вида "province, X", "area, Y", "region, Z", "superregion, W".</summary>
    public static HashSet<int> ParseCoverage(string text, SelectorContext ctx, out List<string> errors)
    {
        errors = new();
        var result = new HashSet<int>();

        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            var parts = line.Split(',', 2);
            if (parts.Length != 2) { errors.Add(Loc.T("parser.bad_line", line)); continue; }
            var kind = parts[0].Trim().ToLowerInvariant();
            var arg  = parts[1].Trim();

            switch (kind)
            {
                case "province" or "prov" or "p":
                    if (int.TryParse(arg, out var pid)) AddProvince(result, pid, ctx);
                    else if (ctx.ProvinceIdByName.TryGetValue(arg, out var pid2)) AddProvince(result, pid2, ctx);
                    else errors.Add(Loc.T("parser.unknown_province", arg));
                    break;

                case "area" or "a":
                {
                    var area = ctx.Areas.FirstOrDefault(x => MatchName(x.Name, arg, "_area"));
                    if (area == null) { errors.Add(Loc.T("parser.unknown_area", arg)); break; }
                    foreach (var p in area.ProvinceIds) AddProvince(result, p, ctx);
                    break;
                }

                case "region" or "r":
                {
                    var region = ctx.Regions.FirstOrDefault(x => MatchName(x.Name, arg, "_region"));
                    if (region == null) { errors.Add(Loc.T("parser.unknown_region", arg)); break; }
                    foreach (var an in region.AreaNames)
                    {
                        var a = ctx.Areas.FirstOrDefault(x => x.Name.Equals(an, StringComparison.OrdinalIgnoreCase));
                        if (a == null) continue;
                        foreach (var p in a.ProvinceIds) AddProvince(result, p, ctx);
                    }
                    break;
                }

                case "superregion" or "s":
                {
                    var sr = ctx.Superregions.FirstOrDefault(x => MatchName(x.Name, arg, "_superregion"));
                    if (sr == null) { errors.Add(Loc.T("parser.unknown_superregion", arg)); break; }
                    foreach (var rn in sr.RegionNames)
                    {
                        var r = ctx.Regions.FirstOrDefault(x => x.Name.Equals(rn, StringComparison.OrdinalIgnoreCase));
                        if (r == null) continue;
                        foreach (var an in r.AreaNames)
                        {
                            var a = ctx.Areas.FirstOrDefault(x => x.Name.Equals(an, StringComparison.OrdinalIgnoreCase));
                            if (a == null) continue;
                            foreach (var p in a.ProvinceIds) AddProvince(result, p, ctx);
                        }
                    }
                    break;
                }

                default:
                    errors.Add(Loc.T("parser.unknown_selector", kind));
                    break;
            }
        }
        return result;
    }

    /// <summary>Сравнивает имена, игнорируя регистр и необязательный суффикс (например, "_area").</summary>
    private static bool MatchName(string actual, string query, string suffix)
    {
        if (actual.Equals(query, StringComparison.OrdinalIgnoreCase)) return true;
        
        var actualBase = actual.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) 
            ? actual.Substring(0, actual.Length - suffix.Length) 
            : actual;
            
        var queryBase = query.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) 
            ? query.Substring(0, query.Length - suffix.Length) 
            : query;

        return actualBase.Equals(queryBase, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddProvince(HashSet<int> target, int pid, SelectorContext ctx)
    {
        if (ctx.SeaProvinces.Contains(pid)) return;
        if (ctx.Wastelands.Contains(pid)) return;
        target.Add(pid);
    }

    /// <summary>Список ID через запятую/пробел/перенос строки.</summary>
    public static HashSet<int> ParseIdList(string text)
    {
        var result = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (var raw in text.Split(
            new[] { ',', ' ', '\n', '\r', '\t', ';' },
            StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(raw.Trim(), out var id)) result.Add(id);
        }
        return result;
    }

    /// <summary>Групповые ограничения: "Name : p1, p2, ... : Min".</summary>
    public static List<FortGroup> ParseGroups(string text, out List<string> errors)
    {
        errors = new();
        var result = new List<FortGroup>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            var parts = line.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length < 3) { errors.Add(Loc.T("parser.bad_group", line)); continue; }

            var name   = parts[0];
            var provs  = parts[1];
            var minStr = parts[^1];

            if (!int.TryParse(minStr, out var min) || min < 0)
            {
                errors.Add(Loc.T("parser.bad_group_min", line));
                continue;
            }

            var group = new FortGroup { Name = name, MinForts = min };
            foreach (var pid in ParseIdList(provs)) group.Provinces.Add(pid);
            result.Add(group);
        }
        return result;
    }
}