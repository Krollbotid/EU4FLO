// Rendering/MapRenderer.cs
using System.Drawing;
using System.Drawing.Imaging;
using EU4FLO.Localization;
using System.Linq;

public sealed class MapRenderer
{
    private readonly int _width, _height;
    private readonly uint[] _provincePixels;
    private readonly Dictionary<uint, int> _colorToProvinceId;
    private readonly List<Province> _provinces;

    public MapRenderer(string bmpPath, List<Province> provinces)
    {
        _provinces = provinces;
        using var bmp = new Bitmap(bmpPath);
        _width = bmp.Width;
        _height = bmp.Height;
        _provincePixels = new uint[_width * _height];
        _colorToProvinceId = new Dictionary<uint, int>();
        
        foreach (var p in provinces)
        {
            if (!_colorToProvinceId.ContainsKey(p.Packed))
            {
                _colorToProvinceId[p.Packed] = p.Id;
            }
        }

        // Собираем все дубликаты в один список
        var duplicateLines = provinces
            .GroupBy(p => p.Packed)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} -> IDs: {string.Join(", ", g.Select(p => p.Id))}")
            .ToList();

        // Показываем одно сообщение, если есть дубликаты
        if (duplicateLines.Count > 0)
        {
            string msg = Loc.T("map.warn_duplicate_colors", string.Join("\n", duplicateLines));
            MessageBox.Show(msg, Loc.T("app.error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        var rect = new Rectangle(0, 0, _width, _height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                int stride = data.Stride;
                for (int y = 0; y < _height; y++)
                {
                    byte* row = ptr + y * stride;
                    for (int x = 0; x < _width; x++)
                    {
                        byte b = row[x * 3];
                        byte g = row[x * 3 + 1];
                        byte r = row[x * 3 + 2];
                        _provincePixels[y * _width + x] = (uint)((r << 16) | (g << 8) | b);
                    }
                }
            }
        }
        finally { bmp.UnlockBits(data); }
    }

    public int Width => _width;
    public int Height => _height;

