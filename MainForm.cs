using EU4FLO.Export;
using EU4FLO.Parsers;
using System.Text.Json;
using System.Text.Json.Serialization;
using EU4FLO.Localization;
using EU4FLO.Optimization;

namespace EU4FLO;

public sealed class MainForm : Form
{
    private MapRenderer? _map;
    private List<Province> _provinces = new();
    private List<Area> _areas = new();
    private List<Region> _regions = new();
    private List<Superregion> _superregions = new();

    private HashSet<int> _seaProvinces = new();
    private HashSet<int> _wastelands = new();
    private HashSet<int> _forts = new();
    private Dictionary<int, Province> _provinceById = new();
    private Dictionary<int, SortedSet<int>>? _adjacency;

    private readonly MapCanvas _canvas = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _modeBox = new()
    { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly Button _loadButton = new() { Width = 180 };
    private readonly Button _exportButton = new() { Width = 180, Enabled = false };
    private readonly Button _resetViewButton = new() { Width = 120 };

    private readonly Button _saveMapButton  = new() { Width = 160, Enabled = false };
    private readonly Button _saveViewButton = new() { Width = 160, Enabled = false };

    private ProvinceInfoForm? _infoForm;
    private string? _fortsPath;

    private OptimizationForm? _optimizationForm;
    private Dictionary<int, Color> _optimizationPalette = new();
    private HashSet<int> _optimizationSolution = new();

    private readonly Button _optimizeButton = new() { Width = 200, Enabled = false };

    private static readonly JsonSerializerOptions OptJsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private ProvinceTreeForm? _treeForm;
    private Dictionary<int, string> _provinceToArea = new();
    private Dictionary<string, string> _areaToRegion   = new();
    private Dictionary<string, string> _regionToSuper  = new();

    private readonly Button _treeButton = new() { Width = 170, Enabled = false };

    private bool IsOptimizationMode => _modeBox.SelectedIndex == 5;

    private Dictionary<int, Color> _previewPalette = new();

    private readonly Label _langLabel = new()
    { AutoSize = true, Padding = new Padding(12, 10, 0, 0) };
    private readonly ComboBox _langBox = new()
    { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly Label _modeLabel = new()
    { AutoSize = true, Padding = new Padding(12, 10, 0, 0) };

    public MainForm()
    {
        Width = 1200; Height = 800;

        // Заполняем заглушками; реальные строки придут из ApplyLocalization().
        _modeBox.Items.AddRange(new object[] { "", "", "", "", "", "" });
        _modeBox.SelectedIndex = 0;
        _modeBox.SelectedIndexChanged += (_, _) =>
        {
            _infoForm?.SetOptimizationMode(IsOptimizationMode);
            RefreshMap();
        };

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42 };
        top.Controls.Add(_loadButton);
        top.Controls.Add(_modeLabel);
        top.Controls.Add(_modeBox);
        top.Controls.Add(_resetViewButton);
        top.Controls.Add(_exportButton);
        top.Controls.Add(_saveMapButton);
        top.Controls.Add(_saveViewButton);
        top.Controls.Add(_optimizeButton);
        top.Controls.Add(_treeButton);
        top.Controls.Add(_langLabel);
        top.Controls.Add(_langBox);

        _langBox.SelectedIndexChanged += (_, _) =>
        {
            if (_langBox.SelectedItem is LanguageItem item &&
                item.Code != Loc.CurrentLanguage)
            {
                Loc.SetLanguage(item.Code);
            }
        };

        Loc.LanguageChanged += OnLanguageChanged;
        FormClosed += (_, _) => Loc.LanguageChanged -= OnLanguageChanged;

        Controls.Add(_canvas);
        Controls.Add(top);

        _canvas.ProvinceClicked += OnProvinceClicked;
        _loadButton.Click += (_, _) => LoadFiles();
        _resetViewButton.Click += (_, _) => _canvas.ResetView();
        _exportButton.Click += (_, _) => ExportAdjacency();
        _saveMapButton.Click  += (_, _) => SaveFullMap();
        _saveViewButton.Click += (_, _) => SaveVisibleMap();
        _optimizeButton.Click += (_, _) => OpenOptimizationForm();
        _treeButton.Click += (_, _) => OpenTreeForm();

        PopulateLanguages();
        ApplyLocalization();
    }

    private void LoadFiles()
    {
        using var dlg = new FolderBrowserDialog { Description = Loc.T("main.load.dialog_title") };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        string mapDir = dlg.SelectedPath;
        string defPath = Path.Combine(mapDir, "definition.csv");
        string bmpPath = Path.Combine(mapDir, "provinces.bmp");
        string areaPath = Path.Combine(mapDir, "area.txt");
        string regionPath = Path.Combine(mapDir, "region.txt");
        string superPath = Path.Combine(mapDir, "superregion.txt");
        string defaultMapPath = Path.Combine(mapDir, "default.map");
        string adjPath = Path.Combine(mapDir, "adjacencies.csv");
        string fortsPath = Path.Combine(mapDir, "forts.txt");

        if (!File.Exists(defPath) || !File.Exists(bmpPath))
        {
            MessageBox.Show(Loc.T("main.load.missing_files"));
            return;
        }

        _provinces = DefinitionParser.Parse(defPath);
        _provinceById = _provinces.ToDictionary(p => p.Id, p => p);
        _map = new MapRenderer(bmpPath, _provinces);

        _areas = File.Exists(areaPath) ? HierarchyParser.ParseAreas(areaPath) : new();
        _regions = File.Exists(regionPath) ? HierarchyParser.ParseRegions(regionPath) : new();
        _superregions = File.Exists(superPath) ? HierarchyParser.ParseSuperregions(superPath) : new();

        _seaProvinces = File.Exists(defaultMapPath)
            ? DefaultMapParser.ParseSeaProvinces(defaultMapPath)
            : new HashSet<int>();

        var extraPairs = File.Exists(adjPath)
            ? AdjacenciesCsvParser.Parse(adjPath)
            : new List<(int, int)>();

        string climatePath = Path.Combine(mapDir, "climate.txt");
        _wastelands = File.Exists(climatePath)
            ? ClimateParser.ParseImpassable(climatePath)
            : new HashSet<int>();

        _adjacency = AdjacencyBuilder.Build(_map, _seaProvinces, extraPairs, _wastelands);

        _forts = FortsParser.Parse(fortsPath);
        _fortsPath = fortsPath;

        _canvas.Renderer = _map;
        _exportButton.Enabled   = true;
        _saveMapButton.Enabled  = true;
        _saveViewButton.Enabled = true;
        _optimizeButton.Enabled = true;
        _treeButton.Enabled     = true;

        _provinceToArea.Clear();
        foreach (var a in _areas)
            foreach (var pid in a.ProvinceIds)
                _provinceToArea[pid] = a.Name;

        _areaToRegion.Clear();
        foreach (var r in _regions)
            foreach (var an in r.AreaNames)
                _areaToRegion[an] = r.Name;

        _regionToSuper.Clear();
        foreach (var s in _superregions)
            foreach (var rn in s.RegionNames)
                _regionToSuper[rn] = s.Name;

        MessageBox.Show(
            Loc.T("main.load.summary",
                _provinces.Count, _areas.Count, _regions.Count, _superregions.Count, _forts.Count),
            Loc.T("main.load.complete_title"));

        RefreshMap();
    }

    private void RefreshMap()
    {
        if (_map == null) return;

        Bitmap result = _modeBox.SelectedIndex switch
        {
            0 => _map.RenderProvinceMode(_provinces),
            1 => _map.RenderAreaMode(_areas),
            2 => _map.RenderRegionMode(_areas, _regions),
            3 => _map.RenderSuperregionMode(_areas, _regions, _superregions),
            4 => _map.RenderFortMode(
                    _forts, _seaProvinces, _wastelands,
                    _adjacency ?? new Dictionary<int, SortedSet<int>>()),
            5 => _map.RenderColoredMode(
                    MergePalettes(_optimizationPalette, _previewPalette),
                    _optimizationSolution,
                    _adjacency ?? new Dictionary<int, SortedSet<int>>(),
                    _seaProvinces,
                    _wastelands),
            _ => _map.RenderProvinceMode(_provinces)
        };

        _canvas.SetImage(result);
    }

    private void OnProvinceClicked(int pid)
    {
        if (_infoForm == null)
        {
            _infoForm = new ProvinceInfoForm();
            _infoForm.NeighborSelected        += OnProvinceClicked;
            _infoForm.FortToggled             += OnFortToggled;
            _infoForm.AddToCoverageRequested  += OnAddToCoverage;
            _infoForm.AddToRequiredRequested  += OnAddToRequired;
            _infoForm.FormClosed += (_, _) => _infoForm = null;
            _infoForm.Location = new Point(
                Math.Min(Right + 10, Screen.FromControl(this).WorkingArea.Right - _infoForm.Width),
                Top + 50);
            _infoForm.Show(this);
        }
        else
        {
            _infoForm.BringToFront();
        }

        _infoForm.SetOptimizationMode(IsOptimizationMode);

        if (pid < 0)
        {
            _infoForm.ShowProvince(-1, null, null, _provinceById, false, ProvinceKind.Land,
                null, null, null);
            return;
        }

        _provinceById.TryGetValue(pid, out var province);

        ProvinceKind kind;
        if (_seaProvinces.Contains(pid))     kind = ProvinceKind.Water;
        else if (_wastelands.Contains(pid))  kind = ProvinceKind.Wasteland;
        else                                 kind = ProvinceKind.Land;

        SortedSet<int>? nbrs = null;
        if (kind == ProvinceKind.Land)
            _adjacency?.TryGetValue(pid, out nbrs);

        bool hasFort = kind == ProvinceKind.Land && _forts.Contains(pid);

        string? areaName = null, regionName = null, superName = null;
        if (_provinceToArea.TryGetValue(pid, out var an))
        {
            areaName = an;
            if (_areaToRegion.TryGetValue(an, out var rn))
            {
                regionName = rn;
                if (_regionToSuper.TryGetValue(rn, out var sn))
                    superName = sn;
            }
        }

        _infoForm.ShowProvince(pid, province, nbrs, _provinceById, hasFort, kind,
            areaName, regionName, superName);
    }

    private void ExportAdjacency()
    {
        if (_map == null || _adjacency == null) return;

        using var dlg = new SaveFileDialog
        {
            Filter = Loc.T("main.export.filter"),
            FileName = Loc.T("main.export.default_name")
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        AdjacencyExporter.Export(
            dlg.FileName, _adjacency, _seaProvinces, _wastelands,
            _areas, _regions, _superregions, _provinces);

        MessageBox.Show(Loc.T("main.export.done", dlg.FileName), Loc.T("main.export.title"));
    }

    private void OnFortToggled(int pid, bool hasFort)
    {
        if (_seaProvinces.Contains(pid) || _wastelands.Contains(pid))
            return;

        if (hasFort) _forts.Add(pid);
        else _forts.Remove(pid);

        SaveForts();
        RefreshMap();
    }

    private void SaveForts()
    {
        if (string.IsNullOrEmpty(_fortsPath)) return;
        try
        {
            var lines = _forts.OrderBy(x => x).Select(x => x.ToString());
            File.WriteAllLines(_fortsPath, lines);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.T("main.forts.save_error", ex.Message));
        }
    }

    private void OpenOptimizationForm()
    {
        if (_map == null || _adjacency == null)
        {
            MessageBox.Show(Loc.T("msg.load_first"));
            return;
        }

        if (_optimizationForm == null)
        {
            _optimizationForm = new OptimizationForm();
            _optimizationForm.RunRequested      += RunOptimization;
            _optimizationForm.ResetRequested    += ResetOptimization;
            _optimizationForm.SaveConfigClicked += SaveOptimizationConfig;
            _optimizationForm.LoadConfigClicked += LoadOptimizationConfig;
            _optimizationForm.PreviewRequested  += OnPreviewRequested;
            _optimizationForm.FormClosed        += (_, _) => _optimizationForm = null;
            _optimizationForm.Location = new Point(
                Math.Min(Right + 10,
                    Screen.FromControl(this).WorkingArea.Right - _optimizationForm.Width),
                Top + 50);
            _optimizationForm.Show(this);
        }
        else
        {
            _optimizationForm.BringToFront();
        }

        _infoForm?.SetOptimizationMode(IsOptimizationMode);

        const int optIdx = 5;
        if (_modeBox.SelectedIndex != optIdx) _modeBox.SelectedIndex = optIdx;
    }

    private SelectorContext BuildSelectorContext()
    {
        var ctx = new SelectorContext
        {
            ProvinceById = _provinceById,
            Areas = _areas,
            Regions = _regions,
            Superregions = _superregions,
            SeaProvinces = _seaProvinces,
            Wastelands = _wastelands
        };
        foreach (var p in _provinces)
            if (!ctx.ProvinceIdByName.ContainsKey(p.Name))
                ctx.ProvinceIdByName[p.Name] = p.Id;
        return ctx;
    }

    private void RunOptimization(OptimizationConfig cfg)
    {
        if (_map == null || _adjacency == null) return;

        _previewPalette.Clear();

        var ctx = BuildSelectorContext();

        var coverage = SelectorParser.ParseCoverage(cfg.CoverageText, ctx, out var covErrors);
        if (coverage.Count == 0)
            coverage = _provinces
                .Where(p => !_seaProvinces.Contains(p.Id) && !_wastelands.Contains(p.Id))
                .Select(p => p.Id)
                .ToHashSet();

        var required   = SelectorParser.ParseIdList(cfg.RequiredText);
        var preferred  = SelectorParser.ParseIdList(cfg.PreferredText);
        var exceptions = SelectorParser.ParseIdList(cfg.ExcludedText);
        var groups     = SelectorParser.ParseGroups(cfg.GroupsText, out var groupErrors);

        var candidates = _provinces
            .Where(p => !_seaProvinces.Contains(p.Id) && !_wastelands.Contains(p.Id))
            .Select(p => p.Id)
            .ToHashSet();

        var req = new OptimizationRequest
        {
            Universe = coverage,
            Adjacency = _adjacency,
            Required = required,
            Preferred = preferred,
            Exceptions = exceptions,
            Groups = groups,
            Mode = cfg.Mode,
            Candidates = candidates
        };

        var result = DominatingSetSolver.Solve(req);
        _optimizationSolution = result.Forts;

        var red    = Color.FromArgb(220, 60, 60);
        var orange = Color.FromArgb(240, 150, 50);
        var purple = Color.FromArgb(150, 60, 200);
        var pink   = Color.FromArgb(240, 130, 200);
        var yellow = Color.FromArgb(240, 220, 60);

        var inGroup = new HashSet<int>();
        foreach (var g in groups) foreach (var p in g.Provinces) inGroup.Add(p);

        var palette = new Dictionary<int, Color>();
        foreach (var pid in _optimizationSolution)
        {
            Color c;
            if (required.Contains(pid))        c = red;
            else if (preferred.Contains(pid))  c = orange;
            else if (exceptions.Contains(pid)) c = purple;
            else if (inGroup.Contains(pid))    c = pink;
            else                               c = yellow;

            palette[pid] = c;
        }
        _optimizationPalette = palette;

        var errors = new List<string>();
        errors.AddRange(covErrors);
        errors.AddRange(groupErrors);
        if (errors.Count > 0)
            MessageBox.Show(string.Join("\n", errors), Loc.T("opt.msg.parse_errors"));

        if (result.Warnings.Count > 0)
            MessageBox.Show(string.Join("\n", result.Warnings), Loc.T("opt.msg.warnings"));
        else if (result.CoverageComplete)
            MessageBox.Show(Loc.T("opt.msg.complete", result.Forts.Count), Loc.T("opt.msg.done"));

        _optimizationForm?.ShowSolution(_optimizationSolution, _provinceById);

        RefreshMap();
    }

    private void ResetOptimization()
    {
        _optimizationPalette.Clear();
        _optimizationSolution.Clear();
        _previewPalette.Clear();
        _optimizationForm?.ClearSolution();
        RefreshMap();
    }

    private void SaveOptimizationConfig(OptimizationConfig cfg)
    {
        using var dlg = new SaveFileDialog
        {
            Filter = Loc.T("opt.config.filter"),
            FileName = "optimization.json"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(cfg, OptJsonOpts));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, Loc.T("app.error")); }
    }

    private void LoadOptimizationConfig()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = Loc.T("opt.config.filter")
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var cfg = JsonSerializer.Deserialize<OptimizationConfig>(json, OptJsonOpts);
            if (cfg != null) _optimizationForm?.ApplyConfig(cfg);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, Loc.T("app.error")); }
    }

    private void OnAddToCoverage(int pid)
    {
        if (_optimizationForm == null) OpenOptimizationForm();
        _optimizationForm?.AppendCoverage(pid);

        const int optIdx = 5;
        if (_modeBox.SelectedIndex != optIdx) _modeBox.SelectedIndex = optIdx;
    }

    private void OnAddToRequired(int pid)
    {
        if (_optimizationForm == null) OpenOptimizationForm();
        _optimizationForm?.AppendRequired(pid);

        const int optIdx = 5;
        if (_modeBox.SelectedIndex != optIdx) _modeBox.SelectedIndex = optIdx;
    }

    private void OpenTreeForm()
    {
        if (_map == null) return;

        if (_treeForm == null)
        {
            _treeForm = new ProvinceTreeForm(
                _provinces, _areas, _regions, _superregions,
                _seaProvinces, _wastelands);
            _treeForm.ProvinceSelected += pid => OnProvinceClicked(pid);
            _treeForm.FormClosed += (_, _) => _treeForm = null;
            _treeForm.Location = new Point(
                Math.Min(Right + 10, Screen.FromControl(this).WorkingArea.Right - _treeForm.Width),
                Top + 50);
            _treeForm.Show(this);
        }
        else
        {
            _treeForm.BringToFront();
        }
    }

    private void OnPreviewRequested(PreviewKind kind, OptimizationConfig cfg)
    {
        if (_map == null) return;

        var ctx = BuildSelectorContext();

        Color green  = Color.FromArgb(80, 220, 80);
        Color red    = Color.FromArgb(220, 60, 60);
        Color orange = Color.FromArgb(240, 150, 50);
        Color purple = Color.FromArgb(150, 60, 200);

        switch (kind)
        {
            case PreviewKind.Coverage:
            {
                var cov = SelectorParser.ParseCoverage(cfg.CoverageText, ctx, out _);
                if (cov.Count == 0)
                    cov = _provinces
                        .Where(p => !_seaProvinces.Contains(p.Id) && !_wastelands.Contains(p.Id))
                        .Select(p => p.Id).ToHashSet();
                foreach (var p in cov) _previewPalette[p] = green;
                break;
            }
            case PreviewKind.Required:
                foreach (var p in SelectorParser.ParseIdList(cfg.RequiredText))
                    _previewPalette[p] = red;
                break;

            case PreviewKind.Preferred:
                foreach (var p in SelectorParser.ParseIdList(cfg.PreferredText))
                    _previewPalette[p] = orange;
                break;

            case PreviewKind.Excluded:
                foreach (var p in SelectorParser.ParseIdList(cfg.ExcludedText))
                    _previewPalette[p] = purple;
                break;

            case PreviewKind.Groups:
            {
                var groups = SelectorParser.ParseGroups(cfg.GroupsText, out _);

                var used = new List<Color> { green, red, orange, purple };
                used.AddRange(_previewPalette.Values);

                var rng = new Random();
                foreach (var g in groups)
                {
                    Color c;
                    int guard = 0;
                    do
                    {
                        c = Color.FromArgb(rng.Next(60, 220), rng.Next(60, 220), rng.Next(60, 220));
                        guard++;
                    }
                    while (guard < 500 && used.Any(u => ColorDistance(u, c) < 90));
                    used.Add(c);

                    foreach (var p in g.Provinces) _previewPalette[p] = c;
                }
                break;
            }
        }

        // "Оптимизация" — всегда индекс 5 (текст локализован, искать по имени нельзя).
        const int optIdx = 5;
        if (_modeBox.SelectedIndex != optIdx)
            _modeBox.SelectedIndex = optIdx;
        else
            RefreshMap();
    }

    private static double ColorDistance(Color a, Color b)
    {
        double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private static Dictionary<int, Color> MergePalettes(
        Dictionary<int, Color> basePalette,
        Dictionary<int, Color> overlay)
    {
        var result = new Dictionary<int, Color>(basePalette);
        foreach (var kv in overlay) result[kv.Key] = kv.Value;
        return result;
    }

    private sealed record LanguageItem(string Code, string Name)
    {
        public override string ToString() => Name;
    }

    private void PopulateLanguages()
    {
        _langBox.Items.Clear();
        foreach (var (code, name) in Loc.AvailableLanguages().OrderBy(x => x.Code))
            _langBox.Items.Add(new LanguageItem(code, name));

        for (int i = 0; i < _langBox.Items.Count; i++)
            if (((LanguageItem)_langBox.Items[i]!).Code == Loc.CurrentLanguage)
            { _langBox.SelectedIndex = i; break; }
        if (_langBox.SelectedIndex < 0 && _langBox.Items.Count > 0)
            _langBox.SelectedIndex = 0;
    }

    private void OnLanguageChanged()
    {
        ApplyLocalization();
        _infoForm?.ApplyLocalization();
        _optimizationForm?.ApplyLocalization();
        _treeForm?.ApplyLocalization();
    }

    public void ApplyLocalization()
    {
        Text = Loc.T("app.title");

        _loadButton.Text      = Loc.T("main.btn.load");
        _exportButton.Text    = Loc.T("main.btn.export");
        _saveMapButton.Text  = Loc.T("main.btn.save_map");
        _saveViewButton.Text = Loc.T("main.btn.save_view");
        _resetViewButton.Text = Loc.T("main.btn.reset_view");
        _optimizeButton.Text  = Loc.T("main.btn.optimize");
        _treeButton.Text      = Loc.T("main.btn.tree");
        _modeLabel.Text       = Loc.T("main.mode.label");
        _langLabel.Text       = Loc.T("menu.language");
        

        int sel = _modeBox.SelectedIndex;
        _modeBox.Items.Clear();
        _modeBox.Items.AddRange(new object[]
        {
            Loc.T("main.mode.province"),
            Loc.T("main.mode.area"),
            Loc.T("main.mode.region"),
            Loc.T("main.mode.superregion"),
            Loc.T("main.mode.forts"),
            Loc.T("main.mode.optimization")
        });
        _modeBox.SelectedIndex = sel >= 0 ? sel : 0;
    }

    private void SaveFullMap()
    {
        if (!_canvas.HasImage) return;

        using var dlg = new SaveFileDialog
        {
            Filter   = Loc.T("main.export.png_filter"),
            FileName = Loc.T("main.export.save_map_default")
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            using var bmp = _canvas.RenderFull();
            if (bmp == null) return;
            bmp.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Loc.T("app.error"));
        }
    }

    private void SaveVisibleMap()
    {
        if (!_canvas.HasImage) return;

        using var dlg = new SaveFileDialog
        {
            Filter   = Loc.T("main.export.png_filter"),
            FileName = Loc.T("main.export.save_view_default")
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            using var bmp = _canvas.RenderVisible();
            bmp.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Loc.T("app.error"));
        }
    }
}