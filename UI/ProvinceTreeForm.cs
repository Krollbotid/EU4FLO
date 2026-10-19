using EU4FLO.Localization;

namespace EU4FLO;

public sealed class ProvinceTreeForm : Form
{
    private readonly TreeView _tree = new()
    {
        Dock = DockStyle.Fill,
        HideSelection = false,
        ShowLines = true,
        ShowPlusMinus = true,
        ShowRootLines = true
    };

    private readonly List<Province> _provinces;
    private readonly List<Area> _areas;
    private readonly List<Region> _regions;
    private readonly List<Superregion> _superregions;
    private readonly HashSet<int> _seaProvinces;
    private readonly HashSet<int> _wastelands;

    public event Action<int>? ProvinceSelected;

    public ProvinceTreeForm(
        List<Province> provinces,
        List<Area> areas,
        List<Region> regions,
        List<Superregion> superregions,
        HashSet<int> seaProvinces,
        HashSet<int> wastelands)
    {
        _provinces = provinces;
        _areas = areas;
        _regions = regions;
        _superregions = superregions;
        _seaProvinces = seaProvinces;
        _wastelands = wastelands;

        Width = 420;
        Height = 720;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;

        _tree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is int pid)
                ProvinceSelected?.Invoke(pid);
        };

        Controls.Add(_tree);

        Loc.LanguageChanged += ApplyLocalization;
        FormClosed += (_, _) => Loc.LanguageChanged -= ApplyLocalization;

        ApplyLocalization();
    }

    public void ApplyLocalization()
    {
        Text = Loc.T("tree.title");
        Rebuild();
    }

    private void Rebuild()
    {
        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            Build();
        }
        finally { _tree.EndUpdate(); }
    }

    private void Build()
    {
        var provinceById = _provinces.ToDictionary(p => p.Id, p => p);
        var areaByName   = _areas.ToDictionary(a => a.Name, a => a,
            StringComparer.OrdinalIgnoreCase);
        var regionByName = _regions.ToDictionary(r => r.Name, r => r,
            StringComparer.OrdinalIgnoreCase);

        var placedPids   = new HashSet<int>();
        var usedRegions  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedAreas    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var super in _superregions)
        {
            var superNode = _tree.Nodes.Add(super.Name);

            foreach (var regionName in super.RegionNames)
            {
                if (!regionByName.TryGetValue(regionName, out var region)) continue;
                usedRegions.Add(region.Name);
                var regionNode = superNode.Nodes.Add(region.Name);

                foreach (var areaName in region.AreaNames)
                {
                    if (!areaByName.TryGetValue(areaName, out var area)) continue;
                    usedAreas.Add(area.Name);
                    var areaNode = regionNode.Nodes.Add(area.Name);

                    foreach (var pid in area.ProvinceIds)
                    {
                        AddProvinceNode(areaNode, pid, provinceById);
                        placedPids.Add(pid);
                    }
                }
            }
        }

        // Регионы без суперрегиона
        var orphanRegions = _regions
            .Where(r => !usedRegions.Contains(r.Name))
            .ToList();
        if (orphanRegions.Count > 0)
        {
            var orphanRegionsNode = _tree.Nodes.Add(Loc.T("tree.orphan.regions"));
            foreach (var region in orphanRegions)
            {
                var regionNode = orphanRegionsNode.Nodes.Add(region.Name);
                foreach (var areaName in region.AreaNames)
                {
                    if (!areaByName.TryGetValue(areaName, out var area)) continue;
                    usedAreas.Add(area.Name);
                    var areaNode = regionNode.Nodes.Add(area.Name);
                    foreach (var pid in area.ProvinceIds)
                    {
                        AddProvinceNode(areaNode, pid, provinceById);
                        placedPids.Add(pid);
                    }
                }
            }
        }

        // Области без региона
        var orphanAreas = _areas
            .Where(a => !usedAreas.Contains(a.Name))
            .ToList();
        if (orphanAreas.Count > 0)
        {
            var orphanAreasNode = _tree.Nodes.Add(Loc.T("tree.orphan.areas"));
            foreach (var area in orphanAreas)
            {
                var areaNode = orphanAreasNode.Nodes.Add(area.Name);
                foreach (var pid in area.ProvinceIds)
                {
                    AddProvinceNode(areaNode, pid, provinceById);
                    placedPids.Add(pid);
                }
            }
        }

        // Провинции без области
        var orphanProvinces = _provinces
            .Where(p => !placedPids.Contains(p.Id))
            .OrderBy(p => p.Id)
            .ToList();
        if (orphanProvinces.Count > 0)
        {
            var orphans = _tree.Nodes.Add(Loc.T("tree.orphan.provinces"));
            foreach (var p in orphanProvinces)
                AddProvinceNode(orphans, p.Id, provinceById);
        }
    }

    private void AddProvinceNode(TreeNode parent, int pid,
        Dictionary<int, Province> provinceById)
    {
        var name = provinceById.TryGetValue(pid, out var p) ? p.Name : Loc.T("tree.unknown");

        string suffix = "";
        if (_seaProvinces.Contains(pid))      suffix = "  " + Loc.T("tree.tag.water");
        else if (_wastelands.Contains(pid))   suffix = "  " + Loc.T("tree.tag.wasteland");

        var node = parent.Nodes.Add($"{name} ({pid}){suffix}");
        node.Tag = pid;
        if (suffix.Length > 0) node.ForeColor = Color.DimGray;
    }
}