    public int GetProvinceIdAt(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height) return -1;
        uint packed = _provincePixels[y * _width + x];
        return _colorToProvinceId.TryGetValue(packed, out var id) ? id : -1;
    }

    // ---------- РЕЖИМЫ ОТОБРАЖЕНИЯ ----------

    public Bitmap RenderProvinceMode(List<Province> provinces)
    {
        var idToColor = new Dictionary<int, Color>();
        foreach (var p in provinces)
            idToColor[p.Id] = Color.FromArgb(255, p.R, p.G, p.B);

        return RenderByProvince((pid, _, _) =>
            idToColor.TryGetValue(pid, out var c) ? c : Color.Black);
    }

    public Bitmap RenderAreaMode(List<Area> areas)
    {
        var provinceToArea = new Dictionary<int, int>();
        for (int i = 0; i < areas.Count; i++)
            foreach (var pid in areas[i].ProvinceIds)
                provinceToArea[pid] = i;

        var colors = areas.Select((_, i) => ColorAssigner.FromHash(i * 7919)).ToArray();

        return RenderByProvince((pid, _, _) =>
            provinceToArea.TryGetValue(pid, out var ai) ? colors[ai] : Color.Black);
    }

    public Bitmap RenderRegionMode(List<Area> areas, List<Region> regions)
    {
        var areaToRegion = new Dictionary<string, int>();
        for (int i = 0; i < regions.Count; i++)
            foreach (var an in regions[i].AreaNames)
                areaToRegion[an] = i;

        var provinceToAreaName = new Dictionary<int, string>();
        foreach (var area in areas)
            foreach (var pid in area.ProvinceIds)
                provinceToAreaName[pid] = area.Name;

        var colors = regions.Select((_, i) => ColorAssigner.FromHash(i * 104729)).ToArray();

        return RenderByProvince((pid, _, _) =>
            provinceToAreaName.TryGetValue(pid, out var an) &&
            areaToRegion.TryGetValue(an, out var ri)
                ? colors[ri]
                : Color.Black);
    }

    public Bitmap RenderSuperregionMode(
        List<Area> areas, List<Region> regions, List<Superregion> superregions)
    {
        var areaToRegion = new Dictionary<string, string>();
        foreach (var r in regions)
            foreach (var an in r.AreaNames)
                areaToRegion[an] = r.Name;

        var regionToSuper = new Dictionary<string, int>();
        for (int i = 0; i < superregions.Count; i++)
            foreach (var rn in superregions[i].RegionNames)
                regionToSuper[rn] = i;

        var provinceToAreaName = new Dictionary<int, string>();
        foreach (var area in areas)
            foreach (var pid in area.ProvinceIds)
                provinceToAreaName[pid] = area.Name;

        var colors = superregions.Select((_, i) => ColorAssigner.FromHash(i * 15485863)).ToArray();

        return RenderByProvince((pid, _, _) =>
            provinceToAreaName.TryGetValue(pid, out var an) &&
            areaToRegion.TryGetValue(an, out var rn) &&
            regionToSuper.TryGetValue(rn, out var si)
                ? colors[si]
                : Color.Black);
    }

    public Bitmap RenderFortMode(
        HashSet<int> forts,
        HashSet<int> seaProvinces,
        HashSet<int> wastelands,
        Dictionary<int, SortedSet<int>> adjacency)
    {
        Color fortColor    = Color.FromArgb(60, 200, 60);
        Color neighborCol  = Color.FromArgb(60, 200, 60);
        Color neutralLand  = Color.White;
        Color waterColor   = Color.FromArgb(50, 100, 200);
        Color wasteColor   = Color.Black;
    
        return RenderByProvince((pid, x, y) =>
        {
            if (wastelands.Contains(pid))  return wasteColor;
            if (seaProvinces.Contains(pid)) return waterColor;
            if (forts.Contains(pid))       return fortColor;
    
            if (adjacency.TryGetValue(pid, out var nbrs))
                foreach (var n in nbrs)
                    if (forts.Contains(n))
                        return ((x + y) % 8 < 4) ? neighborCol : neutralLand;
    
            return neutralLand;
        }, drawBorders: true);
    }

    public Bitmap RenderColoredMode(
        Dictionary<int, Color> palette,
        HashSet<int> fortSolution,
        Dictionary<int, SortedSet<int>> adjacency,
        HashSet<int> seaProvinces,
        HashSet<int> wastelands)
    {
        Color water    = Color.FromArgb(50, 100, 200);
        Color waste    = Color.Black;
        Color stripe   = Color.FromArgb(60, 200, 60);   // как в режиме фортов
        Color fallback = Color.White;
    
        return RenderByProvince((pid, x, y) =>
        {
            if (wastelands.Contains(pid))   return waste;
            if (seaProvinces.Contains(pid)) return water;
    
            // 1) провинция-крепость — своя палитра
            if (palette.TryGetValue(pid, out var c)) return c;
    
            // 2) суша без крепости, но соседняя с крепостью из решения — штрих
            if (adjacency.TryGetValue(pid, out var nbrs))
            {
                foreach (var n in nbrs)
                    if (fortSolution.Contains(n))
                        return ((x + y) % 8 < 4) ? stripe : fallback;
            }
    
            // 3) всё остальное
            return fallback;
        }, drawBorders: true);
    }

    // ---------- общий цикл перекраски ----------

    private Bitmap RenderByProvince(
        Func<int, int, int, Color> colorForProvince,
        bool drawBorders = false)
    {
        var bmp = new Bitmap(_width, _height);
        var data = bmp.LockBits(
            new Rectangle(0, 0, _width, _height),
            ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                int stride = data.Stride;

                // для быстрой проверки границ заранее считаем id всех пикселей
                int[] ids = null!;
                if (drawBorders)
                {
                    ids = new int[_width * _height];
                    for (int y = 0; y < _height; y++)
                        for (int x = 0; x < _width; x++)
                            ids[y * _width + x] = GetProvinceIdAt(x, y);
                }

                for (int y = 0; y < _height; y++)
                {
                    byte* row = ptr + y * stride;
                    for (int x = 0; x < _width; x++)
                    {
                        int pid = GetProvinceIdAt(x, y);
                        Color c = pid >= 0 ? colorForProvince(pid, x, y) : Color.Black;

                        if (drawBorders && pid >= 0)
                        {
                            int rightId = x + 1 < _width ? ids[y * _width + (x + 1)] : -1;
                            int bottomId = y + 1 < _height ? ids[(y + 1) * _width + x] : -1;
                            bool isBorder =
                                (rightId >= 0 && rightId != pid) ||
                                (bottomId >= 0 && bottomId != pid);
                            if (isBorder) c = Color.Black;
                        }

                        row[x * 4 + 0] = c.B;
                        row[x * 4 + 1] = c.G;
                        row[x * 4 + 2] = c.R;
                        row[x * 4 + 3] = 255;
                    }
                }
            }
        }
        finally { bmp.UnlockBits(data); }
        return bmp;
    }
}