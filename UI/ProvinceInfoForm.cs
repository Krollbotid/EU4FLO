using System.Drawing;
using EU4FLO.Localization;

namespace EU4FLO;

public enum ProvinceKind { Land, Water, Wasteland }

public sealed class ProvinceInfoForm : Form
{
    private readonly Label _header;
    private readonly Label _idLabel;
    private readonly TextBox _idBox;
    private readonly Label _nameLabel;
    private readonly TextBox _nameBox;
    private readonly Label _areaLabel;
    private readonly TextBox _areaBox;
    private readonly Label _regionLabel;
    private readonly TextBox _regionBox;
    private readonly Label _superLabel;
    private readonly TextBox _superBox;
    private readonly Label _statusLabel;

    private readonly FlowLayoutPanel _fortPanel;
    private readonly Label _fortTextLabel;
    private readonly CheckBox _fortCheck;

    private readonly FlowLayoutPanel _optPanel;
    private readonly Button _addToPlanBtn;
    private readonly Button _addToRequiredBtn;
    private readonly Label _neighborsLabel;
    private readonly ListBox _neighbors;

    private int _currentPid = -1;
    private bool _updating;
    private bool _optimizationMode;
    private bool _isLandCurrent;

    public event Action<int>? NeighborSelected;
    public event Action<int, bool>? FortToggled;
    public event Action<int>? AddToCoverageRequested;
    public event Action<int>? AddToRequiredRequested;

