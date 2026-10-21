using System.Text;
using EU4FLO.Localization;
using EU4FLO.Optimization;

namespace EU4FLO;

public enum PreviewKind { Coverage, Required, Preferred, Excluded, Groups }

public sealed class OptimizationForm : Form
{
    // --- поля ввода ---
    private readonly TextBox _coverageBox  = MakeMultiline();
    private readonly TextBox _requiredBox  = MakeMultiline();
    private readonly TextBox _preferredBox = MakeMultiline();
    private readonly TextBox _excludedBox  = MakeMultiline();
    private readonly TextBox _groupsBox    = MakeMultiline();
    private readonly ComboBox _modeCombo   = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Top,
        Height = 24
    };

    // --- заголовки и подсказки для локализации ---
    private readonly Label _covTitle  = MakeTitle();
    private readonly Label _covHint   = MakeHint();
    private readonly Label _reqTitle  = MakeTitle();
    private readonly Label _reqHint   = MakeHint();
    private readonly Label _prefTitle = MakeTitle();
    private readonly Label _prefHint  = MakeHint();
    private readonly Label _excTitle  = MakeTitle();
    private readonly Label _excHint   = MakeHint();
    private readonly Label _grpTitle  = MakeTitle();
    private readonly Label _grpHint   = MakeHint();
    private readonly Label _modeTitle = MakeTitle();

    // --- кнопки предпросмотра ---
    private readonly Button _covBtn  = MakePreviewButton();
    private readonly Button _reqBtn  = MakePreviewButton();
    private readonly Button _prefBtn = MakePreviewButton();
    private readonly Button _excBtn  = MakePreviewButton();
    private readonly Button _grpBtn  = MakePreviewButton();

    // --- нижние кнопки ---
    private readonly Button _runBtn   = new() { Width = 180 };
    private readonly Button _resetBtn = new() { Width = 150 };
    private readonly Button _saveBtn  = new() { Width = 140 };
    private readonly Button _loadBtn  = new() { Width = 140 };

    // --- блок вывода ---
    private readonly GroupBox _outputGroup = new()
    {
        Dock = DockStyle.Bottom, Height = 220, Padding = new Padding(8)
    };
    private readonly Label _selectedCountLabel = new()
    {
        Dock = DockStyle.Top, Height = 24,
        Padding = new Padding(8, 4, 0, 0),
        Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
    };
    private readonly TextBox _idsBox   = MakeReadonlyMultiline();
    private readonly TextBox _namesBox = MakeReadonlyMultiline();
    private readonly Label _idHeader   = new()
    {
        Dock = DockStyle.Top, Height = 18, Padding = new Padding(4, 0, 0, 0)
    };
    private readonly Label _nameHeader = new()
    {
        Dock = DockStyle.Top, Height = 18, Padding = new Padding(4, 0, 0, 0)
    };

    // --- события ---
    public event Action<OptimizationConfig>? RunRequested;
    public event Action? ResetRequested;
    public event Action<OptimizationConfig>? SaveConfigClicked;
    public event Action? LoadConfigClicked;
    public event Action<PreviewKind, OptimizationConfig>? PreviewRequested;

    public OptimizationForm()
    {
        Width = 760;
        Height = 900;
        MinimumSize = new Size(640, 640);
        StartPosition = FormStartPosition.Manual;

        _covBtn.Click  += (_, _) => PreviewRequested?.Invoke(PreviewKind.Coverage,  CurrentConfig());
        _reqBtn.Click  += (_, _) => PreviewRequested?.Invoke(PreviewKind.Required,  CurrentConfig());
        _prefBtn.Click += (_, _) => PreviewRequested?.Invoke(PreviewKind.Preferred, CurrentConfig());
        _excBtn.Click  += (_, _) => PreviewRequested?.Invoke(PreviewKind.Excluded,  CurrentConfig());
        _grpBtn.Click  += (_, _) => PreviewRequested?.Invoke(PreviewKind.Groups,    CurrentConfig());

        _runBtn.Click   += (_, _) => RunRequested?.Invoke(CurrentConfig());
        _resetBtn.Click += (_, _) => ResetRequested?.Invoke();
        _saveBtn.Click  += (_, _) => SaveConfigClicked?.Invoke(CurrentConfig());
        _loadBtn.Click  += (_, _) => LoadConfigClicked?.Invoke();

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(8),
            AutoScroll = true
        };

        AddParameterBlock(table, _covTitle, _covHint, _covBtn, _coverageBox);
        AddParameterBlock(table, _reqTitle, _reqHint, _reqBtn, _requiredBox);
        AddParameterBlock(table, _prefTitle, _prefHint, _prefBtn, _preferredBox);
        AddParameterBlock(table, _excTitle, _excHint, _excBtn, _excludedBox);
        AddParameterBlock(table, _grpTitle, _grpHint, _grpBtn, _groupsBox);

        var modeRow = new TableLayoutPanel
        {
            ColumnCount = 2, RowCount = 2,
            Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 6, 0, 6)
        };
        modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        modeRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        modeRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        modeRow.Controls.Add(_modeTitle, 0, 0);
        modeRow.SetColumnSpan(_modeTitle, 2);
        modeRow.Controls.Add(_modeCombo, 1, 1);
        table.Controls.Add(modeRow);

        // --- блок вывода ---
        var leftPanel  = new Panel { Dock = DockStyle.Fill };
        leftPanel.Controls.Add(_idsBox);
        leftPanel.Controls.Add(_idHeader);

        var rightPanel = new Panel { Dock = DockStyle.Fill };
        rightPanel.Controls.Add(_namesBox);
        rightPanel.Controls.Add(_nameHeader);

        var outputGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1
        };
        outputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        outputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outputGrid.Controls.Add(leftPanel, 0, 0);
        outputGrid.Controls.Add(rightPanel, 1, 0);

        _outputGroup.Controls.Add(outputGrid);
        _outputGroup.Controls.Add(_selectedCountLabel);

        // --- нижняя панель ---
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 46,
            Padding = new Padding(8, 6, 8, 6), WrapContents = false
        };
        buttons.Controls.Add(_runBtn);
        buttons.Controls.Add(_resetBtn);
        buttons.Controls.Add(_saveBtn);
        buttons.Controls.Add(_loadBtn);

        Controls.Add(table);
        Controls.Add(_outputGroup);
        Controls.Add(buttons);

        Loc.LanguageChanged += ApplyLocalization;
        FormClosed += (_, _) => Loc.LanguageChanged -= ApplyLocalization;

        ApplyLocalization();
    }

    // ---------- ЛОКАЛИЗАЦИЯ ----------

    public void ApplyLocalization()
    {
        Text = Loc.T("opt.title");

        _covTitle.Text  = Loc.T("opt.field.coverage");
        _covHint.Text   = Loc.T("opt.field.coverage.hint");
        _reqTitle.Text  = Loc.T("opt.field.required");
        _reqHint.Text   = Loc.T("opt.hint.ids");
        _prefTitle.Text = Loc.T("opt.field.preferred");
        _prefHint.Text  = Loc.T("opt.hint.ids");
        _excTitle.Text  = Loc.T("opt.field.excluded");
        _excHint.Text   = Loc.T("opt.hint.ids");
        _grpTitle.Text  = Loc.T("opt.field.groups");
        _grpHint.Text   = Loc.T("opt.hint.groups");
        _modeTitle.Text = Loc.T("opt.field.mode");

        int prevMode = _modeCombo.SelectedIndex;
        _modeCombo.Items.Clear();
        _modeCombo.Items.AddRange(new object[]
        {
            Loc.T("opt.mode.no_adjacent"),
            Loc.T("opt.mode.homebrew"),
            Loc.T("opt.mode.any")
        });
        _modeCombo.SelectedIndex = prevMode >= 0 ? prevMode : 0;

        var previewText = Loc.T("opt.btn.show_on_map");
        _covBtn.Text  = previewText;
        _reqBtn.Text  = previewText;
        _prefBtn.Text = previewText;
        _excBtn.Text  = previewText;
        _grpBtn.Text  = previewText;

        _runBtn.Text   = Loc.T("opt.btn.run");
        _resetBtn.Text = Loc.T("opt.btn.reset");
        _saveBtn.Text  = Loc.T("opt.btn.save_config");
        _loadBtn.Text  = Loc.T("opt.btn.load_config");

        _outputGroup.Text = Loc.T("opt.output.title");
        _idHeader.Text    = Loc.T("opt.output.id");
        _nameHeader.Text  = Loc.T("opt.output.name");

        // Обновить строку счётчика. Если решение уже показывалось,
        // количество можно вытащить из первой строки ID-поля.
        int count = 0;
        if (!string.IsNullOrEmpty(_idsBox.Text))
            count = _idsBox.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        _selectedCountLabel.Text = Loc.T("opt.output.count", count);
    }

    // ---------- API ----------

    public OptimizationConfig CurrentConfig() => new()
    {
        CoverageText  = _coverageBox.Text,
        RequiredText  = _requiredBox.Text,
        PreferredText = _preferredBox.Text,
        ExcludedText  = _excludedBox.Text,
        GroupsText    = _groupsBox.Text,
        Mode          = (AdjacencyMode)Math.Max(0, _modeCombo.SelectedIndex)
    };

    public void ApplyConfig(OptimizationConfig cfg)
    {
        _coverageBox.Text  = cfg.CoverageText;
        _requiredBox.Text  = cfg.RequiredText;
        _preferredBox.Text = cfg.PreferredText;
        _excludedBox.Text  = cfg.ExcludedText;
        _groupsBox.Text    = cfg.GroupsText;
        _modeCombo.SelectedIndex = (int)cfg.Mode;
    }

    public void AppendCoverage(int pid)
    {
        var text = _coverageBox.Text ?? "";
        // Используем Environment.NewLine (\r\n), чтобы TextBox корректно отобразил перенос
        if (text.Length > 0 && !text.EndsWith(Environment.NewLine))
        {
            text += Environment.NewLine;
        }
        text += $"province, {pid}";
        _coverageBox.Text = text;
        _coverageBox.SelectionStart = _coverageBox.TextLength;
        _coverageBox.ScrollToCaret();
    }

    public void AppendRequired(int pid)
    {
        var text = (_requiredBox.Text ?? "").TrimEnd();
        if (text.Length > 0) text += ", ";
        text += pid.ToString();
        _requiredBox.Text = text;
        _requiredBox.SelectionStart = _requiredBox.TextLength;
        _requiredBox.ScrollToCaret();
    }

    public void ShowSolution(HashSet<int> forts, Dictionary<int, Province> provinceById)
    {
        var sorted = forts.OrderBy(x => x).ToList();

        var ids   = new StringBuilder();
        var names = new StringBuilder();
        foreach (var pid in sorted)
        {
            ids.AppendLine(pid.ToString());
            var name = provinceById.TryGetValue(pid, out var p) ? p.Name : Loc.T("info.unknown");
            names.AppendLine(name);
        }
        _idsBox.Text   = ids.ToString();
        _namesBox.Text = names.ToString();

        _selectedCountLabel.Text = Loc.T("opt.output.count", sorted.Count);
    }

    public void ClearSolution()
    {
        _idsBox.Text = "";
        _namesBox.Text = "";
        _selectedCountLabel.Text = Loc.T("opt.output.count", 0);
    }

    // ---------- вёрстка ----------

    private static TextBox MakeMultiline() => new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Height = 72
    };

    private static TextBox MakeReadonlyMultiline() => new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        WordWrap = false
    };

    private static Label MakeTitle() => new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        AutoSize = false
    };

    private static Label MakeHint() => new()
    {
        Dock = DockStyle.Top,
        Height = 16,
        ForeColor = Color.DimGray,
        Font = new Font(SystemFonts.DefaultFont.FontFamily,
            SystemFonts.DefaultFont.Size - 1)
    };

    private static Button MakePreviewButton() => new()
    {
        Width = 140,
        Height = 26
    };

    /// <summary>
    /// Блок параметра:
    ///   строка 0: заголовок на всю ширину
    ///   строка 1, колонка 0: кнопка предпросмотра
    ///   строка 1, колонка 1: TextBox, под ним — hint
    /// </summary>
    private static void AddParameterBlock(
        TableLayoutPanel parent,
        Label title, Label hint, Button previewBtn, TextBox field)
    {
        var block = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 2,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 6, 0, 6)
        };
        block.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        block.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        block.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        block.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));

        block.Controls.Add(title, 0, 0);
        block.SetColumnSpan(title, 2);

        previewBtn.Dock = DockStyle.Top;
        block.Controls.Add(previewBtn, 0, 1);

        var fieldPanel = new Panel { Dock = DockStyle.Fill };
        fieldPanel.Controls.Add(hint);
        field.Dock = DockStyle.Top;
        field.Height = 72;
        fieldPanel.Controls.Add(field);
        block.Controls.Add(fieldPanel, 1, 1);

        parent.Controls.Add(block);
    }
}