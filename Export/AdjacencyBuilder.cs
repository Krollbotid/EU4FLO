// Export/AdjacencyBuilder.cs
namespace EU4FLO.Export;

public static class AdjacencyBuilder
{
    /// <summary>
    /// Возвращает для каждой сухопутной провинции множество соседних сухопутных провинций.
    /// Морские и озёрные провинции (из default.map) исключаются.
    /// </summary>
    public static Dictionary<int, SortedSet<int>> Build(
        MapRenderer map,
        HashSet<int> seaProvinces,
        IEnumerable<(int From, int To)>? extraPairs = null,
        HashSet<int>? wastelands = null)
    {
        var excluded = new HashSet<int>(seaProvinces);
        if (wastelands != null) excluded.UnionWith(wastelands);
    
        int w = map.Width, h = map.Height;
        var result = new Dictionary<int, SortedSet<int>>();
    
        void Ensure(int pid)
        {
            if (pid >= 0 && !excluded.Contains(pid) && !result.ContainsKey(pid))
                result[pid] = new SortedSet<int>();
        }
    
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int a = map.GetProvinceIdAt(x, y);
                Ensure(a);
                if (x + 1 < w) TryLink(result, excluded, a, map.GetProvinceIdAt(x + 1, y));
                if (y + 1 < h) TryLink(result, excluded, a, map.GetProvinceIdAt(x, y + 1));
            }
    
        if (extraPairs != null)
            foreach (var (from, to) in extraPairs)
            {
                if (from == to) continue;
                if (excluded.Contains(from) || excluded.Contains(to)) continue;
                EnsurePair(result, from);
                EnsurePair(result, to);
                result[from].Add(to);
                result[to].Add(from);
            }
    
        return result;
    }
    
    private static void TryLink(
        Dictionary<int, SortedSet<int>> result,
        HashSet<int> excluded,
        int a, int b)
    {
        if (a < 0 || b < 0 || a == b) return;
        if (excluded.Contains(a) || excluded.Contains(b)) return;
        EnsurePair(result, a);
        EnsurePair(result, b);
        result[a].Add(b);
        result[b].Add(a);
    }
    
    private static void EnsurePair(Dictionary<int, SortedSet<int>> d, int pid)
    {
        if (!d.ContainsKey(pid)) d[pid] = new SortedSet<int>();
    }
}