    public ProvinceInfoForm()
    {
        Width = 380;
        Height = 740;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.Manual;
        MinimizeBox = false;

        _header = new Label
        {
            Dock = DockStyle.Top, Height = 26,
            Padding = new Padding(8, 5, 0, 0),
            Font = new Font(Font, FontStyle.Bold)
        };

        _idLabel   = MakeFieldLabel();
        _idBox     = MakeReadonlyBox();
        _nameLabel = MakeFieldLabel();
        _nameBox   = MakeReadonlyBox();
        _areaLabel = MakeFieldLabel();
        _areaBox   = MakeReadonlyBox();
        _regionLabel = MakeFieldLabel();
        _regionBox   = MakeReadonlyBox();
        _superLabel = MakeFieldLabel();
        _superBox   = MakeReadonlyBox();

        _statusLabel = new Label
        {
            Dock = DockStyle.Top, Height = 24,
            Padding = new Padding(8, 5, 0, 0),
            Font = new Font(Font, FontStyle.Italic),
            ForeColor = Color.DimGray,
            Visible = false
        };

        // === Фикс перекрытия: FlowLayoutPanel вместо ручного позиционирования ===
        _fortPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(8, 2, 0, 0),
            WrapContents = false,
            AutoSize = false,
            FlowDirection = FlowDirection.LeftToRight
        };
        _fortTextLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 6, 4, 0),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _fortCheck = new CheckBox
        {
            AutoSize = true,
            Margin = new Padding(0, 5, 0, 0)
        };
        _fortCheck.CheckedChanged += (_, _) =>
        {
            if (_updating) return;
            if (_currentPid >= 0)
                FortToggled?.Invoke(_currentPid, _fortCheck.Checked);
        };
        _fortPanel.Controls.Add(_fortTextLabel);
        _fortPanel.Controls.Add(_fortCheck);

        _optPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 36,
            Padding = new Padding(8, 4, 0, 0),
            WrapContents = false, Visible = false
        };
        _addToPlanBtn     = new Button { Width = 170, Height = 26 };
        _addToRequiredBtn = new Button { Width = 180, Height = 26 };
        _addToPlanBtn.Click += (_, _) =>
        {
            if (_currentPid >= 0) AddToCoverageRequested?.Invoke(_currentPid);
        };
        _addToRequiredBtn.Click += (_, _) =>
        {
            if (_currentPid >= 0) AddToRequiredRequested?.Invoke(_currentPid);
        };
        _optPanel.Controls.Add(_addToPlanBtn);
        _optPanel.Controls.Add(_addToRequiredBtn);

        _neighborsLabel = new Label
        {
            Dock = DockStyle.Top, Height = 22,
            Padding = new Padding(8, 5, 0, 0)
        };

        _neighbors = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        _neighbors.DoubleClick += (_, _) =>
        {
            if (_neighbors.SelectedItem is NeighborEntry ne)
                NeighborSelected?.Invoke(ne.Id);
        };

        // Dock=Top: порядок добавления — снизу вверх
        Controls.Add(_neighbors);
        Controls.Add(_neighborsLabel);
        Controls.Add(_optPanel);
        Controls.Add(_fortPanel);
        Controls.Add(_statusLabel);
        Controls.Add(_superBox);
        Controls.Add(_superLabel);
        Controls.Add(_regionBox);
        Controls.Add(_regionLabel);
        Controls.Add(_areaBox);
        Controls.Add(_areaLabel);
        Controls.Add(_nameBox);
        Controls.Add(_nameLabel);
        Controls.Add(_idBox);
        Controls.Add(_idLabel);
        Controls.Add(_header);

        Loc.LanguageChanged += ApplyLocalization;
        FormClosed += (_, _) => Loc.LanguageChanged -= ApplyLocalization;

        ApplyLocalization();
    }

    private static Label MakeFieldLabel() => new()
    {
        Dock = DockStyle.Top, Height = 18,
        Padding = new Padding(8, 2, 0, 0),
        ForeColor = Color.DimGray
    };

    private static TextBox MakeReadonlyBox() => new()
    {
        Dock = DockStyle.Top,
        ReadOnly = true,
        BorderStyle = BorderStyle.FixedSingle
    };

    private sealed record NeighborEntry(int Id, string Name)
    {
        public override string ToString() => $"{Id} — {Name}";
    }

    public void ApplyLocalization()
    {
        Text = _currentPid >= 0 ? Loc.T("info.title.pid", _currentPid) : Loc.T("info.title.default");
        _idLabel.Text      = Loc.T("info.field.id");
        _nameLabel.Text    = Loc.T("info.field.name");
        _areaLabel.Text    = Loc.T("info.field.area");
        _regionLabel.Text  = Loc.T("info.field.region");
        _superLabel.Text   = Loc.T("info.field.superregion");
        _neighborsLabel.Text = Loc.T("info.neighbors");
        _fortTextLabel.Text  = Loc.T("info.fort");

        _addToPlanBtn.Text     = Loc.T("info.btn.add_coverage");
        _addToRequiredBtn.Text = Loc.T("info.btn.add_required");
    }

    public void SetOptimizationMode(bool enabled)
    {
        _optimizationMode = enabled;
        _optPanel.Visible = enabled && _isLandCurrent && _currentPid >= 0;
    }

    public void ShowProvince(
        int pid, Province? province,
        IReadOnlyCollection<int>? neighbors,
        Dictionary<int, Province> provinceById,
        bool hasFort,
        ProvinceKind kind,
        string? areaName, string? regionName, string? superregionName)
    {
        _updating = true;
        try
        {
            _currentPid = pid;
            Text = pid >= 0 ? Loc.T("info.title.pid", pid) : Loc.T("info.title.default");
            _header.Text = pid >= 0 ? Loc.T("info.header") : Loc.T("info.no_selection");

            _idBox.Text   = pid >= 0 ? pid.ToString() : "";
            _nameBox.Text = pid >= 0 ? (province?.Name ?? Loc.T("info.unknown")) : "";
            _areaBox.Text   = pid >= 0 ? (areaName       ?? "—") : "";
            _regionBox.Text = pid >= 0 ? (regionName     ?? "—") : "";
            _superBox.Text  = pid >= 0 ? (superregionName ?? "—") : "";

            _isLandCurrent = kind == ProvinceKind.Land;
            bool isLand = _isLandCurrent;

            _statusLabel.Visible = !isLand;
            _statusLabel.Text = kind switch
            {
                ProvinceKind.Water     => Loc.T("info.status.water"),
                ProvinceKind.Wasteland => Loc.T("info.status.wasteland"),
                _                      => ""
            };

            _fortPanel.Visible = isLand;
            _fortCheck.Enabled = isLand && pid >= 0;
            _fortCheck.Checked = hasFort;

            _optPanel.Visible = isLand && _optimizationMode && pid >= 0;

            _neighborsLabel.Visible = isLand;
            _neighbors.Visible      = isLand;

            _neighbors.BeginUpdate();
            _neighbors.Items.Clear();
            if (isLand && neighbors != null)
                foreach (var nid in neighbors.OrderBy(x => x))
                {
                    var name = provinceById.TryGetValue(nid, out var np) ? np.Name : Loc.T("info.unknown");
                    _neighbors.Items.Add(new NeighborEntry(nid, name));
                }
            _neighbors.EndUpdate();
        }
        finally { _updating = false; }
    }
}