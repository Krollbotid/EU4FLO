// Export/AdjacencyExporter.cs
using System.Text;

namespace EU4FLO.Export;

public static class AdjacencyExporter
{
    public static void Export(
        string outputPath,
        Dictionary<int, SortedSet<int>> adjacency,
        HashSet<int> seaProvinces,
        HashSet<int> wastelands,
        List<Area> areas,
        List<Region> regions,
        List<Superregion> superregions,
        List<Province> provinces)
    {
        // provinceId -> areaName
        var provinceToArea = new Dictionary<int, string>();
        foreach (var a in areas)
            foreach (var pid in a.ProvinceIds)
                provinceToArea[pid] = a.Name;

        // areaName -> список provinceId (отсортированный)
        var areaProvinces = new Dictionary<string, List<int>>();
        foreach (var a in areas)
            areaProvinces[a.Name] = a.ProvinceIds.OrderBy(x => x).ToList();

        // regionName -> список areaName (в порядке из region.txt)
        var regionAreas = new Dictionary<string, List<string>>();
        foreach (var r in regions)
            regionAreas[r.Name] = r.AreaNames.ToList();

        // superregionName -> список regionName
        var superRegions = new Dictionary<string, List<string>>();
        foreach (var s in superregions)
            superRegions[s.Name] = s.RegionNames.ToList();

        var sb = new StringBuilder();

        void WriteProvinceLine(int pid, string indent)
        {
            sb.Append(indent).Append(pid).Append(" : ");
            if (wastelands.Contains(pid))
                sb.Append("wasteland");
            else if (adjacency.TryGetValue(pid, out var nbrs) && nbrs.Count > 0)
                sb.Append(string.Join(", ", nbrs));
            sb.AppendLine();
        }

        foreach (var (superName, regionNames) in superRegions)
        {
            sb.Append("# ").AppendLine(superName);
            foreach (var regionName in regionNames)
            {
                if (!regionAreas.TryGetValue(regionName, out var areaNames)) continue;
                sb.Append("    # ").AppendLine(regionName);

                foreach (var areaName in areaNames)
                {
                    if (!areaProvinces.TryGetValue(areaName, out var provinceIds)) continue;
                    sb.Append("        # ").AppendLine(areaName);
                    foreach (var pid in provinceIds)
                        WriteProvinceLine(pid, "            ");
                }
            }
            sb.AppendLine();
        }

        var orphans = provinces
            .Where(p => !seaProvinces.Contains(p.Id) && !provinceToArea.ContainsKey(p.Id))
            .Select(p => p.Id)
            .OrderBy(x => x)
            .ToList();

        if (orphans.Count > 0)
        {
            sb.AppendLine("# (provinces not assigned to any area)");
            foreach (var pid in orphans)
                WriteProvinceLine(pid, "    ");
        }

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }
}