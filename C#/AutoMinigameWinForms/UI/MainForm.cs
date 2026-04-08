using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using AutoMinigameWinForms.Core;
using AutoMinigameWinForms.Models;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using CvPoint = OpenCvSharp.Point;
using CvRect = OpenCvSharp.Rect;

namespace AutoMinigameWinForms;

public sealed class MainForm : Form
{
    private const int WmHotKey = 0x0312;
    private const int StartHotkeyId = 0x5A01;
    private const double RuntimeRevalidateIntervalSec = 300.0;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private static readonly string[] AllowedHotkeyKeys =
    [
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"
    ];

    private readonly string _configPath;
    private readonly AppConfig _runtimeConfig;
    private readonly LicenseService _licenseService;
    private readonly SimpleConfig _cfg = AppConstants.CreateDefaultDetectorConfig();
    private readonly OcrEngine _ocrEngine = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly System.Windows.Forms.Timer _timer = new();

    private bool _scanning;
    private double _lastPress;
    private double _lastAttempt;
    private double _lastDetect;
    private double _lastRender;
    private double _lastOcrMs;
    private double _lastRegionMs;
    private OcrResult _lastOcr = new(null, 0.0, "w:0.00 a:0.00", new CvRect(0, 0, 0, 0));
    private OcrResult _lastValidOcr = new(null, 0.0, "w:0.00 a:0.00", new CvRect(0, 0, 0, 0));
    private double _lastValidOcrMs;
    private int _overlapStreak;
    private int _clearStreak;
    private bool _pressArmed = true;
    private bool _wasOverlapping;
    private double _touchWindowUntilMs;
    private string _lastOcrKeyStable = string.Empty;
    private int _ocrSameKeyStreak;
    private double? _prevFrameDiff;
    private nint? _targetHwnd;
    private Rectangle? _cachedRegion;
    private (int capX, int capY, int capW, int capH, int scanX, int scanY, int tolX10)? _lastCfgTuple;
    private (int miniX, int miniY, int miniW, int miniH)? _miniLastTuple;
    private string _lastWindowTitle = string.Empty;
    private bool _usingFallbackRegion;
    private double _lastLicenseRevalidateAtSec;
    private double _lastOcrDebugLogMs;

    private int _hit;
    private Keys _startHotkey = Keys.F6;
    private uint _startHotkeyModifier;
    private bool _hotkeyRegistered;
    private bool _toggleBusy;
    private bool _isRuntimeRevalidating;
    private bool _debugAllLogs = true;
    private double _ocrMinScore = AppConstants.OcrMinScore;
    private double _ocrMinMargin = AppConstants.OcrMinMargin;

    private MiniPreviewForm? _miniPreview;
    private bool _captureEnabled = true;

    private Label _statusLabel = null!;
    private TextBox _licenseInfoBox = null!;
    private Label _summaryLabel = null!;
    private Label _detailLabel = null!;
    private TextBox _logBox = null!;

    private NumericUpDown _spCapX = null!;
    private NumericUpDown _spCapY = null!;
    private NumericUpDown _spCapW = null!;
    private NumericUpDown _spCapH = null!;
    private NumericUpDown _spScanX = null!;
    private NumericUpDown _spScanY = null!;
    private NumericUpDown _spOcrX = null!;
    private NumericUpDown _spOcrY = null!;
    private NumericUpDown _spOcrW = null!;
    private NumericUpDown _spOcrH = null!;
    private NumericUpDown _spOcrScore = null!;
    private NumericUpDown _spOcrMargin = null!;
    private NumericUpDown _spTol = null!;
    private ComboBox _cbMode = null!;
    private NumericUpDown _spMiniX = null!;
    private NumericUpDown _spMiniY = null!;
    private NumericUpDown _spMiniW = null!;
    private NumericUpDown _spMiniH = null!;
    private Button _btnStart = null!;
    private Button _btnCapture = null!;
    private Button _btnDebugLogs = null!;
    private CheckBox _chkAlwaysOnTop = null!;
    private ComboBox _cbStartHotkey = null!;
    private ComboBox _cbHotkeyModifier = null!;
    private ComboBox _cbDebugLogs = null!;
    private TextBox _tbWindowTitle = null!;
    private TextBox _tbHotkey = null!;

    public MainForm(string configPath, AppConfig runtimeConfig, LicenseService licenseService)
    {
        _configPath = configPath;
        _runtimeConfig = runtimeConfig;
        _licenseService = licenseService;
        BuildUi();
        BuildMiniCapture();
        LoadConfigFile();
        SyncCfg();

        _timer.Interval = AppConstants.UpdateMs;
        _timer.Tick += (_, _) => Loop();
        _timer.Start();
    }

    private void BuildUi()
    {
        Text = "ZCUS MINIGAME V1.0.1";
        ApplyAppIcon();
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        SetBounds(100, 80, 620, 760);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        MinimumSize = new System.Drawing.Size(620, 760);
        MaximumSize = new System.Drawing.Size(620, 760);
        BackColor = Color.FromArgb(0x12, 0x12, 0x12);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(8),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var leftHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0),
        };

        var headerIcon = new PictureBox
        {
            Width = 22,
            Height = 22,
            SizeMode = PictureBoxSizeMode.StretchImage,
            Margin = new Padding(0, 4, 8, 0),
            BackColor = Color.Transparent,
        };
        if (Icon is not null)
        {
            headerIcon.Image = Icon.ToBitmap();
        }

        var title = new Label
        {
            Text = "ZCUS MINIGAME V1.0.1",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0x00, 0xFF, 0xCC),
            Padding = new Padding(0, 1, 0, 4),
            AutoSize = true,
        };
        leftHeader.Controls.Add(headerIcon);
        leftHeader.Controls.Add(title);
        layout.Controls.Add(leftHeader, 0, 0);

        _statusLabel = new Label
        {
            Text = "Status: Idle",
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(255, 220, 120),
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 6),
        };
        layout.Controls.Add(_statusLabel, 0, 1);

        _spCapX = NewSpin(-1000, 1000, _cfg.CaptureOffsetX);
        _spCapY = NewSpin(-1000, 1000, _cfg.CaptureOffsetY);
        _spCapW = NewSpin(80, 1200, _cfg.CaptureWidth > 0 ? _cfg.CaptureWidth : _cfg.CaptureSize);
        _spCapH = NewSpin(80, 1200, _cfg.CaptureHeight > 0 ? _cfg.CaptureHeight : _cfg.CaptureSize);
        _spScanX = NewSpin(-300, 300, _cfg.ScanCenterOffsetX);
        _spScanY = NewSpin(-300, 300, _cfg.ScanCenterOffsetY);
        _spOcrX = NewSpin(-200, 200, AppConstants.OcrOffsetX);
        _spOcrY = NewSpin(-200, 200, AppConstants.OcrOffsetY);
        _spOcrW = NewSpin(12, 220, AppConstants.OcrBoxW);
        _spOcrH = NewSpin(12, 220, AppConstants.OcrBoxH);
        _spOcrScore = NewSpin(0, 100, (int)Math.Round(AppConstants.OcrMinScore * 100.0));
        _spOcrMargin = NewSpin(0, 100, (int)Math.Round(AppConstants.OcrMinMargin * 100.0));
        _spTol = NewSpin(5, 400, (int)(_cfg.AngleToleranceDeg * 10));

        _cbMode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 90,
        };
        _cbMode.Items.AddRange(["WASD", "AUTO", "1234"]);
        _cbMode.SelectedItem = AppConstants.OcrModeDefault;
        _cbStartHotkey = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 90,
        };
        _cbDebugLogs = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 90,
        };
        _cbDebugLogs.Items.AddRange(["ON", "OFF"]);
        _cbDebugLogs.SelectedItem = "ON";
        _cbDebugLogs.SelectedIndexChanged += (_, _) =>
        {
            _debugAllLogs = string.Equals(_cbDebugLogs.SelectedItem?.ToString(), "ON", StringComparison.OrdinalIgnoreCase);
        };
        _cbHotkeyModifier = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 110,
        };
        _cbHotkeyModifier.Items.AddRange(["None", "Ctrl", "Alt", "Shift", "Ctrl+Alt", "Ctrl+Shift", "Alt+Shift", "Ctrl+Alt+Shift"]);
        _cbHotkeyModifier.SelectedItem = AppConstants.DefaultStartHotkeyModifier;
        _cbStartHotkey.Items.AddRange(AllowedHotkeyKeys);
        _cbStartHotkey.SelectedItem = AppConstants.DefaultStartHotkey;
        _tbHotkey = new TextBox
        {
            Width = 150,
            ReadOnly = true,
            ShortcutsEnabled = false,
            Text = "Click and press keybind",
        };
        _tbHotkey.KeyDown += HotkeyBoxKeyDown;
        _tbHotkey.Enter += (_, _) => _tbHotkey.BackColor = Color.FromArgb(34, 36, 44);
        _tbHotkey.Leave += (_, _) =>
        {
            _tbHotkey.BackColor = Color.FromArgb(24, 24, 30);
            _tbHotkey.Text = BuildHotkeyDisplay(NormalizeHotkeyModifier(_cbHotkeyModifier.SelectedItem?.ToString() ?? AppConstants.DefaultStartHotkeyModifier), NormalizeHotkey(_cbStartHotkey.SelectedItem?.ToString() ?? AppConstants.DefaultStartHotkey));
        };

        _tbWindowTitle = new TextBox
        {
            Width = 128,
            Text = AppConstants.WindowTitle,
        };
        _tbWindowTitle.TextChanged += (_, _) =>
        {
            _cachedRegion = null;
            _targetHwnd = null;
        };

        var btnUseActiveWindow = new Button
        {
            Text = "Use Active",
            Width = 74,
            Height = 24,
            AutoSize = false,
        };
        StyleButton(btnUseActiveWindow, ButtonTone.Secondary);
        btnUseActiveWindow.Click += (_, _) => ApplyActiveWindowTarget();

        var windowTargetPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        windowTargetPanel.Controls.Add(_tbWindowTitle);
        windowTargetPanel.Controls.Add(btnUseActiveWindow);
        _cbHotkeyModifier.SelectedIndexChanged += (_, _) =>
        {
            var modifierText = (_cbHotkeyModifier.SelectedItem?.ToString() ?? AppConstants.DefaultStartHotkeyModifier).Trim();
            var hotkeyText = (_cbStartHotkey.SelectedItem?.ToString() ?? AppConstants.DefaultStartHotkey).Trim();
            ApplyStartHotkey(modifierText, hotkeyText);
        };
        _cbStartHotkey.SelectedIndexChanged += (_, _) =>
        {
            var modifierText = (_cbHotkeyModifier.SelectedItem?.ToString() ?? AppConstants.DefaultStartHotkeyModifier).Trim();
            var hotkeyText = (_cbStartHotkey.SelectedItem?.ToString() ?? AppConstants.DefaultStartHotkey).Trim();
            ApplyStartHotkey(modifierText, hotkeyText);
        };

        _spMiniX = NewSpin(0, 4000, AppConstants.MiniGuiX);
        _spMiniY = NewSpin(0, 4000, AppConstants.MiniGuiY);
        _spMiniW = NewSpin(80, 800, AppConstants.MiniGuiW);
        _spMiniH = NewSpin(80, 800, AppConstants.MiniGuiH);
        StyleSpin(_spCapX);
        StyleSpin(_spCapY);
        StyleSpin(_spCapW);
        StyleSpin(_spCapH);
        StyleSpin(_spScanX);
        StyleSpin(_spScanY);
        StyleSpin(_spOcrX);
        StyleSpin(_spOcrY);
        StyleSpin(_spOcrW);
        StyleSpin(_spOcrH);
        StyleSpin(_spOcrScore);
        StyleSpin(_spOcrMargin);
        StyleSpin(_spTol);
        StyleSpin(_spMiniX);
        StyleSpin(_spMiniY);
        StyleSpin(_spMiniW);
        StyleSpin(_spMiniH);
        StyleCombo(_cbMode);
        StyleCombo(_cbDebugLogs);
        StyleCombo(_cbHotkeyModifier);
        StyleCombo(_cbStartHotkey);
        StyleTextBox(_tbWindowTitle);
        StyleTextBox(_tbHotkey);

        var settingsPanel = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoScroll = false,
            Padding = new Padding(0),
        };
        var settingsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0),
        };
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        settingsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        settingsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        settingsGrid.Controls.Add(CreateSettingsGroup("Capture", [
            ("TARGET", windowTargetPanel),
            ("CAP X", _spCapX), ("CAP Y", _spCapY), ("CAP W", _spCapW), ("CAP H", _spCapH),
            ("MINI X", _spMiniX), ("MINI Y", _spMiniY), ("MINI W", _spMiniW), ("MINI H", _spMiniH)
        ]), 0, 0);

        settingsGrid.Controls.Add(CreateSettingsGroup("Scan", [
            ("SCAN X", _spScanX), ("SCAN Y", _spScanY), ("Tol x10", _spTol), ("Mode", _cbMode), ("Debug", _cbDebugLogs), ("Keybind", _tbHotkey)
        ]), 1, 0);

        settingsGrid.Controls.Add(CreateSettingsGroup("OCR", [
            ("OCR X", _spOcrX), ("OCR Y", _spOcrY), ("OCR W", _spOcrW), ("OCR H", _spOcrH),
            ("Score%", _spOcrScore), ("Margin%", _spOcrMargin)
        ]), 0, 1);
        settingsGrid.SetColumnSpan(settingsGrid.GetControlFromPosition(0, 1)!, 2);

        settingsPanel.Controls.Add(settingsGrid);
        layout.Controls.Add(settingsPanel, 0, 2);

        var actionBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 6),
        };
        actionBar.RowCount = 2;
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        _btnStart = new Button
        {
            Text = "START",
            AutoSize = true,
            Padding = new Padding(8, 6, 8, 6),
        };
        StyleButton(_btnStart, ButtonTone.Primary);
        SetStartButtonStyle(false);
        _btnStart.Click += async (_, _) => await ToggleScanAsync();

        _btnCapture = new Button
        {
            Text = "Capture: ON",
            AutoSize = true,
            Padding = new Padding(8, 6, 8, 6),
        };
        StyleButton(_btnCapture, ButtonTone.Secondary);
        _btnCapture.Click += (_, _) => ToggleCapturePreview();

        _btnDebugLogs = new Button
        {
            Text = _debugAllLogs ? "Debug: ON" : "Debug: OFF",
            AutoSize = true,
            Padding = new Padding(8, 6, 8, 6),
        };
        StyleButton(_btnDebugLogs, ButtonTone.Secondary);
        _btnDebugLogs.Click += (_, _) =>
        {
            _debugAllLogs = !_debugAllLogs;
            _btnDebugLogs.Text = _debugAllLogs ? "Debug: ON" : "Debug: OFF";
            _cbDebugLogs.SelectedItem = _debugAllLogs ? "ON" : "OFF";
            AppendLog($"Debug logs: {(_debugAllLogs ? "ON" : "OFF")}");
        };

        _chkAlwaysOnTop = new CheckBox
        {
            Text = "Always On Top",
            AutoSize = false,
            Width = 130,
            Height = 32,
            Checked = false,
            Margin = new Padding(0, 0, 6, 0),
        };
        StyleCheckBox(_chkAlwaysOnTop);
        _chkAlwaysOnTop.CheckedChanged += (_, _) => ApplyAlwaysOnTop(_chkAlwaysOnTop.Checked);

        var btnCopy = new Button { Text = "Copy Log", AutoSize = true };
        StyleButton(btnCopy, ButtonTone.Secondary);
        btnCopy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(_logBox.Text ?? string.Empty);
                AppendLog($"Log copied ({_logBox.Lines.Length} lines)");
            }
            catch (Exception ex)
            {
                AppendLog($"Copy log failed: {ex.Message}");
            }
        };

        var btnClear = new Button { Text = "Clear Log", AutoSize = true };
        StyleButton(btnClear, ButtonTone.Warn);
        btnClear.Click += (_, _) => ClearLog();

        var btnSaveCfg = new Button { Text = "Save Config", AutoSize = true };
        StyleButton(btnSaveCfg, ButtonTone.Secondary);
        btnSaveCfg.Click += (_, _) => SaveConfigFile();

        var btnLoadCfg = new Button { Text = "Load Config", AutoSize = true };
        StyleButton(btnLoadCfg, ButtonTone.Secondary);
        btnLoadCfg.Click += (_, _) => LoadConfigFile();

        _btnStart.Dock = DockStyle.Fill;
        _btnCapture.Dock = DockStyle.Fill;
        _btnDebugLogs.Dock = DockStyle.Fill;
        _chkAlwaysOnTop.Dock = DockStyle.Fill;
        btnSaveCfg.Dock = DockStyle.Fill;
        btnLoadCfg.Dock = DockStyle.Fill;
        btnCopy.Dock = DockStyle.Fill;
        btnClear.Dock = DockStyle.Fill;

        actionBar.Controls.Add(_btnStart, 0, 0);
        actionBar.Controls.Add(_btnCapture, 1, 0);
        actionBar.Controls.Add(_btnDebugLogs, 2, 0);
        actionBar.Controls.Add(_chkAlwaysOnTop, 3, 0);
        actionBar.Controls.Add(btnSaveCfg, 0, 1);
        actionBar.Controls.Add(btnLoadCfg, 1, 1);
        actionBar.Controls.Add(btnCopy, 2, 1);
        actionBar.Controls.Add(btnClear, 3, 1);
        layout.Controls.Add(actionBar, 0, 3);

        _summaryLabel = new Label
        {
            Text = "Total Press=0",
            Dock = DockStyle.Top,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 4),
        };
        layout.Controls.Add(_summaryLabel, 0, 4);

        _detailLabel = new Label
        {
            Text = "red=- blue=- diff=- key=-",
            Dock = DockStyle.Top,
            AutoSize = true,
            ForeColor = Color.FromArgb(180, 180, 180),
            Padding = new Padding(0, 0, 0, 4),
            Visible = false,
        };
        layout.Controls.Add(_detailLabel, 0, 5);

        var bottomPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0),
        };
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));

        _logBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ForeColor = Color.FromArgb(0x00, 0xFF, 0x88),
            Font = new Font("Consolas", 9F),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 6, 0),
        };

        _licenseInfoBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.None,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 24, 32),
            ForeColor = Color.FromArgb(185, 220, 255),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0),
            WordWrap = true,
        };

        bottomPanel.Controls.Add(_logBox, 0, 0);
        bottomPanel.Controls.Add(_licenseInfoBox, 1, 0);
        layout.Controls.Add(bottomPanel, 0, 6);

        Controls.Add(layout);
        RefreshLicenseInfo();
        AppendLog("System: Ready. Klik START untuk mulai detect.");
    }

    private void BuildMiniCapture()
    {
        _miniPreview = new MiniPreviewForm(AppConstants.MiniGuiX, AppConstants.MiniGuiY, AppConstants.MiniGuiW, AppConstants.MiniGuiH);
        _miniPreview.CaptureDrag += (_, args) => MoveCaptureByDrag(args.DeltaX, args.DeltaY);
        _miniPreview.Show();
        _miniPreview.TopMost = _chkAlwaysOnTop is not null && _chkAlwaysOnTop.Checked;
        _miniPreview.Visible = _captureEnabled;
    }

    private static NumericUpDown NewSpin(int min, int max, int value)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Max(min, Math.Min(max, value)),
            Width = 78,
        };
    }

    private static void AddGridPair(TableLayoutPanel grid, int row, int col, string label, Control control)
    {
        while (grid.RowStyles.Count <= row)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var lbl = new Label
        {
            Text = label,
            Anchor = AnchorStyles.Left,
            AutoSize = true,
        };
        control.Anchor = AnchorStyles.Left;

        grid.Controls.Add(lbl, col, row);
        grid.Controls.Add(control, col + 1, row);
    }

    private GroupBox CreateSettingsGroup(string title, (string label, Control control)[] fields)
    {
        var group = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Top,
            AutoSize = true,
            ForeColor = Color.FromArgb(195, 205, 225),
            Font = new Font("Segoe UI Semibold", 8.7F, FontStyle.Bold),
            Padding = new Padding(8, 6, 8, 6),
            Margin = new Padding(0, 0, 0, 4),
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            Padding = new Padding(0),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        for (var i = 0; i < fields.Length; i++)
        {
            var row = i / 2;
            var pairCol = (i % 2) * 2;
            while (grid.RowStyles.Count <= row)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            var (labelText, control) = fields[i];
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = Color.FromArgb(205, 205, 205),
                Margin = new Padding(0, 3, 6, 3),
                Font = new Font("Segoe UI", 8.2F, FontStyle.Regular),
            };
            control.Anchor = AnchorStyles.Left;
            control.Margin = new Padding(0, 1, 8, 1);

            grid.Controls.Add(label, pairCol, row);
            grid.Controls.Add(control, pairCol + 1, row);
        }

        group.Controls.Add(grid);
        return group;
    }

    private void SetStartButtonStyle(bool running)
    {
        if (running)
        {
            StyleButton(_btnStart, ButtonTone.Danger);
            _btnStart.Text = "STOP";
            return;
        }

        StyleButton(_btnStart, ButtonTone.Primary);
        _btnStart.Text = "START";
    }

    private void ToggleCapturePreview()
    {
        _captureEnabled = !_captureEnabled;
        _runtimeConfig.CaptureEnabled = _captureEnabled;
        _btnCapture.Text = _captureEnabled ? "Capture: ON" : "Capture: OFF";
        if (_miniPreview is not null)
        {
            _miniPreview.Visible = _captureEnabled;
        }
    }

    private void ApplyAlwaysOnTop(bool enabled)
    {
        TopMost = false;
        _runtimeConfig.AlwaysOnTop = enabled;
        _chkAlwaysOnTop.BackColor = enabled ? Color.FromArgb(0, 165, 220) : Color.FromArgb(58, 58, 68);
        _chkAlwaysOnTop.Text = enabled ? "Mini Always On Top: ON" : "Mini Always On Top: OFF";
        if (_miniPreview is not null)
        {
            _miniPreview.TopMost = enabled;
        }
    }

    private void MoveCaptureByDrag(int dx, int dy)
    {
        if (dx == 0 && dy == 0)
        {
            return;
        }

        SetSpin(_spCapX, (int)_spCapX.Value + dx);
        SetSpin(_spCapY, (int)_spCapY.Value + dy);
        _cachedRegion = null;
    }

    private async Task ToggleScanAsync()
    {
        if (_toggleBusy)
        {
            return;
        }

        if (!_scanning)
        {
            _toggleBusy = true;
            _btnStart.Enabled = false;
            _statusLabel.Text = "Status: Validating license...";
            try
            {
                var check = await _licenseService.RevalidateStoredLicenseAsync(_configPath, _runtimeConfig);
                if (!check.ok)
                {
                    _statusLabel.Text = "Status: License invalid";
                    SetStartButtonStyle(false);
                    RefreshLicenseInfo();
                    MessageBox.Show(
                        this,
                        $"License invalid: {check.message}",
                        "License",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                _lastLicenseRevalidateAtSec = _clock.Elapsed.TotalSeconds;
                _scanning = true;
                _overlapStreak = 0;
                _clearStreak = 0;
                _pressArmed = true;
                _wasOverlapping = false;
                _touchWindowUntilMs = 0;
                _lastOcrKeyStable = string.Empty;
                _ocrSameKeyStreak = 0;
                _lastValidOcr = new OcrResult(null, 0.0, "w:0.00 a:0.00", new CvRect(0, 0, 0, 0));
                _lastValidOcrMs = 0;
                _prevFrameDiff = null;
                _statusLabel.Text = "Status: Tracking...";
                SetStartButtonStyle(true);
                RefreshLicenseInfo();
                AppendLog("Bot: Started");
                return;
            }
            finally
            {
                _btnStart.Enabled = true;
                _toggleBusy = false;
            }
        }

        _scanning = false;
        _overlapStreak = 0;
        _clearStreak = 0;
        _pressArmed = true;
        _wasOverlapping = false;
        _touchWindowUntilMs = 0;
        _lastOcrKeyStable = string.Empty;
        _ocrSameKeyStreak = 0;
        _lastValidOcr = new OcrResult(null, 0.0, "w:0.00 a:0.00", new CvRect(0, 0, 0, 0));
        _lastValidOcrMs = 0;
        _prevFrameDiff = null;
        _statusLabel.Text = "Status: Idle";
        SetStartButtonStyle(false);
        RefreshLicenseInfo();
        AppendLog("Bot: Stopped");
    }

    private void ClearLog()
    {
        _hit = 0;
        _logBox.Clear();
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        _summaryLabel.Text = $"Total Press={_hit}";
    }

    private void RefreshLicenseInfo()
    {
        var owner = string.IsNullOrWhiteSpace(_runtimeConfig.LicenseOwner) ? "-" : _runtimeConfig.LicenseOwner;
        var key = string.IsNullOrWhiteSpace(_runtimeConfig.LicenseKey)
            ? "-"
            : $"{_runtimeConfig.LicenseKey[..Math.Min(4, _runtimeConfig.LicenseKey.Length)]}...{_runtimeConfig.LicenseKey[^Math.Min(4, _runtimeConfig.LicenseKey.Length)..]}";
        var exp = string.IsNullOrWhiteSpace(_runtimeConfig.LicenseExpiresAt) ? "-" : _runtimeConfig.LicenseExpiresAt;
        var verify = string.IsNullOrWhiteSpace(_runtimeConfig.LicenseLastVerifiedAt) ? "-" : _runtimeConfig.LicenseLastVerifiedAt;
        if (_licenseInfoBox is null)
        {
            return;
        }

        _licenseInfoBox.Text =
            "LICENSE INFO" + Environment.NewLine +
            $"User: {owner}" + Environment.NewLine +
            $"Key: {key}" + Environment.NewLine +
            $"Expired: {exp}" + Environment.NewLine +
            $"Last Verify: {verify}";
    }

    private void ShowLicenseInfoDialog()
    {
        var owner = string.IsNullOrWhiteSpace(_runtimeConfig.LicenseOwner) ? "-" : _runtimeConfig.LicenseOwner;
        var key = string.IsNullOrWhiteSpace(_runtimeConfig.LicenseKey) ? "-" : _runtimeConfig.LicenseKey;
        var machine = string.IsNullOrWhiteSpace(_runtimeConfig.MachineId) ? "-" : _runtimeConfig.MachineId;
        var exp = string.IsNullOrWhiteSpace(_runtimeConfig.LicenseExpiresAt) ? "-" : _runtimeConfig.LicenseExpiresAt;
        var verify = string.IsNullOrWhiteSpace(_runtimeConfig.LicenseLastVerifiedAt) ? "-" : _runtimeConfig.LicenseLastVerifiedAt;
        var status = string.IsNullOrWhiteSpace(_runtimeConfig.LicenseLastMessage) ? "-" : _runtimeConfig.LicenseLastMessage;

        var message =
            $"User: {owner}\n" +
            $"License Key: {key}\n" +
            $"Machine ID: {machine}\n" +
            $"Expired: {exp}\n" +
            $"Last Verify: {verify}\n" +
            $"Status: {status}";

        MessageBox.Show(this, message, "License Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private enum ButtonTone
    {
        Primary,
        Secondary,
        Warn,
        Danger,
    }

    private static void StyleButton(Button button, ButtonTone tone)
    {
        button.AutoSize = false;
        button.Height = 28;
        button.MinimumSize = new System.Drawing.Size(84, 28);
        button.Margin = new Padding(3);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold);
        button.BackColor = tone switch
        {
            ButtonTone.Primary => Color.FromArgb(0, 165, 220),
            ButtonTone.Warn => Color.FromArgb(214, 145, 36),
            ButtonTone.Danger => Color.FromArgb(194, 67, 67),
            _ => Color.FromArgb(58, 58, 68),
        };
    }

    private static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.Appearance = Appearance.Button;
        checkBox.TextAlign = ContentAlignment.MiddleCenter;
        checkBox.FlatStyle = FlatStyle.Flat;
        checkBox.FlatAppearance.BorderSize = 0;
        checkBox.ForeColor = Color.White;
        checkBox.BackColor = Color.FromArgb(58, 58, 68);
        checkBox.Font = new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold);
        checkBox.Margin = new Padding(3);
    }

    private static void StyleSpin(NumericUpDown spin)
    {
        spin.BackColor = Color.FromArgb(24, 24, 30);
        spin.ForeColor = Color.FromArgb(235, 235, 235);
        spin.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void StyleCombo(ComboBox combo)
    {
        combo.BackColor = Color.FromArgb(24, 24, 30);
        combo.ForeColor = Color.FromArgb(235, 235, 235);
        combo.FlatStyle = FlatStyle.Popup;
    }

    private static void StyleTextBox(TextBox box)
    {
        box.BackColor = Color.FromArgb(24, 24, 30);
        box.ForeColor = Color.FromArgb(235, 235, 235);
        box.BorderStyle = BorderStyle.FixedSingle;
    }

    private void ApplyActiveWindowTarget()
    {
        if (!WindowFinder.TryGetForegroundWindowInfo(out var win) || win is null)
        {
            AppendLog("Window target: gagal ambil window aktif.");
            return;
        }

        _tbWindowTitle.Text = win.Title.Trim();
        AppendLog($"Window target: {win.Title}");
    }

    private void HotkeyBoxKeyDown(object? sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        if (!TryExtractHotkeyFromKeyEvent(e, out var modifierText, out var hotkeyText))
        {
            _tbHotkey.Text = "Use A-Z / 0-9 / F1-F12";
            return;
        }

        ApplyStartHotkey(modifierText, hotkeyText);
        _tbHotkey.Text = BuildHotkeyDisplay(modifierText, hotkeyText);
    }

    private static bool TryExtractHotkeyFromKeyEvent(KeyEventArgs e, out string modifierText, out string hotkeyText)
    {
        hotkeyText = KeyCodeToHotkeyText(e.KeyCode);
        if (string.IsNullOrWhiteSpace(hotkeyText) || !TryParseHotkey(hotkeyText, out _))
        {
            modifierText = AppConstants.DefaultStartHotkeyModifier;
            hotkeyText = AppConstants.DefaultStartHotkey;
            return false;
        }

        var parts = new List<string>(3);
        if (e.Control)
        {
            parts.Add("Ctrl");
        }

        if (e.Alt)
        {
            parts.Add("Alt");
        }

        if (e.Shift)
        {
            parts.Add("Shift");
        }

        modifierText = parts.Count == 0 ? "None" : string.Join("+", parts);
        return true;
    }

    private static string KeyCodeToHotkeyText(Keys keyCode)
    {
        if (keyCode is >= Keys.A and <= Keys.Z)
        {
            return keyCode.ToString().ToUpperInvariant();
        }

        if (keyCode is >= Keys.D0 and <= Keys.D9)
        {
            return ((int)(keyCode - Keys.D0)).ToString();
        }

        if (keyCode is >= Keys.F1 and <= Keys.F24)
        {
            return keyCode.ToString().ToUpperInvariant();
        }

        return string.Empty;
    }

    private static string BuildHotkeyDisplay(string modifierText, string hotkeyText)
    {
        return modifierText == "None" ? hotkeyText : $"{modifierText}+{hotkeyText}";
    }

    private void ApplyAppIcon()
    {
        try
        {
            var path = ResolveAppIconPath();
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                Icon = new Icon(path);
                return;
            }

            var exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon is not null)
            {
                Icon = exeIcon;
            }
        }
        catch
        {
            // Ignore icon load issues in runtime.
        }
    }

    private static string ResolveAppIconPath()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(local))
        {
            return local;
        }

        var project = Path.Combine(Directory.GetCurrentDirectory(), "app.ico");
        if (File.Exists(project))
        {
            return project;
        }

        return string.Empty;
    }

    private void AppendLog(string line)
    {
        const int maxLines = 120;
        var existingLines = _logBox.Lines;
        if (existingLines.Length >= maxLines)
        {
            var kept = existingLines.Skip(existingLines.Length - maxLines + 1).ToArray();
            _logBox.Lines = kept;
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
        }

        if (_logBox.TextLength > 0)
        {
            _logBox.AppendText(Environment.NewLine);
        }

        _logBox.AppendText(line);
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    private void SyncCfg()
    {
        _ocrMinScore = Math.Clamp((double)_spOcrScore.Value / 100.0, 0.0, 1.0);
        _ocrMinMargin = Math.Clamp((double)_spOcrMargin.Value / 100.0, 0.0, 1.0);

        var windowTitle = NormalizeWindowTitle(_tbWindowTitle.Text);
        if (!string.Equals(_lastWindowTitle, windowTitle, StringComparison.Ordinal))
        {
            _lastWindowTitle = windowTitle;
            _cfg.WindowTitle = windowTitle;
            _targetHwnd = null;
            _cachedRegion = null;
        }

        var cfgTuple = (
            (int)_spCapX.Value,
            (int)_spCapY.Value,
            (int)_spCapW.Value,
            (int)_spCapH.Value,
            (int)_spScanX.Value,
            (int)_spScanY.Value,
            (int)_spTol.Value
        );

        if (_lastCfgTuple != cfgTuple)
        {
            _lastCfgTuple = cfgTuple;
            _cfg.CaptureOffsetX = cfgTuple.Item1;
            _cfg.CaptureOffsetY = cfgTuple.Item2;
            _cfg.CaptureWidth = cfgTuple.Item3;
            _cfg.CaptureHeight = cfgTuple.Item4;
            _cfg.CaptureSize = Math.Min(cfgTuple.Item3, cfgTuple.Item4);
            _cfg.ScanCenterOffsetX = cfgTuple.Item5;
            _cfg.ScanCenterOffsetY = cfgTuple.Item6;
            _cfg.AngleToleranceDeg = cfgTuple.Item7 / 10.0;
            _cachedRegion = null;
        }

        var miniTuple = ((int)_spMiniX.Value, (int)_spMiniY.Value, (int)_spMiniW.Value, (int)_spMiniH.Value);
        if (_miniLastTuple != miniTuple)
        {
            _miniLastTuple = miniTuple;
            ApplyMiniGeometry(miniTuple.Item1, miniTuple.Item2, miniTuple.Item3, miniTuple.Item4);
        }
    }

    private void SetPreview(Mat bgr)
    {
        if (_miniPreview is null || !_captureEnabled || !_miniPreview.Visible)
        {
            return;
        }

        var targetW = Math.Max(2, _miniPreview.ClientSize.Width);
        var targetH = Math.Max(2, _miniPreview.ClientSize.Height);

        using var resized = new Mat();
        Cv2.Resize(bgr, resized, new OpenCvSharp.Size(targetW, targetH), interpolation: InterpolationFlags.Nearest);
        using var bitmap = BitmapConverter.ToBitmap(resized);
        _miniPreview.SetFrame((Bitmap)bitmap.Clone());
    }

    private void ApplyMiniGeometry(int x, int y, int w, int h)
    {
        _miniPreview?.ApplyGeometry(x, y, w, h);
    }

    private AppConfig BuildConfigFromUi()
    {
        return new AppConfig
        {
            WindowTitle = NormalizeWindowTitle(_tbWindowTitle.Text),
            CaptureOffsetX = (int)_spCapX.Value,
            CaptureOffsetY = (int)_spCapY.Value,
            CaptureWidth = (int)_spCapW.Value,
            CaptureHeight = (int)_spCapH.Value,
            ScanCenterOffsetX = (int)_spScanX.Value,
            ScanCenterOffsetY = (int)_spScanY.Value,
            OcrOffsetX = (int)_spOcrX.Value,
            OcrOffsetY = (int)_spOcrY.Value,
            OcrBoxW = (int)_spOcrW.Value,
            OcrBoxH = (int)_spOcrH.Value,
            OcrMinScoreX100 = (int)_spOcrScore.Value,
            OcrMinMarginX100 = (int)_spOcrMargin.Value,
            AngleToleranceX10 = (int)_spTol.Value,
            OcrMode = (_cbMode.SelectedItem?.ToString() ?? AppConstants.OcrModeDefault).Trim(),
            MiniX = (int)_spMiniX.Value,
            MiniY = (int)_spMiniY.Value,
            MiniW = (int)_spMiniW.Value,
            MiniH = (int)_spMiniH.Value,
            CaptureEnabled = _captureEnabled,
            AlwaysOnTop = _chkAlwaysOnTop.Checked,
            DebugAllLogs = _debugAllLogs,
            StartHotkeyModifier = (_cbHotkeyModifier.SelectedItem?.ToString() ?? AppConstants.DefaultStartHotkeyModifier).Trim(),
            StartHotkey = (_cbStartHotkey.SelectedItem?.ToString() ?? AppConstants.DefaultStartHotkey).Trim(),
        };
    }

    private void ApplyConfig(AppConfig cfg)
    {
        SetSpin(_spCapX, cfg.CaptureOffsetX);
        SetSpin(_spCapY, cfg.CaptureOffsetY);
        SetSpin(_spCapW, cfg.CaptureWidth > 0 ? cfg.CaptureWidth : _cfg.CaptureSize);
        SetSpin(_spCapH, cfg.CaptureHeight > 0 ? cfg.CaptureHeight : _cfg.CaptureSize);
        SetSpin(_spScanX, cfg.ScanCenterOffsetX);
        SetSpin(_spScanY, cfg.ScanCenterOffsetY);
        SetSpin(_spOcrX, cfg.OcrOffsetX);
        SetSpin(_spOcrY, cfg.OcrOffsetY);
        SetSpin(_spOcrW, cfg.OcrBoxW);
        SetSpin(_spOcrH, cfg.OcrBoxH);
        SetSpin(_spOcrScore, cfg.OcrMinScoreX100);
        SetSpin(_spOcrMargin, cfg.OcrMinMarginX100);
        SetSpin(_spTol, cfg.AngleToleranceX10);

        var mode = string.IsNullOrWhiteSpace(cfg.OcrMode) ? AppConstants.OcrModeDefault : cfg.OcrMode.ToUpperInvariant();
        if (mode is "AUTO" or "WASD" or "1234")
        {
            _cbMode.SelectedItem = mode;
        }

        SetSpin(_spMiniX, cfg.MiniX);
        SetSpin(_spMiniY, cfg.MiniY);
        SetSpin(_spMiniW, cfg.MiniW);
        SetSpin(_spMiniH, cfg.MiniH);

        _captureEnabled = cfg.CaptureEnabled;
        _btnCapture.Text = _captureEnabled ? "Capture: ON" : "Capture: OFF";
        if (_miniPreview is not null)
        {
            _miniPreview.Visible = _captureEnabled;
        }

        _tbWindowTitle.Text = NormalizeWindowTitle(cfg.WindowTitle);
        _cfg.WindowTitle = NormalizeWindowTitle(cfg.WindowTitle);
        _lastWindowTitle = _cfg.WindowTitle;
        _debugAllLogs = cfg.DebugAllLogs;
        _cbDebugLogs.SelectedItem = _debugAllLogs ? "ON" : "OFF";
        if (_btnDebugLogs is not null)
        {
            _btnDebugLogs.Text = _debugAllLogs ? "Debug: ON" : "Debug: OFF";
        }
        _chkAlwaysOnTop.Checked = cfg.AlwaysOnTop;
        ApplyAlwaysOnTop(cfg.AlwaysOnTop);
        var hotkeyModifier = string.IsNullOrWhiteSpace(cfg.StartHotkeyModifier) ? AppConstants.DefaultStartHotkeyModifier : cfg.StartHotkeyModifier;
        var hotkeyText = string.IsNullOrWhiteSpace(cfg.StartHotkey) ? AppConstants.DefaultStartHotkey : cfg.StartHotkey.ToUpperInvariant();
        _cbHotkeyModifier.SelectedItem = hotkeyModifier;
        _cbStartHotkey.SelectedItem = hotkeyText;
        ApplyStartHotkey(hotkeyModifier, hotkeyText);
        _tbHotkey.Text = BuildHotkeyDisplay(NormalizeHotkeyModifier(hotkeyModifier), NormalizeHotkey(hotkeyText));
        RefreshLicenseInfo();
    }

    private static void SetSpin(NumericUpDown spin, int value)
    {
        var clamped = Math.Max((int)spin.Minimum, Math.Min((int)spin.Maximum, value));
        spin.Value = clamped;
    }

    private void SaveConfigFile()
    {
        try
        {
            var merged = BuildConfigFromUi();
            var existing = ConfigStore.Load(_configPath);
            merged.LicenseKey = existing.LicenseKey;
            merged.Software = existing.Software;
            merged.MachineId = existing.MachineId;
            merged.LicenseOwner = existing.LicenseOwner;
            merged.LicenseExpiresAt = existing.LicenseExpiresAt;
            merged.LicenseLastVerifiedAt = existing.LicenseLastVerifiedAt;
            merged.LicenseLastMessage = existing.LicenseLastMessage;
            ConfigStore.Save(_configPath, merged);
            AppendLog($"Config saved: {Path.GetFileName(_configPath)}");
        }
        catch (Exception ex)
        {
            AppendLog($"Config save failed: {ex.Message}");
        }
    }

    private void LoadConfigFile()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                AppendLog($"Config not found: {Path.GetFileName(_configPath)} (using defaults)");
                return;
            }

            var cfg = ConfigStore.Load(_configPath);
            ApplyConfig(cfg);
            AppendLog($"Config loaded: {Path.GetFileName(_configPath)}");
        }
        catch (Exception ex)
        {
            AppendLog($"Config load failed: {ex.Message}");
        }
    }

    private Rectangle? GetStableRegion(double nowMs)
    {
        if (_cachedRegion is not null && (nowMs - _lastRegionMs) < AppConstants.WindowRefreshMs)
        {
            return _cachedRegion;
        }

        var windows = WindowFinder.GetWindowsWithTitle(_cfg.WindowTitle)
            .Where(w => w.Bounds.Width > 0 && w.Bounds.Height > 0)
            .ToList();

        if (windows.Count == 0)
        {
            _targetHwnd = null;
            var screen = Screen.PrimaryScreen;
            if (screen is null)
            {
                _cachedRegion = null;
                return null;
            }

            var capW = _cfg.CaptureWidth > 0 ? _cfg.CaptureWidth : _cfg.CaptureSize;
            var capH = _cfg.CaptureHeight > 0 ? _cfg.CaptureHeight : _cfg.CaptureSize;
            var cx = screen.Bounds.Left + (screen.Bounds.Width / 2) + _cfg.CaptureOffsetX;
            var cy = screen.Bounds.Top + (screen.Bounds.Height / 2) + _cfg.CaptureOffsetY;

            _cachedRegion = new Rectangle(
                cx - (capW / 2),
                cy - (capH / 2),
                Math.Max(2, capW),
                Math.Max(2, capH)
            );
            _lastRegionMs = nowMs;
            _usingFallbackRegion = true;
            return _cachedRegion;
        }

        WindowInfo? selected = null;
        if (_targetHwnd.HasValue)
        {
            selected = windows.FirstOrDefault(w => w.Handle == _targetHwnd.Value);
        }

        selected ??= windows.OrderByDescending(w => w.Area).First();
        _targetHwnd = selected.Handle;
        _usingFallbackRegion = false;

        var width = _cfg.CaptureWidth > 0 ? _cfg.CaptureWidth : _cfg.CaptureSize;
        var height = _cfg.CaptureHeight > 0 ? _cfg.CaptureHeight : _cfg.CaptureSize;
        var centerX = selected.Bounds.Left + (selected.Bounds.Width / 2) + _cfg.CaptureOffsetX;
        var centerY = selected.Bounds.Top + (selected.Bounds.Height / 2) + _cfg.CaptureOffsetY;

        _cachedRegion = new Rectangle(
            centerX - (width / 2),
            centerY - (height / 2),
            Math.Max(2, width),
            Math.Max(2, height)
        );
        _lastRegionMs = nowMs;
        return _cachedRegion;
    }

    private static Mat CaptureRegion(Rectangle region)
    {
        var safe = new Rectangle(region.X, region.Y, Math.Max(2, region.Width), Math.Max(2, region.Height));
        using var bmp = new Bitmap(safe.Width, safe.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(safe.Left, safe.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        return BitmapConverter.ToMat(bmp);
    }

    private void Loop()
    {
        SyncCfg();

        var now = _clock.Elapsed.TotalSeconds;
        var nowMs = _clock.Elapsed.TotalMilliseconds;

        var idleRenderFps = AppConstants.MaxIdleRenderFps > 0 ? AppConstants.MaxIdleRenderFps : 30;

        if (!_scanning)
        {
            var needIdleRender = (now - _lastRender) >= (1.0 / idleRenderFps);
            if (!needIdleRender)
            {
                return;
            }

            var region = GetStableRegion(nowMs);
            if (region is null)
            {
                _detailLabel.Text = "window not found/minimized";
                _statusLabel.Text = "Status: Window not found";
                return;
            }

            using var frame = CaptureRegion(region.Value);
            _lastRender = now;
            SetPreview(frame);
            _statusLabel.Text = "Status: Idle";
            _detailLabel.Text = _usingFallbackRegion
                ? "preview desktop fallback (window not found)"
                : "preview only (START untuk detect)";
            return;
        }

        _statusLabel.Text = "Status: Tracking...";

        if ((now - _lastLicenseRevalidateAtSec) >= RuntimeRevalidateIntervalSec && !_isRuntimeRevalidating)
        {
            _ = RuntimeRevalidateAsync(now);
        }

        var needDetect = AppConstants.MaxDetectFps <= 0 || (now - _lastDetect) >= (1.0 / AppConstants.MaxDetectFps);

        var needRender = AppConstants.MaxRenderFps <= 0 || (now - _lastRender) >= (1.0 / AppConstants.MaxRenderFps);
        if (!needDetect && !needRender)
        {
            return;
        }

        var stableRegion = GetStableRegion(nowMs);
        if (stableRegion is null)
        {
            _detailLabel.Text = "window not found/minimized";
            _statusLabel.Text = "Status: Window not found";
            return;
        }

        if (_usingFallbackRegion)
        {
            _statusLabel.Text = $"Status: Waiting target window ({_cfg.WindowTitle})";
            if (needRender)
            {
                _lastRender = now;
                using var fallbackFrame = CaptureRegion(stableRegion.Value);
                SetPreview(fallbackFrame);
            }
            return;
        }

        using var frameScan = CaptureRegion(stableRegion.Value);

        if (!needDetect)
        {
            if (needRender)
            {
                _lastRender = now;
                SetPreview(frameScan);
            }

            return;
        }

        _lastDetect = now;

        using var result = MinigameDetector.ProcessFrame(frameScan, _cfg);

        var mode = (_cbMode.SelectedItem?.ToString() ?? AppConstants.OcrModeDefault).Trim().ToUpperInvariant();
        var allowedKeys = mode switch
        {
            "WASD" => AppConstants.WasdTemplateKeys,
            "1234" => AppConstants.DigitTemplateKeys,
            _ => AppConstants.AllTemplateKeys,
        };
        if ((nowMs - _lastOcrMs) >= AppConstants.OcrIntervalMs)
        {
            var ocrNow = _ocrEngine.DetectWhiteLetterLineStyle(
                frameScan,
                result.Center,
                (int)_spOcrX.Value,
                (int)_spOcrY.Value,
                (int)_spOcrW.Value,
                (int)_spOcrH.Value,
                _ocrMinScore,
                _ocrMinMargin,
                allowedKeys
            );

            _lastOcr = ocrNow;
            if (IsAllowedAndMapped(ocrNow.Key, allowedKeys))
            {
                _lastValidOcr = ocrNow;
                _lastValidOcrMs = nowMs;
            }

            _lastOcrMs = nowMs;
        }

        var ocr = _lastOcr;
        var usingFallbackOcr = false;
        if (!IsAllowedAndMapped(ocr.Key, allowedKeys) && _lastValidOcrMs > 0 && (nowMs - _lastValidOcrMs) <= AppConstants.OcrHoldMs)
        {
            var lastStillAllowed = IsAllowedAndMapped(_lastValidOcr.Key, allowedKeys);
            if (lastStillAllowed)
            {
                ocr = _lastValidOcr;
                usingFallbackOcr = true;
            }
        }

        var key = ocr.Key;
        var score = ocr.Score;
        var margin = ocr.Margin;
        var dbg = ocr.Debug;
        var ocrBox = ocr.Box;

        var scoreOk = score >= _ocrMinScore;
        var marginOk = margin >= _ocrMinMargin;

        var normalizedKey = (key ?? string.Empty).Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedKey) && normalizedKey == _lastOcrKeyStable)
        {
            _ocrSameKeyStreak++;
        }
        else
        {
            _lastOcrKeyStable = normalizedKey;
            _ocrSameKeyStreak = string.IsNullOrWhiteSpace(normalizedKey) ? 0 : 1;
        }
        var keyStable = _ocrSameKeyStreak >= AppConstants.OcrRequireStableReads;
        var centerDiffDeg = 999.0;
        var hasCenterDiff = result.RedAngle.HasValue
            && TryGetBlueArcCenterDiff(result.RedAngle.Value, result.BlueAngles, _cfg.BlueStepDeg, out centerDiffDeg);

        var redAngleText = result.RedAngle.HasValue ? result.RedAngle.Value.ToString("0.0") : "-";
        var diffText = result.BestDiff.HasValue ? result.BestDiff.Value.ToString("0.0") : "-";
        var centerDiffText = hasCenterDiff ? centerDiffDeg.ToString("0.0") : "-";
        _detailLabel.Text =
            $"red={redAngleText} blue={result.BlueAngles.Count} diff={diffText} cDiff={centerDiffText} overlap={(result.Overlap ? "Y" : "N")} mode={mode} src={(usingFallbackOcr ? "hold" : "live")} key={key ?? "-"}({score:0.00}) m={margin:0.00} st={_ocrSameKeyStreak}/{AppConstants.OcrRequireStableReads} {dbg}";

        var strictDiffLimit = Math.Min(AppConstants.PressStrictMaxDiffDeg, _cfg.AngleToleranceDeg * AppConstants.PressStrictTolRatio);
        var triggerDiffLimit = Math.Min(AppConstants.PressTriggerDiffDeg, strictDiffLimit);
        var isKeyOk = IsAllowedAndMapped(key, allowedKeys);
        var isTimingOk = result.BestDiff.HasValue && result.BestDiff.Value <= strictDiffLimit;
        var isTriggerDiffOk = result.BestDiff.HasValue && result.BestDiff.Value <= triggerDiffLimit;
        var fallbackCenterLimit = Math.Max(2.2, strictDiffLimit * 0.35);
        var isCenterOk = hasCenterDiff
            ? centerDiffDeg <= AppConstants.PressCenterMaxDiffDeg
            : (result.BestDiff.HasValue && result.BestDiff.Value <= fallbackCenterLimit);
        var isApproachingCenter = !result.BestDiff.HasValue
            || !_prevFrameDiff.HasValue
            || result.BestDiff.Value <= (_prevFrameDiff.Value + 0.35);
        var crossedTriggerBand = result.BestDiff.HasValue
            && _prevFrameDiff.HasValue
            && _prevFrameDiff.Value > triggerDiffLimit
            && result.BestDiff.Value <= triggerDiffLimit;
        var isStableFrame = result.Overlap && isTimingOk && isKeyOk && scoreOk && keyStable;
        var justTouched = result.Overlap && !_wasOverlapping;
        if (justTouched)
        {
            _touchWindowUntilMs = nowMs + AppConstants.PressTouchWindowMs;
        }
        var isInTouchWindow = result.Overlap && nowMs <= _touchWindowUntilMs;

        if (isStableFrame)
        {
            _overlapStreak++;
            _clearStreak = 0;
        }
        else
        {
            _overlapStreak = 0;
            _clearStreak++;
            if (_clearStreak >= AppConstants.PressRearmClearFrames)
            {
                _pressArmed = true;
            }
            if (!result.Overlap)
            {
                _touchWindowUntilMs = 0;
                _prevFrameDiff = null;
            }
        }
        var keyToPress = AppConstants.AutoPressUseOcrKey && isKeyOk ? key?.Trim().ToUpperInvariant() : null;

        var canPress =
            AppConstants.AutoPressOnOverlap &&
            (crossedTriggerBand || (result.Overlap && isTimingOk && isInTouchWindow)) &&
            isTriggerDiffOk &&
            isKeyOk &&
            scoreOk &&
            keyStable &&
            (isCenterOk || _overlapStreak >= 2) &&
            _overlapStreak >= AppConstants.PressRequireStableFrames &&
            _pressArmed &&
            (now - _lastAttempt) >= AppConstants.AttemptIntervalSec &&
            (now - _lastPress) >= AppConstants.PressDelaySec;

        _statusLabel.Text = _scanning
            ? "Status: Tracking..."
            : "Status: Idle";

        if (_debugAllLogs && (nowMs - _lastOcrDebugLogMs) >= 250)
        {
            var rejectReason = string.Join(',',
                new[]
                {
                    scoreOk ? null : "low-score",
                    marginOk ? null : "low-margin",
                    keyStable ? null : "unstable-key",
                    isKeyOk ? null : "bad-key",
                    isTimingOk ? null : "bad-timing",
                    isTriggerDiffOk ? null : "bad-trigger-diff",
                    isCenterOk ? null : "off-center",
                    isApproachingCenter ? null : "away-center",
                    (crossedTriggerBand || isInTouchWindow) ? null : "no-trigger",
                }.Where(x => x is not null));

            AppendLog(
                $"OCR dbg | mode={mode} src={(usingFallbackOcr ? "hold" : "live")} overlap={(result.Overlap ? "Y" : "N")} touch={(isInTouchWindow ? "Y" : "N")} cross={(crossedTriggerBand ? "Y" : "N")} key={(key ?? "-").ToUpperInvariant()} score={score:0.000}/{_ocrMinScore:0.000} m={margin:0.000}/{_ocrMinMargin:0.000} stable={_ocrSameKeyStreak}/{AppConstants.OcrRequireStableReads} amb={(ocr.IsAmbiguous ? "Y" : "N")} diff={diffText}/{triggerDiffLimit:0.0} cDiff={centerDiffText}/{AppConstants.PressCenterMaxDiffDeg:0.0} reject={(string.IsNullOrWhiteSpace(rejectReason) ? "-" : rejectReason)} | {dbg}");
            _lastOcrDebugLogMs = nowMs;
        }

        if (canPress)
        {
            if (!string.IsNullOrWhiteSpace(keyToPress))
            {
                NativeInput.PressKey(keyToPress);
                _lastPress = now;
                _lastAttempt = now;
                _pressArmed = false;
                _lastValidOcrMs = 0;
                _touchWindowUntilMs = 0;

                _hit++;

                AppendLog(
                    $"[{DateTime.Now:HH:mm:ss}] CLICK {keyToPress.ToUpperInvariant()} | total={_hit} score={score:0.000} diff={diffText}");
                RefreshSummary();
                _overlapStreak = 0;
            }
        }

        if (result.BestDiff.HasValue)
        {
            _prevFrameDiff = result.BestDiff.Value;
        }
        _wasOverlapping = result.Overlap;

        if (needRender)
        {
            _lastRender = now;
            using var output = AppConstants.DrawOverlay ? frameScan.Clone() : frameScan;

            if (AppConstants.DrawOverlay)
            {
                Cv2.Circle(output, result.Center, 2, new Scalar(255, 255, 255), -1);
                if (result.RedPoint.HasValue)
                {
                    Cv2.Circle(output, result.RedPoint.Value, 4, new Scalar(0, 0, 255), -1);
                    Cv2.Line(output, result.Center, result.RedPoint.Value, new Scalar(0, 0, 255), 1);
                }

                using var ringBlue = BlueRingMask(result.BlueMask, result.Center);
                if (Cv2.CountNonZero(ringBlue) >= 2)
                {
                    using var nz = new Mat();
                    Cv2.FindNonZero(ringBlue, nz);
                    if (!nz.Empty())
                    {
                        var box = Cv2.BoundingRect(nz);
                        Cv2.Rectangle(output, box, new Scalar(255, 0, 0), 1);
                    }
                }

                var x2 = ocrBox.X + ocrBox.Width;
                var y2 = ocrBox.Y + ocrBox.Height;
                Cv2.Rectangle(output, new CvPoint(ocrBox.X, ocrBox.Y), new CvPoint(x2, y2), new Scalar(0, 255, 255), 1);
            }

            SetPreview(output);
        }
    }

    private static bool IsAllowedKey(string? key, IReadOnlyCollection<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return allowed.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsAllowedAndMapped(string? key, IReadOnlyCollection<string> allowed)
    {
        return IsAllowedKey(key, allowed) && key is not null && AppConstants.VkMap.ContainsKey(key);
    }

    private static bool TryGetBlueArcCenterDiff(double redAngleDeg, IReadOnlyList<double> blueAngles, double stepDeg, out double centerDiffDeg)
    {
        centerDiffDeg = 999.0;
        if (blueAngles.Count == 0)
        {
            return false;
        }

        var sorted = blueAngles
            .Select(NormalizeDeg)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (sorted.Count == 0)
        {
            return false;
        }

        var red = NormalizeDeg(redAngleDeg);
        var nearestIndex = 0;
        var nearestDelta = 999.0;
        for (var i = 0; i < sorted.Count; i++)
        {
            var d = MinigameDetector.CircularDelta(red, sorted[i]);
            if (d < nearestDelta)
            {
                nearestDelta = d;
                nearestIndex = i;
            }
        }

        var allowedGap = Math.Max(0.6, stepDeg * 1.6);
        var selected = new List<double> { sorted[nearestIndex] };

        var idx = nearestIndex;
        while (true)
        {
            var prev = (idx - 1 + sorted.Count) % sorted.Count;
            var gap = ForwardDelta(sorted[prev], sorted[idx]);
            if (gap > allowedGap || selected.Contains(sorted[prev]))
            {
                break;
            }

            selected.Add(sorted[prev]);
            idx = prev;
        }

        idx = nearestIndex;
        while (true)
        {
            var next = (idx + 1) % sorted.Count;
            var gap = ForwardDelta(sorted[idx], sorted[next]);
            if (gap > allowedGap || selected.Contains(sorted[next]))
            {
                break;
            }

            selected.Add(sorted[next]);
            idx = next;
        }

        var anchor = sorted[nearestIndex];
        var unwrapped = selected.Select(a => UnwrapAround(a, anchor)).ToList();
        var minA = unwrapped.Min();
        var maxA = unwrapped.Max();
        var center = NormalizeDeg((minA + maxA) / 2.0);

        centerDiffDeg = MinigameDetector.CircularDelta(red, center);
        return true;
    }

    private static double NormalizeDeg(double deg)
    {
        var v = deg % 360.0;
        if (v < 0.0)
        {
            v += 360.0;
        }

        return v;
    }

    private static double ForwardDelta(double fromDeg, double toDeg)
    {
        var d = NormalizeDeg(toDeg) - NormalizeDeg(fromDeg);
        if (d < 0.0)
        {
            d += 360.0;
        }

        return d;
    }

    private static double UnwrapAround(double angleDeg, double anchorDeg)
    {
        var x = NormalizeDeg(angleDeg);
        var a = NormalizeDeg(anchorDeg);
        while ((x - a) > 180.0)
        {
            x -= 360.0;
        }

        while ((a - x) > 180.0)
        {
            x += 360.0;
        }

        return x;
    }

    private Mat BlueRingMask(Mat blueMask, CvPoint center)
    {
        var ring = Mat.Zeros(blueMask.Size(), MatType.CV_8UC1).ToMat();
        var minDim = Math.Min(blueMask.Rows, blueMask.Cols);
        var rIn = Math.Max(1, (int)(minDim * _cfg.BlueRMinRatio));
        var rOut = Math.Max(rIn + 1, (int)(minDim * _cfg.BlueRMaxRatio));

        Cv2.Circle(ring, center, rOut, Scalar.White, -1);
        Cv2.Circle(ring, center, rIn, Scalar.Black, -1);

        var output = new Mat();
        Cv2.BitwiseAnd(blueMask, ring, output);
        ring.Dispose();
        return output;
    }

    private async Task RuntimeRevalidateAsync(double now)
    {
        _isRuntimeRevalidating = true;
        try
        {
            var check = await _licenseService.RevalidateStoredLicenseAsync(_configPath, _runtimeConfig);
            _lastLicenseRevalidateAtSec = now;
            RefreshLicenseInfo();
            if (check.ok)
            {
                return;
            }

            _scanning = false;
            _statusLabel.Text = "Status: License invalid";
            SetStartButtonStyle(false);
            AppendLog($"License invalid: {check.message}");
            MessageBox.Show(
                this,
                $"License invalid: {check.message}",
                "License",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _isRuntimeRevalidating = false;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyStartHotkey(
            (_cbHotkeyModifier.SelectedItem?.ToString() ?? AppConstants.DefaultStartHotkeyModifier).Trim(),
            (_cbStartHotkey.SelectedItem?.ToString() ?? AppConstants.DefaultStartHotkey).Trim());
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey && m.WParam.ToInt32() == StartHotkeyId)
        {
            _ = ToggleScanAsync();
            return;
        }

        base.WndProc(ref m);
    }

    private void ApplyStartHotkey(string modifierText, string hotkeyText)
    {
        var normalizedModifier = NormalizeHotkeyModifier(modifierText);
        var normalized = NormalizeHotkey(hotkeyText);
        if (!TryParseHotkey(normalized, out var key) || !TryParseHotkeyModifier(normalizedModifier, out var modifier))
        {
            return;
        }

        if (_hotkeyRegistered)
        {
            UnregisterHotKey(Handle, StartHotkeyId);
            _hotkeyRegistered = false;
        }

        _startHotkey = key;
        _startHotkeyModifier = modifier;
        _hotkeyRegistered = RegisterHotKey(Handle, StartHotkeyId, _startHotkeyModifier, (uint)_startHotkey);
        EnsureHotkeyItemExists(normalized);
        if ((_cbHotkeyModifier.SelectedItem?.ToString() ?? string.Empty) != normalizedModifier)
        {
            _cbHotkeyModifier.SelectedItem = normalizedModifier;
        }
        if ((_cbStartHotkey.SelectedItem?.ToString() ?? string.Empty) != normalized)
        {
            _cbStartHotkey.SelectedItem = normalized;
        }

        if (_tbHotkey is not null)
        {
            _tbHotkey.Text = BuildHotkeyDisplay(normalizedModifier, normalized);
        }
    }

    private void EnsureHotkeyItemExists(string hotkeyText)
    {
        if (!_cbStartHotkey.Items.Contains(hotkeyText))
        {
            _cbStartHotkey.Items.Add(hotkeyText);
        }
    }

    private static string NormalizeWindowTitle(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? AppConstants.WindowTitle : value.Trim();
    }

    private static string NormalizeHotkey(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? AppConstants.DefaultStartHotkey : value.Trim().ToUpperInvariant();

        if (text.Length == 1 && text[0] is >= 'A' and <= 'Z')
        {
            return text;
        }

        if (text.Length == 1 && text[0] is >= '0' and <= '9')
        {
            return text;
        }

        if (text.StartsWith("F", StringComparison.Ordinal)
            && int.TryParse(text[1..], out var fn)
            && fn is >= 1 and <= 24)
        {
            return $"F{fn}";
        }

        return AppConstants.DefaultStartHotkey;
    }

    private static string NormalizeHotkeyModifier(string value)
    {
        var text = string.IsNullOrWhiteSpace(value)
            ? AppConstants.DefaultStartHotkeyModifier
            : value.Trim().Replace(" ", string.Empty);
        return text switch
        {
            "Ctrl" => "Ctrl",
            "Alt" => "Alt",
            "Shift" => "Shift",
            "Ctrl+Alt" => "Ctrl+Alt",
            "Ctrl+Shift" => "Ctrl+Shift",
            "Alt+Shift" => "Alt+Shift",
            "Ctrl+Alt+Shift" => "Ctrl+Alt+Shift",
            _ => "None",
        };
    }

    private static bool TryParseHotkey(string value, out Keys key)
    {
        if (value.Length == 1 && value[0] is >= 'A' and <= 'Z'
            && Enum.TryParse(value, true, out Keys alpha))
        {
            key = alpha;
            return true;
        }

        if (value.Length == 1 && value[0] is >= '0' and <= '9')
        {
            key = Keys.D0 + (value[0] - '0');
            return true;
        }

        if (Enum.TryParse(value, true, out Keys parsed) && parsed is >= Keys.F1 and <= Keys.F24)
        {
            key = parsed;
            return true;
        }

        key = Keys.F6;
        return false;
    }

    private static bool TryParseHotkeyModifier(string value, out uint modifier)
    {
        modifier = value switch
        {
            "Ctrl" => ModControl,
            "Alt" => ModAlt,
            "Shift" => ModShift,
            "Ctrl+Alt" => ModControl | ModAlt,
            "Ctrl+Shift" => ModControl | ModShift,
            "Alt+Shift" => ModAlt | ModShift,
            "Ctrl+Alt+Shift" => ModControl | ModAlt | ModShift,
            "None" => 0u,
            _ => 0u,
        };

        return true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            SaveConfigFile();
        }
        catch
        {
            // Ignore config write issues at shutdown.
        }

        _timer.Stop();
        if (_hotkeyRegistered)
        {
            UnregisterHotKey(Handle, StartHotkeyId);
            _hotkeyRegistered = false;
        }
        _miniPreview?.Close();
        _ocrEngine.Dispose();

        base.OnFormClosing(e);
    }
}











