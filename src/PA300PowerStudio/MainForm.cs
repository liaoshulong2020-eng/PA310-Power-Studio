using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows.Forms.DataVisualization.Charting;

namespace PA300UpperMachineFull;

public sealed class MainForm : Form
{
    private readonly AppSettings _settings;

    private readonly ComboBox _cmbConnType = new() { DropDownStyle = ComboBoxStyle.DropDownList, MaximumSize = new Size(150, 0) };
    private readonly TextBox _txtIp = new() { MaximumSize = new Size(150, 0) };
    private readonly NumericUpDown _numPort = new() { Minimum = 1, Maximum = 65535, MaximumSize = new Size(120, 0) };
    private readonly ComboBox _cmbCom = new() { DropDownStyle = ComboBoxStyle.DropDownList, MaximumSize = new Size(120, 0) };
    private readonly ComboBox _cmbBaud = new() { DropDownStyle = ComboBoxStyle.DropDownList, MaximumSize = new Size(120, 0) };
    private readonly Button _btnRefreshPorts = new() { Text = "刷新设备" };
    private readonly Button _btnDiagnose = new() { Text = "连接诊断" };
    private readonly Button _btnForceRelease = new() { Text = "强制释放USB", ForeColor = Color.DarkRed };
    private readonly Button _btnRepairDriver = new() { Text = "修复驱动" };
    private readonly Label _lblDevice = new() { AutoSize = true, Text = "等待扫描设备" };

    private readonly NumericUpDown _numInterval = new() { Minimum = 50, Maximum = 5000, Increment = 50 };
    private readonly CheckBox _chkAutoReconnect = new() { Text = "掉线自动重连" };
    private readonly Label _lblStatus = new() { AutoSize = true, Text = "未连接" };
    private readonly Label _lblStat = new() { AutoSize = true, Text = "0 帧 | 0 错误" };

    private readonly ComboBox _cmbPreset = new() { DropDownStyle = ComboBoxStyle.DropDownList, MaximumSize = new Size(260, 0) };
    private readonly Label _lblPresetDescription = new() { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(71, 85, 105), Text = "说明：选择上位机要显示和记录的测量内容" };
    private readonly TextBox _txtHeaderCmd = new();
    private readonly TextBox _txtQueryCmd = new();
    private readonly Button _btnApplyPreset = new() { Text = "应用测量内容" };
    private readonly Button _btnSavePreset = new() { Text = "保存当前测量方案" };
    private readonly Button _btnReadHeader = new() { Text = "读取列名" };
    private readonly Button _btnSend = new() { Text = "单次读取" };

    private readonly ComboBox _cmbNumericFormat = new() { DropDownStyle = ComboBoxStyle.DropDownList, MaximumSize = new Size(120, 0) };
    private readonly ComboBox _cmbRate = new() { DropDownStyle = ComboBoxStyle.DropDownList, MaximumSize = new Size(150, 0) };
    private readonly CheckBox _chkHold = new() { Text = "数值保持" };
    private readonly CheckBox _chkMaxHold = new() { Text = "最大值保持" };
    private readonly NumericUpDown _numNormalPreset = new() { Minimum = 1, Maximum = 4 };
    private readonly NumericUpDown _numNormalCount = new() { Minimum = 1, Maximum = 255 };
    private readonly NumericUpDown _numListPreset = new() { Minimum = 1, Maximum = 4 };
    private readonly NumericUpDown _numListCount = new() { Minimum = 1, Maximum = 32 };
    private readonly NumericUpDown _numListOrder = new() { Minimum = 1, Maximum = 50 };
    private readonly ComboBox _cmbListSelect = new() { DropDownStyle = ComboBoxStyle.DropDownList, MaximumSize = new Size(120, 0) };
    private readonly TextBox _txtSetupCommands = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly Button _btnApplyGeneralConfig = new() { Text = "应用采样/格式设置" };
    private readonly Button _btnApplyNormalConfig = new() { Text = "应用常规测量列设置" };
    private readonly Button _btnApplyListConfig = new() { Text = "应用谐波测量列设置" };
    private readonly Button _btnRunSetup = new() { Text = "执行批量命令" };
    private readonly Button _btnReadAllConfig = new() { Text = "读取仪器全部配置", Enabled = false };
    private readonly Button _btnRecommendedDefaults = new() { Text = "恢复推荐默认：电压/电流/PF/THD", Enabled = false };

    private readonly ComboBox _cmbWiring = NewCombo("P1W2", "P1W3", "P3W3", "P3W4", "V3A3");
    private readonly ComboBox _cmbInputMode = NewCombo("RMS", "VMEAN", "DC");
    private readonly ComboBox _cmbSyncSource = NewCombo("VOLTAGE", "CURRENT", "OFF");
    private readonly CheckBox _chkVoltageAuto = new() { Text = "电压自动量程", Checked = true };
    private readonly CheckBox _chkCurrentAuto = new() { Text = "电流自动量程", Checked = true };
    private readonly ComboBox _cmbVoltageRange = NewCombo("15V", "30V", "60V", "150V", "300V", "600V");
    private readonly ComboBox _cmbCurrentRange = NewCombo("5mA", "10mA", "20mA", "50mA", "100mA", "200mA", "0.5A", "1A", "2A", "5A", "10A", "20A");
    private readonly CheckBox _chkInputFilter = new() { Text = "输入滤波" };
    private readonly CheckBox _chkLineFilter = new() { Text = "线路滤波" };
    private readonly CheckBox _chkAveraging = new() { Text = "启用平均" };
    private readonly ComboBox _cmbAverageType = NewCombo("LINEAR", "EXPONENT");
    private readonly NumericUpDown _numAverageCount = new() { Minimum = 1, Maximum = 64, Value = 8 };
    private readonly NumericUpDown _numCrestFactor = new() { Minimum = 1, Maximum = 6, Value = 3 };
    private readonly CheckBox _chkScaling = new() { Text = "启用变比" };
    private readonly NumericUpDown _numPtRatio = NewDecimal(1, 0.001m, 100000);
    private readonly NumericUpDown _numCtRatio = NewDecimal(1, 0.001m, 100000);
    private readonly NumericUpDown _numScaleFactor = NewDecimal(1, 0.001m, 100000);
    private readonly Button _btnApplyInput = new() { Text = "应用输入配置" };
    private readonly Button _btnApplyRange = new() { Text = "应用量程设置" };
    private readonly Button _btnApplyProcessing = new() { Text = "应用处理配置" };

    private readonly CheckBox _chkHarmonics = new() { Text = "启用谐波分析" };
    private readonly NumericUpDown _numHarmonicElement = new() { Minimum = 1, Maximum = 3, Value = 1 };
    private readonly NumericUpDown _numHarmonicDisplayOrder = new() { Minimum = 1, Maximum = 50, Value = 1 };
    private readonly ComboBox _cmbHarmonicSync = NewCombo("V", "A");
    private readonly ComboBox _cmbHarmonicThd = NewCombo("IEC", "CSA");
    private readonly Button _btnApplyHarmonics = new() { Text = "应用谐波配置" };
    private readonly Button _btnQuickThirdHarmonic = new() { Text = "快速设置：三次谐波测量" };
    private readonly ComboBox _cmbMultiHarmonicMax = NewCombo("3次", "5次", "7次", "9次", "11次", "13次", "15次", "17次", "19次", "21次", "25次", "31次", "39次", "49次");
    private readonly Button _btnMultiOddHarmonics = new() { Text = "多次电流谐波：显示 3/5/7..." };
    private readonly ComboBox _cmbIntegrationMode = NewCombo("NORMAL", "CONTINUOUS");
    private readonly NumericUpDown _numIntegrationHour = new() { Minimum = 0, Maximum = 10000 };
    private readonly NumericUpDown _numIntegrationMinute = new() { Minimum = 0, Maximum = 59 };
    private readonly NumericUpDown _numIntegrationSecond = new() { Minimum = 0, Maximum = 59 };
    private readonly Button _btnIntegrationStart = new() { Text = "开始积分" };
    private readonly Button _btnIntegrationStop = new() { Text = "停止积分" };
    private readonly Button _btnIntegrationReset = new() { Text = "复位积分" };
    private readonly Button _btnRemote = new() { Text = "远程模式" };
    private readonly Button _btnLocal = new() { Text = "本地模式" };
    private readonly Button _btnLockPanel = new() { Text = "锁定面板" };
    private readonly Button _btnUnlockPanel = new() { Text = "解锁面板" };
    private readonly Button _btnResetInstrument = new() { Text = "恢复测量默认" };
    private readonly Button _btnZero = new() { Text = "执行调零" };
    private readonly NumericUpDown _numDisplayIndex = new() { Minimum = 1, Maximum = 4, Value = 1 };
    private readonly ComboBox _cmbDisplayMode = NewCombo("VALUE", "RANGE", "ESCALING");
    private readonly ComboBox _cmbDisplayFunction = NewCombo("U", "I", "P", "S", "Q", "TIME");
    private readonly NumericUpDown _numDisplayElement = new() { Minimum = 1, Maximum = 1, Value = 1, Enabled = false };
    private readonly ComboBox _cmbDisplayResolution = NewCombo("HIGH", "LOW");
    private readonly Button _btnApplyDisplay = new() { Text = "应用 ABCD 四块显示" };
    private readonly ComboBox _cmbDisplayA = NewDisplayCombo("U", "I", "P", "S", "Q", "TIME");
    private readonly ComboBox _cmbDisplayB = NewDisplayCombo("U", "I", "P", "LAMBDA", "PHI");
    private readonly ComboBox _cmbDisplayC = NewDisplayCombo("U", "I", "P", "UPPEAK", "UMPEAK", "IPPEAK", "IMPEAK", "PPPEAK", "PMPEAK", "WH", "WHP", "WHM", "AH", "AHP", "AHM", "MATH");
    private readonly ComboBox _cmbDisplayD = NewDisplayCombo("U", "I", "P", "LAMBDA", "FU", "FI", "UTHD", "ITHD");
    private readonly CheckBox _chkStore = new() { Text = "启用仪器内部存储" };
    private readonly NumericUpDown _numStoreHour = new() { Minimum = 0, Maximum = 99 };
    private readonly NumericUpDown _numStoreMinute = new() { Minimum = 0, Maximum = 59 };
    private readonly NumericUpDown _numStoreSecond = new() { Minimum = 1, Maximum = 59, Value = 1 };
    private readonly Button _btnApplyStore = new() { Text = "应用存储设置" };

    private readonly FlowLayoutPanel _dashboard = new() { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, Padding = new Padding(8) };
    private readonly ComboBox _cmbChartChannel = NewCombo("全部通道");
    private readonly NumericUpDown _numChartWindow = new() { Minimum = 30, Maximum = 2000, Value = 300, Increment = 10 };
    private readonly CheckBox _chkLockYAxis = new() { Text = "锁定 Y 轴" };
    private readonly NumericUpDown _numYMin = NewDecimal(-100, 0.1m, 1000000, -1000000);
    private readonly NumericUpDown _numYMax = NewDecimal(100, 0.1m, 1000000, -1000000);
    private readonly Button _btnPauseChart = new() { Text = "暂停曲线" };
    private readonly Button _btnClearChart = new() { Text = "清空曲线" };
    private readonly Button _btnChartSettings = new() { Text = "图形设置" };
    private readonly Button _btnHoldReading = new() { Text = "保持读数", Enabled = false };
    private readonly List<Label> _dashboardValues = new();
    private readonly List<string> _dashboardCodes = new();
    private bool _chartPaused;

    private readonly Button _btnConnect = new() { Text = "连接" };
    private readonly Button _btnDisconnect = new() { Text = "断开", Enabled = false };
    private readonly Button _btnStart = new() { Text = "开始采集", Enabled = false };
    private readonly Button _btnStop = new() { Text = "停止采集", Enabled = false };
    private readonly Button _btnConnectionSettings = new() { Text = "连接设置" };
    private readonly Button _btnStartLog = new() { Text = "开始记录", Enabled = false };
    private readonly Button _btnStopLog = new() { Text = "停止记录", Enabled = false };
    private readonly Button _btnExportReport = new() { Text = "导出图表报告" };

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
    };

    private readonly Chart _chart = new();
    private readonly TextBox _txtRaw = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 100 };

    private readonly ConcurrentQueue<MeasurementFrame> _uiQueue = new();
    private readonly CsvLogger _logger = new();
    private readonly FixedSizeFrameBuffer _frameBuffer;
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    private IScpiTransport? _transport;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;
    private List<string> _currentHeaders = new();
    private int _frameCount;
    private int _errorCount;
    private bool _showOddHarmonicsFromThird;
    private int _multiHarmonicMaxOrder = 11;
    private bool _isClosing;
    private bool _shutdownComplete;
    private string _activeRecordDirectory = string.Empty;

    public MainForm()
    {
        _settings = SettingsStore.Load();
        _frameBuffer = new FixedSizeFrameBuffer(_settings.ChartCapacity);

        Text = "PA310 Power Studio";
        AutoScaleMode = AutoScaleMode.Dpi;
        Width = 1600;
        Height = 980;
        MinimumSize = new Size(1280, 820);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(244, 247, 251);

        BuildControlsFromSettings();
        BuildLayout();
        ApplyVisualTheme();
        BuildChart();
        BuildGrid(Array.Empty<string>());
        BuildSeries(Array.Empty<string>());
        BuildDashboardFromDisplayPanels();
        WireEvents();
        SetConnectedState(false);
        RefreshPorts();
        UpdateConnectionModeUi();

        _uiTimer.Tick += (_, _) => FlushUiQueue();
        _uiTimer.Start();
    }

    private void BuildControlsFromSettings()
    {
        _cmbConnType.Items.AddRange(new object[] { "USB", "RS-232", "TCP/IP" });
        _cmbConnType.SelectedItem = _settings.ConnectionType == "串口" ? "USB" : _settings.ConnectionType;
        if (_cmbConnType.SelectedIndex < 0) _cmbConnType.SelectedIndex = 0;

        _txtIp.Text = _settings.Ip;
        _numPort.Value = _settings.Port;
        _numInterval.Value = _settings.PollIntervalMs;
        _chkAutoReconnect.Checked = _settings.AutoReconnect;
        _txtHeaderCmd.Text = _settings.DefaultHeaderCommand;
        _txtQueryCmd.Text = _settings.DefaultQueryCommand;
        _txtSetupCommands.Text = _settings.SetupCommands;

        _cmbBaud.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
        _cmbBaud.SelectedItem = _settings.BaudRate.ToString();
        if (_cmbBaud.SelectedIndex < 0) _cmbBaud.SelectedIndex = 4;

        _cmbNumericFormat.Items.AddRange(new object[] { "ASCii", "FLOat" });
        _cmbNumericFormat.SelectedItem = _settings.NumericFormat;
        if (_cmbNumericFormat.SelectedIndex < 0) _cmbNumericFormat.SelectedIndex = 0;

        _cmbRate.Items.AddRange(new object[] { "100 ms", "250 ms", "500 ms", "1 s", "2 s", "5 s", "10 s", "20 s" });
        _cmbRate.SelectedItem = RateLabelFromSetting(_settings.Rate);
        if (_cmbRate.SelectedIndex < 0) _cmbRate.SelectedItem = "250 ms";

        _chkHold.Checked = _settings.NumericHoldEnabled;
        _numNormalPreset.Value = Math.Clamp(_settings.NormalPresetMode, 1, 4);
        _numNormalCount.Value = Math.Clamp(_settings.NormalItemCount, 1, 255);
        _numListPreset.Value = Math.Clamp(_settings.ListPresetMode, 1, 4);
        _numListCount.Value = Math.Clamp(_settings.ListItemCount, 1, 32);
        _numListOrder.Value = Math.Clamp(_settings.ListOrder, 1, 50);

        _cmbListSelect.Items.AddRange(new object[] { "ALL", "ODD", "EVEN" });
        _cmbListSelect.SelectedItem = _settings.ListSelect.ToUpperInvariant();
        if (_cmbListSelect.SelectedIndex < 0) _cmbListSelect.SelectedItem = "ALL";

        NormalizeBuiltinPresetNames();
        foreach (var preset in _settings.Presets)
            _cmbPreset.Items.Add(preset);
        if (_cmbPreset.Items.Count > 0) _cmbPreset.SelectedIndex = 0;
        UpdatePresetDescription();
        RefreshDisplayFunctionChoices();
        SetRecommendedDisplayPanelDefaults();
    }

    private void NormalizeBuiltinPresetNames()
    {
        if (_settings.Presets.Count == 0)
        {
            _settings.Presets = AppSettings.CreateDefault().Presets;
            return;
        }

        foreach (var preset in _settings.Presets)
        {
            string name = preset.Name.Trim();
            if (name is "基础4项" or "基础四项" or "常规输出" or "常规数值")
            {
                preset.Name = "常规测量：电压/电流/功率/功率因数/频率";
                preset.Description = "读取 PA300 当前常规输出列；常见为 U/I/P/S/Q/PF/相位/频率等";
            }
            else if (name is "谐波列表")
            {
                preset.Name = "谐波测量：各阶电压/电流/功率谐波";
                preset.Description = "读取谐波列表，列含义由谐波输出设置决定";
            }
            else if (name is "设备信息")
            {
                preset.Name = "设备信息：型号/序列号/固件版本";
                preset.Description = "读取仪器身份信息，不是测量数据";
            }
            else if (name is "谐波失真")
            {
                preset.Name = "谐波失真：THD/总谐波失真";
                preset.Description = "结合谐波 LIST 预设读取 THD 等失真项目";
            }
        }
    }

    private void BuildLayout()
    {
        SuspendLayout();

        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 23, 42), Padding = new Padding(24, 12, 20, 10) };
        var brand = new Label
        {
            Text = "PA310  POWER STUDIO",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 12)
        };
        var subtitle = new Label
        {
            Text = "功率分析 · 实时采集 · SCPI 控制",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Microsoft YaHei UI", 9),
            AutoSize = true,
            Location = new Point(26, 43)
        };
        var headerActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, Width = 570, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Padding = new Padding(0, 8, 0, 0), BackColor = Color.Transparent
        };
        header.Controls.Add(headerActions);
        header.Controls.Add(brand);
        header.Controls.Add(subtitle);
        shell.Controls.Add(header, 0, 0);

        var workspace = new SplitContainer
        {
            Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterDistance = 260,
            SplitterWidth = 1, BackColor = Color.FromArgb(226, 232, 240), Padding = new Padding(14)
        };
        workspace.Panel1.BackColor = BackColor;
        workspace.Panel2.BackColor = BackColor;
        workspace.Panel1.Padding = new Padding(0, 0, 8, 0);
        workspace.Panel2.Padding = new Padding(8, 0, 0, 0);
        Shown += (_, _) =>
        {
            workspace.Panel1MinSize = 245;
            workspace.SplitterDistance = 260;
        };

        var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 330));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var connection = NewCard("设备连接", "自动识别 PA300 的实际驱动模式");
        var connectFields = NewFormGrid(7);
        AddFormRow(connectFields, 0, "连接方式", _cmbConnType);
        AddFormRow(connectFields, 1, "串口 / 设备", _cmbCom);
        AddFormRow(connectFields, 2, "通信速率", _cmbBaud);
        AddFormRow(connectFields, 3, "IP 地址", _txtIp);
        AddFormRow(connectFields, 4, "TCP 端口", _numPort);
        var usbActions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0) };
        usbActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        usbActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        usbActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        usbActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        usbActions.Controls.Add(_btnConnect, 0, 0);
        usbActions.Controls.Add(_btnStart, 1, 0);
        usbActions.Controls.Add(_btnConnectionSettings, 2, 0);
        foreach (var button in new[] { _btnConnect, _btnStart, _btnConnectionSettings })
        {
            button.Dock = DockStyle.Fill;
            button.AutoSize = false;
            button.MinimumSize = new Size(64, 32);
            button.Margin = new Padding(2, 3, 2, 3);
        }
        connectFields.Controls.Add(usbActions, 0, 5);
        connectFields.SetColumnSpan(usbActions, 2);
        connection.Controls.Add(connectFields);
        sidebar.Controls.Add(connection, 0, 0);

        var acquisition = NewCard("采集任务", "选择要显示和记录的测量内容，并控制采样节奏");
        var acquireFields = NewFormGrid(5);
        AddFormRow(acquireFields, 0, "测量内容", _cmbPreset);
        AddFormRow(acquireFields, 1, "参数说明", _lblPresetDescription);
        AddFormRow(acquireFields, 2, "采集间隔 ms", _numInterval);
        var presetActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
        presetActions.Controls.Add(_btnApplyPreset);
        acquireFields.Controls.Add(presetActions, 0, 3);
        acquireFields.SetColumnSpan(presetActions, 2);
        var logActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
        logActions.Controls.AddRange([_btnStartLog, _btnExportReport]);
        acquireFields.Controls.Add(logActions, 0, 4);
        acquireFields.SetColumnSpan(logActions, 2);
        acquisition.Controls.Add(acquireFields);
        sidebar.Controls.Add(acquisition, 0, 1);
        workspace.Panel1.Controls.Add(sidebar);

        var pages = new TabControl { Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 10), Padding = new Point(18, 8) };
        var livePage = new TabPage("实时监测") { BackColor = BackColor, Padding = new Padding(0, 10, 0, 0) };
        var liveLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        liveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        liveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        liveLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        liveLayout.Controls.Add(_dashboard, 0, 0);
        var chartToolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(8, 5, 8, 3), BackColor = Color.White };
        chartToolbar.Controls.AddRange([
            new Label { Text = "曲线通道", AutoSize = true, Margin = new Padding(5, 8, 4, 0) }, _cmbChartChannel,
            _btnHoldReading, _btnChartSettings
        ]);
        liveLayout.Controls.Add(chartToolbar, 0, 1);
        var liveSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 365, SplitterWidth = 8 };
        liveSplit.Panel1.Controls.Add(_chart);
        liveSplit.Panel2.Controls.Add(_grid);
        liveLayout.Controls.Add(liveSplit, 0, 2);
        livePage.Controls.Add(liveLayout);

        var configPage = new TabPage("仪器配置") { BackColor = BackColor, Padding = new Padding(12) };
        var configTabs = new TabControl { Dock = DockStyle.Fill };
        configTabs.TabPages.Add(BuildDisplayStoragePage());
        configTabs.TabPages.Add(BuildHarmonicsIntegrationPage());
        configTabs.TabPages.Add(BuildProcessingConfigPage());
        configTabs.TabPages.Add(BuildInputConfigPage());
        configTabs.TabPages.Add(BuildOutputConfigPage());
        configTabs.TabPages.Add(BuildSystemControlPage());
        configTabs.TabPages.Add(BuildGeneralConfigPage());
        configTabs.TabPages.Add(BuildCommandPage());
        var configHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 48, Padding = new Padding(12, 8, 12, 4),
            BackColor = Color.White, WrapContents = false
        };
        configHeader.Controls.Add(_btnReadAllConfig);
        configHeader.Controls.Add(_btnRecommendedDefaults);
        configHeader.Controls.Add(new Label
        {
            Text = "先读取仪器当前值，再按分类修改并应用；读取失败的选项会标红，所有操作都会记录到底部运行日志。",
            AutoSize = true, Margin = new Padding(14, 8, 0, 0), ForeColor = Color.FromArgb(71, 85, 105)
        });
        configPage.Controls.Add(configTabs);
        configPage.Controls.Add(configHeader);

        pages.TabPages.AddRange([livePage, configPage]);
        workspace.Panel2.Controls.Add(pages);
        shell.Controls.Add(workspace, 0, 1);
        shell.Controls.Add(BuildRuntimeLogPanel(), 0, 2);
        Controls.Add(shell);
        ResumeLayout(true);
    }

    private Control BuildRuntimeLogPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = BackColor, Padding = new Padding(0, 10, 0, 0) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            BackColor = Color.White,
            Padding = new Padding(10, 6, 10, 4),
            Margin = new Padding(0)
        };
        _lblStatus.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
        _lblStatus.ForeColor = Color.FromArgb(15, 23, 42);
        _lblDevice.ForeColor = Color.FromArgb(71, 85, 105);
        _lblStat.ForeColor = Color.FromArgb(71, 85, 105);
        toolbar.Controls.Add(new Label { Text = "运行日志", AutoSize = true, Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold), Margin = new Padding(4, 7, 12, 0) });
        toolbar.Controls.Add(_lblStatus);
        toolbar.Controls.Add(new Label { Text = "｜", AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184), Margin = new Padding(8, 7, 8, 0) });
        toolbar.Controls.Add(_lblDevice);
        toolbar.Controls.Add(new Label { Text = "｜", AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184), Margin = new Padding(8, 7, 8, 0) });
        toolbar.Controls.Add(_lblStat);
        toolbar.Controls.Add(_chkAutoReconnect);

        panel.Controls.Add(toolbar, 0, 0);
        panel.Controls.Add(_txtRaw, 0, 1);
        return panel;
    }

    private TabPage BuildGeneralConfigPage()
    {
        var page = new TabPage("采样与返回格式") { BackColor = Color.White, Padding = new Padding(24) };
        var grid = NewFormGrid(4, 180);
        AddFormRow(grid, 0, "数据返回格式", _cmbNumericFormat);
        AddFormRow(grid, 1, "仪器刷新周期", _cmbRate);
        AddFormRow(grid, 2, "保持当前读数", _chkHold);
        grid.Controls.Add(_btnApplyGeneralConfig, 1, 3);
        page.Controls.Add(grid);
        return page;
    }

    private TabPage BuildInputConfigPage()
    {
        var page = new TabPage("接线输入") { BackColor = Color.White, Padding = new Padding(24), AutoScroll = true };
        var grid = NewFormGrid(4, 180);
        AddFormRow(grid, 0, "功率接线方式", _cmbWiring);
        AddFormRow(grid, 1, "电压/电流输入模式", _cmbInputMode);
        AddFormRow(grid, 2, "频率同步源", _cmbSyncSource);
        grid.Controls.Add(_btnApplyInput, 1, 3);
        page.Controls.Add(grid);
        return page;
    }

    private TabPage BuildProcessingConfigPage()
    {
        var page = new TabPage("滤波与变比") { BackColor = Color.White, Padding = new Padding(24), AutoScroll = true };
        var grid = NewFormGrid(12, 180);
        AddFormRow(grid, 0, "输入信号滤波", _chkInputFilter);
        AddFormRow(grid, 1, "电源线路滤波", _chkLineFilter);
        AddFormRow(grid, 2, "启用平均读数", _chkAveraging);
        AddFormRow(grid, 3, "平均算法类型", _cmbAverageType);
        AddFormRow(grid, 4, "平均采样次数", _numAverageCount);
        AddFormRow(grid, 5, "峰值因数 CF", _numCrestFactor);
        AddFormRow(grid, 6, "启用 PT/CT 变比", _chkScaling);
        AddFormRow(grid, 7, "PT 电压互感器变比", _numPtRatio);
        AddFormRow(grid, 8, "CT 电流互感器变比", _numCtRatio);
        AddFormRow(grid, 9, "功率缩放系数", _numScaleFactor);
        AddFormRow(grid, 10, "保持最大测量值", _chkMaxHold);
        grid.Controls.Add(_btnApplyProcessing, 1, 11);
        page.Controls.Add(grid);
        return page;
    }

    private TabPage BuildDisplayStoragePage()
    {
        var page = new TabPage("显示与量程") { BackColor = Color.White, Padding = new Padding(18), AutoScroll = true };
        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        var display = NewCard("ABCD 屏显示", "读取或配置 PA310 面板当前四个测量项");
        var dg = NewFormGrid(5, 170);
        AddFormRow(dg, 0, "A 屏当前测量项", _cmbDisplayA);
        AddFormRow(dg, 1, "B 屏当前测量项", _cmbDisplayB);
        AddFormRow(dg, 2, "C 屏当前测量项", _cmbDisplayC);
        AddFormRow(dg, 3, "D 屏当前测量项", _cmbDisplayD);
        dg.Controls.Add(_btnApplyDisplay, 1, 4);
        display.Controls.Add(dg);

        var ranges = NewCard("电压/电流量程", "PA310：电压最高 600V，直接电流最高 20A；建议先用最大量程");
        var rg = NewFormGrid(5, 145);
        AddFormRow(rg, 0, "电压测量量程", _cmbVoltageRange);
        AddFormRow(rg, 1, "电压自动量程", _chkVoltageAuto);
        AddFormRow(rg, 2, "电流测量量程", _cmbCurrentRange);
        AddFormRow(rg, 3, "电流自动量程", _chkCurrentAuto);
        rg.Controls.Add(_btnApplyRange, 1, 4);
        ranges.Controls.Add(rg);

        var storage = NewCard("仪器内部存储", "启用仪器存储并设置自动保存间隔");
        var sg = NewFormGrid(5, 135);
        AddFormRow(sg, 0, "存储状态", _chkStore);
        AddFormRow(sg, 1, "间隔小时", _numStoreHour);
        AddFormRow(sg, 2, "间隔分钟", _numStoreMinute);
        AddFormRow(sg, 3, "间隔秒", _numStoreSecond);
        sg.Controls.Add(_btnApplyStore, 1, 4);
        storage.Controls.Add(sg);

        split.Controls.Add(display, 0, 0);
        split.Controls.Add(ranges, 1, 0);
        split.Controls.Add(storage, 2, 0);
        page.Controls.Add(split);
        return page;
    }

    private TabPage BuildHarmonicsIntegrationPage()
    {
        var page = new TabPage("谐波与积分") { BackColor = Color.White, Padding = new Padding(18), AutoScroll = true };
        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var harmonics = NewCard("电流谐波 / ITHD 分析", "PFC 关键看输入电流谐波：多次谐波测试同时显示 3、5、7、9、11…次电流谐波");
        var hg = NewFormGrid(9, 170);
        AddFormRow(hg, 0, "启用谐波分析", _chkHarmonics);
        AddFormRow(hg, 1, "谐波分析通道", _numHarmonicElement);
        AddFormRow(hg, 2, "谐波同步源", _cmbHarmonicSync);
        AddFormRow(hg, 3, "最高显示阶次", _numHarmonicDisplayOrder);
        AddFormRow(hg, 4, "THD 计算标准", _cmbHarmonicThd);
        AddFormRow(hg, 5, "多次谐波最高阶", _cmbMultiHarmonicMax);
        hg.Controls.Add(_btnApplyHarmonics, 1, 6);
        hg.Controls.Add(_btnQuickThirdHarmonic, 1, 7);
        hg.Controls.Add(_btnMultiOddHarmonics, 1, 8);
        harmonics.Controls.Add(hg);
        var integration = NewCard("积分控制", "用于累计电能/电量等积分结果，设置计时并控制开始/停止");
        var ig = NewFormGrid(7, 150);
        AddFormRow(ig, 0, "积分运行模式", _cmbIntegrationMode);
        AddFormRow(ig, 1, "积分定时：小时", _numIntegrationHour);
        AddFormRow(ig, 2, "积分定时：分钟", _numIntegrationMinute);
        AddFormRow(ig, 3, "积分定时：秒", _numIntegrationSecond);
        ig.Controls.Add(_btnIntegrationStart, 1, 4);
        ig.Controls.Add(_btnIntegrationStop, 1, 5);
        ig.Controls.Add(_btnIntegrationReset, 1, 6);
        integration.Controls.Add(ig);
        split.Controls.Add(harmonics, 0, 0);
        split.Controls.Add(integration, 1, 0);
        page.Controls.Add(split);
        return page;
    }

    private TabPage BuildSystemControlPage()
    {
        var page = new TabPage("面板与系统") { BackColor = Color.White, Padding = new Padding(24) };
        var info = new Label
        {
            Text = "这些操作对应仪器物理面板的远程/本地、按键锁定、复位与调零功能。",
            Dock = DockStyle.Top, Height = 42, ForeColor = Color.FromArgb(71, 85, 105)
        };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 100, WrapContents = true };
        actions.Controls.AddRange([_btnRemote, _btnLocal, _btnLockPanel, _btnUnlockPanel, _btnZero, _btnResetInstrument]);
        page.Controls.Add(actions);
        page.Controls.Add(info);
        return page;
    }

    private TabPage BuildOutputConfigPage()
    {
        var page = new TabPage("输出与谐波") { BackColor = Color.White, Padding = new Padding(24) };
        var grid = NewFormGrid(8, 180);
        AddFormRow(grid, 0, "常规测量预设号", _numNormalPreset);
        AddFormRow(grid, 1, "常规测量列数", _numNormalCount);
        grid.Controls.Add(_btnApplyNormalConfig, 1, 2);
        AddFormRow(grid, 3, "谐波输出预设号", _numListPreset);
        AddFormRow(grid, 4, "谐波参数组数量", _numListCount);
        AddFormRow(grid, 5, "谐波最高阶次", _numListOrder);
        AddFormRow(grid, 6, "谐波阶次范围", _cmbListSelect);
        grid.Controls.Add(_btnApplyListConfig, 1, 7);
        page.Controls.Add(grid);
        return page;
    }

    private TabPage BuildCommandPage()
    {
        var page = new TabPage("批量 SCPI") { BackColor = Color.White, Padding = new Padding(20) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.Controls.Add(_txtSetupCommands, 0, 0);
        layout.Controls.Add(_btnRunSetup, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private static Panel NewCard(string title, string caption)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(16, 62, 16, 12) };
        card.Controls.Add(new Label { Text = title, AutoSize = true, Location = new Point(17, 14), Font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) });
        card.Controls.Add(new Label { Text = caption, AutoSize = true, Location = new Point(18, 39), Font = new Font("Microsoft YaHei UI", 8.5f), ForeColor = Color.FromArgb(100, 116, 139) });
        return card;
    }

    private static TableLayoutPanel NewFormGrid(int rows, int labelWidth = 92)
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = rows };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < rows; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        return grid;
    }

    private static ComboBox NewCombo(params object[] items)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(items);
        if (items.Length > 0) combo.SelectedIndex = 0;
        return combo;
    }

    private static ComboBox NewDisplayCombo(params string[] codes)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(codes.Select(code => (object)DisplayCodeLabel(code)).ToArray());
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        return combo;
    }

    private static NumericUpDown NewDecimal(decimal value, decimal increment, decimal maximum, decimal minimum = 0)
        => new() { DecimalPlaces = 3, Increment = increment, Minimum = minimum, Maximum = maximum, Value = value };

    private static void AddFormRow(TableLayoutPanel grid, int row, string label, Control control)
    {
        grid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(71, 85, 105) }, 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 4, 0, 4);
        grid.Controls.Add(control, 1, row);
    }

    private static void ResetConfigReadMarkers(IEnumerable<Control> controls)
    {
        foreach (var control in controls)
        {
            control.BackColor = control is ComboBox or TextBox or NumericUpDown
                ? SystemColors.Window
                : Color.Transparent;
            control.ForeColor = control is CheckBox ? SystemColors.ControlText : SystemColors.WindowText;
        }
    }

    private static void MarkConfigReadFailed(IEnumerable<Control> controls)
    {
        foreach (var control in controls)
        {
            control.BackColor = Color.FromArgb(254, 226, 226);
            control.ForeColor = Color.FromArgb(153, 27, 27);
        }
    }

    private void ApplyVisualTheme()
    {
        Font = new Font("Microsoft YaHei UI", 9);
        Color primary = Color.FromArgb(37, 99, 235);
        foreach (var button in GetAllControls(this).OfType<Button>())
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = primary;
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
            button.Height = 32;
            button.Padding = new Padding(9, 0, 9, 0);
            button.AutoSize = true;
        }
        foreach (var button in new[] { _btnDisconnect, _btnStop, _btnStopLog })
            button.BackColor = Color.FromArgb(51, 65, 85);
        foreach (var button in new[] { _btnConnect, _btnStart, _btnConnectionSettings })
        {
            button.AutoSize = false;
            button.MinimumSize = new Size(64, 32);
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(2, 3, 2, 3);
        }
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(51, 65, 85);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
        _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);
        _txtRaw.BackColor = Color.FromArgb(15, 23, 42);
        _txtRaw.ForeColor = Color.FromArgb(203, 213, 225);
        _txtRaw.Font = new Font("Consolas", 10);
    }

    private static IEnumerable<Control> GetAllControls(Control root)
    {
        foreach (Control control in root.Controls)
        {
            yield return control;
            foreach (var child in GetAllControls(control)) yield return child;
        }
    }

    private void BuildLegacyLayout()
    {
        var main = new TableLayoutPanel { Dock = DockStyle.Top, RowCount = 3, ColumnCount = 1, Height = 2000 };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 500));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8) };
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 340));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var firstRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        firstRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        firstRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        firstRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        firstRow.Padding = new Padding(0);

        var connectionGroup = new GroupBox { Text = "连接", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var connectionTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4 };
        connectionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        connectionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        connectionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        connectionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 4; i++) connectionTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        connectionTable.Padding = new Padding(2);
        connectionTable.Margin = new Padding(0);
        connectionTable.Controls.Add(new Label { Text = "方式", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        connectionTable.Controls.Add(_cmbConnType, 1, 0);
        connectionTable.Controls.Add(new Label { Text = "状态", Anchor = AnchorStyles.Left, AutoSize = true }, 2, 0);
        connectionTable.Controls.Add(_lblStatus, 3, 0);
        connectionTable.Controls.Add(new Label { Text = "USB端口", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        connectionTable.Controls.Add(_cmbCom, 1, 1);
        connectionTable.Controls.Add(new Label { Text = "波特率", Anchor = AnchorStyles.Left, AutoSize = true }, 2, 1);
        connectionTable.Controls.Add(_cmbBaud, 3, 1);
        connectionTable.Controls.Add(new Label { Text = "IP", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        connectionTable.Controls.Add(_txtIp, 1, 2);
        connectionTable.Controls.Add(new Label { Text = "端口", Anchor = AnchorStyles.Left, AutoSize = true }, 2, 2);
        connectionTable.Controls.Add(_numPort, 3, 2);
        var connectionButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = false, Margin = new Padding(0) };
        connectionTable.Controls.Add(connectionButtons, 0, 3);
        connectionTable.SetColumnSpan(connectionButtons, 4);
        connectionGroup.Controls.Add(connectionTable);

        var acquisitionGroup = new GroupBox { Text = "采集", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var acquisitionTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        acquisitionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        acquisitionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++) acquisitionTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        acquisitionTable.Padding = new Padding(2);
        acquisitionTable.Margin = new Padding(0);
        acquisitionTable.Controls.Add(new Label { Text = "参数组", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        acquisitionTable.Controls.Add(_cmbPreset, 1, 0);
        acquisitionTable.Controls.Add(new Label { Text = "表头命令", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        acquisitionTable.Controls.Add(_txtHeaderCmd, 1, 1);
        acquisitionTable.Controls.Add(new Label { Text = "采集命令", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        acquisitionTable.Controls.Add(_txtQueryCmd, 1, 2);
        var commandButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = false, Margin = new Padding(0) };
        commandButtons.Controls.Add(_btnApplyPreset);
        commandButtons.Controls.Add(_btnSavePreset);
        commandButtons.Controls.Add(_btnReadHeader);
        commandButtons.Controls.Add(_btnSend);
        commandButtons.Controls.Add(_btnStart);
        commandButtons.Controls.Add(_btnStop);
        commandButtons.Controls.Add(_btnStartLog);
        commandButtons.Controls.Add(_btnStopLog);
        acquisitionTable.Controls.Add(commandButtons, 0, 3);
        acquisitionTable.SetColumnSpan(commandButtons, 2);
        acquisitionGroup.Controls.Add(acquisitionTable);

        firstRow.Controls.Add(connectionGroup, 0, 0);
        firstRow.Controls.Add(acquisitionGroup, 0, 1);
        top.Controls.Add(firstRow, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };

        var tabGeneral = new TabPage("基础配置");
        var generalPanel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true, ColumnCount = 4, RowCount = 3 };
        generalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        generalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        generalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        generalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        generalPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        generalPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        generalPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        generalPanel.Controls.Add(new Label { Text = "格式", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        generalPanel.Controls.Add(_cmbNumericFormat, 1, 0);
        generalPanel.Controls.Add(new Label { Text = "更新率", Anchor = AnchorStyles.Left, AutoSize = true }, 2, 0);
        generalPanel.Controls.Add(_cmbRate, 3, 0);
        generalPanel.Controls.Add(new Label { Text = "采集周期(ms)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        generalPanel.Controls.Add(_numInterval, 1, 1);
        generalPanel.Controls.Add(_chkHold, 2, 1);
        generalPanel.Controls.Add(_chkAutoReconnect, 3, 1);
        generalPanel.Controls.Add(_btnApplyGeneralConfig, 3, 2);
        tabGeneral.Controls.Add(generalPanel);

        var tabOutput = new TabPage("输出配置");
        var outputPanel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true, ColumnCount = 4, RowCount = 4 };
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        outputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        outputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        outputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        outputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        outputPanel.Controls.Add(new Label { Text = "常规预设", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        outputPanel.Controls.Add(_numNormalPreset, 1, 0);
        outputPanel.Controls.Add(new Label { Text = "常规项数", Anchor = AnchorStyles.Left, AutoSize = true }, 2, 0);
        outputPanel.Controls.Add(_numNormalCount, 3, 0);
        outputPanel.Controls.Add(new Label { Text = "谐波预设", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        outputPanel.Controls.Add(_numListPreset, 1, 1);
        outputPanel.Controls.Add(new Label { Text = "列表数", Anchor = AnchorStyles.Left, AutoSize = true }, 2, 1);
        outputPanel.Controls.Add(_numListCount, 3, 1);
        outputPanel.Controls.Add(new Label { Text = "阶次", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        outputPanel.Controls.Add(_numListOrder, 1, 2);
        outputPanel.Controls.Add(new Label { Text = "选择", Anchor = AnchorStyles.Left, AutoSize = true }, 2, 2);
        outputPanel.Controls.Add(_cmbListSelect, 3, 2);
        outputPanel.Controls.Add(_btnApplyNormalConfig, 1, 3);
        outputPanel.Controls.Add(_btnApplyListConfig, 3, 3);
        tabOutput.Controls.Add(outputPanel);

        var tabSetup = new TabPage("批量命令");
        var setupLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
        setupLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        setupLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        setupLayout.Controls.Add(_txtSetupCommands, 0, 0);
        setupLayout.Controls.Add(_btnRunSetup, 1, 0);
        tabSetup.Controls.Add(setupLayout);

        tabs.TabPages.Add(tabGeneral);
        tabs.TabPages.Add(tabOutput);
        tabs.TabPages.Add(tabSetup);
        top.Controls.Add(tabs, 0, 1);

        var middle = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 350 };
        middle.Panel1.Controls.Add(_chart);
        middle.Panel2.Controls.Add(_grid);

        var bottom = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 40 };
        var bottomTop = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        bottomTop.Controls.Add(_btnStartLog);
        bottomTop.Controls.Add(_btnStopLog);
        bottomTop.Controls.Add(_lblStat);
        bottom.Panel1.Controls.Add(bottomTop);
        bottom.Panel2.Controls.Add(_txtRaw);

        main.Controls.Add(top, 0, 0);
        main.Controls.Add(middle, 0, 1);
        main.Controls.Add(bottom, 0, 2);
        Controls.Add(main);

        foreach (var control in new Control[]
                 {
                     _cmbConnType, _cmbCom, _cmbBaud, _txtIp, _numPort,
                     _cmbPreset, _txtHeaderCmd, _txtQueryCmd,
                     _cmbNumericFormat, _cmbRate, _numInterval, _cmbVoltageRange, _cmbCurrentRange,
                     _numNormalPreset, _numNormalCount, _numListPreset, _numListCount, _numListOrder, _cmbListSelect
                 })
        {
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(3);
            control.MinimumSize = new Size(120, 0);
        }

        foreach (var button in new[]
                 {
                     _btnRefreshPorts, _btnConnect, _btnDisconnect, _btnConnectionSettings, _btnApplyPreset, _btnSavePreset,
                     _btnReadHeader, _btnSend, _btnStart, _btnStop, _btnStartLog, _btnStopLog,
                     _btnApplyGeneralConfig, _btnApplyNormalConfig, _btnApplyListConfig, _btnRunSetup,
                     _btnRecommendedDefaults, _btnExportReport, _btnApplyRange, _btnChartSettings, _btnHoldReading
                 })
        {
            button.AutoSize = true;
            button.MinimumSize = new Size(110, 32);
            button.Margin = new Padding(4, 3, 4, 3);
        }

        _lblStatus.Dock = DockStyle.Fill;
        _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        _chkHold.AutoSize = true;
        _chkAutoReconnect.AutoSize = true;
    }

    private void BuildChart()
    {
        _chart.Dock = DockStyle.Fill;
        _chart.BackColor = Color.White;
        _chart.Palette = ChartColorPalette.None;
        _chart.PaletteCustomColors =
        [
            Color.FromArgb(37, 99, 235), Color.FromArgb(14, 165, 233),
            Color.FromArgb(16, 185, 129), Color.FromArgb(245, 158, 11),
            Color.FromArgb(139, 92, 246), Color.FromArgb(236, 72, 153)
        ];
        var area = new ChartArea("main");
        area.BackColor = Color.White;
        area.AxisX.Title = "采样序列";
        area.AxisY.Title = "测量值";
        area.AxisX.MajorGrid.LineColor = Color.FromArgb(241, 245, 249);
        area.AxisY.MajorGrid.LineColor = Color.FromArgb(226, 232, 240);
        area.AxisX.LineColor = Color.FromArgb(203, 213, 225);
        area.AxisY.LineColor = Color.FromArgb(203, 213, 225);
        _chart.ChartAreas.Add(area);
        _chart.Legends.Add(new Legend { Docking = Docking.Top, Alignment = StringAlignment.Far, BackColor = Color.Transparent });
    }

    private void BuildGrid(IReadOnlyList<string> headers)
    {
        _grid.Columns.Clear();
        _grid.Rows.Clear();
        _grid.Columns.Add("Time", "时间");
        var titles = headers.Count > 0 ? headers : Enumerable.Range(1, 8).Select(i => $"Value{i}").ToArray();
        foreach (string title in titles)
            _grid.Columns.Add(Guid.NewGuid().ToString("N"), title);
        _grid.Columns.Add("RawCount", "返回项数");
    }

    private void BuildSeries(IReadOnlyList<string> headers)
    {
        _chart.Series.Clear();
        var names = headers.Count > 0 ? headers.Take(8).ToArray() : Enumerable.Range(1, 4).Select(i => $"Value{i}").ToArray();
        foreach (var name in names)
        {
            _chart.Series.Add(new Series(name)
            {
                ChartType = SeriesChartType.FastLine,
                BorderWidth = 2,
                ChartArea = "main"
            });
        }

        string selected = _cmbChartChannel.Text;
        _cmbChartChannel.Items.Clear();
        _cmbChartChannel.Items.Add("全部通道");
        foreach (string name in names) _cmbChartChannel.Items.Add(name);
        _cmbChartChannel.SelectedItem = _cmbChartChannel.Items.Contains(selected)
            ? selected
            : (headers.Count > 0 ? names[0] : "全部通道");
    }

    private void BuildDashboard(IReadOnlyList<string> headers)
    {
        _dashboard.SuspendLayout();
        _dashboard.Controls.Clear();
        _dashboardValues.Clear();
        _dashboardCodes.Clear();
        var names = headers.Count > 0 ? headers.Take(10).ToArray() : new[] { "电压", "电流", "有功功率", "功率因数" };
        foreach (string name in names)
        {
            var card = new Panel { Width = 190, Height = 98, BackColor = Color.White, Margin = new Padding(5), Padding = new Padding(12) };
            var title = new Label { Text = name, AutoEllipsis = true, Dock = DockStyle.Top, Height = 28, ForeColor = Color.FromArgb(100, 116, 139) };
            var value = new Label { Text = "—", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };
            card.Controls.Add(value);
            card.Controls.Add(title);
            _dashboard.Controls.Add(card);
            _dashboardValues.Add(value);
            _dashboardCodes.Add(string.Empty);
        }
        _dashboard.ResumeLayout(true);
    }

    private void BuildDashboardFromDisplayPanels()
    {
        string[] codes =
        [
            DisplayCodeFromItem(_cmbDisplayA.SelectedItem),
            DisplayCodeFromItem(_cmbDisplayB.SelectedItem),
            DisplayCodeFromItem(_cmbDisplayC.SelectedItem),
            DisplayCodeFromItem(_cmbDisplayD.SelectedItem)
        ];

        _dashboard.SuspendLayout();
        _dashboard.Controls.Clear();
        _dashboardValues.Clear();
        _dashboardCodes.Clear();
        string[] slots = ["A", "B", "C", "D"];
        for (int i = 0; i < codes.Length; i++)
        {
            string title = $"{slots[i]} 屏 · {DashboardTitleForCode(codes[i])}";
            var card = new Panel { Width = 220, Height = 98, BackColor = Color.White, Margin = new Padding(5), Padding = new Padding(12) };
            var label = new Label { Text = title, AutoEllipsis = true, Dock = DockStyle.Top, Height = 32, ForeColor = Color.FromArgb(100, 116, 139) };
            var value = new Label { Text = "—", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };
            card.Controls.Add(value);
            card.Controls.Add(label);
            _dashboard.Controls.Add(card);
            _dashboardValues.Add(value);
            _dashboardCodes.Add(codes[i]);
        }
        _dashboard.ResumeLayout(true);
    }

    private void UpdateDashboard(MeasurementFrame frame)
    {
        int count = _dashboardValues.Count;
        for (int i = 0; i < count; i++)
        {
            int valueIndex = ResolveDashboardValueIndex(frame.Headers, i);
            _dashboardValues[i].Text = valueIndex >= 0 && valueIndex < frame.Values.Count && frame.Values[valueIndex] is double value
                ? FormatDashboardValue(value)
                : "—";
        }
    }

    private static string FormatDashboardValue(double value)
    {
        double abs = Math.Abs(value);
        if (abs >= 1_000_000) return $"{value / 1_000_000:0.###} M";
        if (abs >= 1_000) return $"{value / 1_000:0.###} k";
        if (abs > 0 && abs < 0.001) return value.ToString("0.###E+0", CultureInfo.InvariantCulture);
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private int ResolveDashboardValueIndex(IReadOnlyList<string> headers, int dashboardIndex)
    {
        if (dashboardIndex >= _dashboardCodes.Count || string.IsNullOrWhiteSpace(_dashboardCodes[dashboardIndex]))
            return dashboardIndex < headers.Count ? dashboardIndex : -1;

        string code = _dashboardCodes[dashboardIndex].ToUpperInvariant();
        for (int i = 0; i < headers.Count; i++)
        {
            if (HeaderMatchesDisplayCode(headers[i], code))
                return i;
        }

        return -1;
    }

    private static bool HeaderMatchesDisplayCode(string header, string code)
    {
        string h = header.ToUpperInvariant();
        return code switch
        {
            "U" => h.Contains("电压 U") || h.StartsWith("U-"),
            "I" => h.Contains("电流 I") || h.StartsWith("I-"),
            "P" => h.Contains("有功功率") || h.StartsWith("P-"),
            "S" => h.Contains("视在功率") || h.StartsWith("S-"),
            "Q" => h.Contains("无功功率") || h.StartsWith("Q-"),
            "LAMBDA" => h.Contains("功率因数") || h.Contains("PF") || h.StartsWith("LAMBDA"),
            "PHI" => h.Contains("相位角") || h.StartsWith("PHI"),
            "FU" => h.Contains("电压频率") || h.StartsWith("FU"),
            "FI" => h.Contains("电流频率") || h.StartsWith("FI"),
            "UTHD" => h.Contains("电压总谐波") || h.StartsWith("UTHD"),
            "ITHD" => h.Contains("电流总谐波") || h.StartsWith("ITHD"),
            _ => h.Contains(code)
        };
    }

    private static string DashboardTitleForCode(string code) => code.ToUpperInvariant() switch
    {
        "U" => "电压 U",
        "I" => "电流 I",
        "P" => "有功功率 P",
        "S" => "视在功率 S",
        "Q" => "无功功率 Q",
        "LAMBDA" => "功率因数 PF/λ",
        "PHI" => "相位角 φ",
        "FU" => "电压频率 fU",
        "FI" => "电流频率 fI",
        "UTHD" => "电压总谐波畸变率 UTHD",
        "ITHD" => "电流总谐波畸变率 ITHD",
        _ => DisplayCodeLabel(code)
    };

    private void WireEvents()
    {
        _cmbConnType.SelectedIndexChanged += (_, _) => UpdateConnectionModeUi();
        _cmbPreset.SelectedIndexChanged += (_, _) => UpdatePresetDescription();
        _btnRefreshPorts.Click += (_, _) => RefreshPorts();
        _btnDiagnose.Click += (_, _) => ShowConnectionDiagnostics();
        _btnForceRelease.Click += async (_, _) => await ForceReleasePortAsync();
        _btnRepairDriver.Click += (_, _) => RepairUsbDriver();
        _btnConnect.Click += async (_, _) => await ToggleConnectionAsync();
        _btnDisconnect.Click += async (_, _) => await DisconnectAsync();
        _btnConnectionSettings.Click += (_, _) => ShowConnectionSettingsDialog();
        _btnSend.Click += async (_, _) => await QueryOnceAsync();
        _btnReadHeader.Click += async (_, _) => await RefreshHeadersAsync();
        _btnStart.Click += async (_, _) => await TogglePollingAsync();
        _btnStop.Click += async (_, _) => await StopPollingAsync();
        _btnStartLog.Click += async (_, _) => await ToggleLoggingAsync();
        _btnStopLog.Click += async (_, _) => await StopLoggingAsync();
        _btnExportReport.Click += (_, _) => ExportReport();
        _btnApplyPreset.Click += (_, _) => ApplySelectedPreset();
        _btnSavePreset.Click += (_, _) => SaveCurrentPreset();
        _btnApplyGeneralConfig.Click += async (_, _) => await ApplyGeneralConfigAsync();
        _btnApplyNormalConfig.Click += async (_, _) => await ApplyNormalConfigAsync();
        _btnApplyListConfig.Click += async (_, _) => await ApplyListConfigAsync();
        _btnRunSetup.Click += async (_, _) => await RunSetupCommandsAsync();
        _btnApplyInput.Click += async (_, _) => await ApplyInputConfigAsync();
        _btnApplyRange.Click += async (_, _) => await ApplyRangeConfigAsync();
        _btnApplyProcessing.Click += async (_, _) => await ApplyProcessingConfigAsync();
        _btnApplyHarmonics.Click += async (_, _) => await ApplyHarmonicsConfigAsync();
        _btnQuickThirdHarmonic.Click += async (_, _) => await ConfigureThirdHarmonicAsync();
        _btnMultiOddHarmonics.Click += async (_, _) => await ConfigureMultiOddHarmonicsAsync();
        _btnReadAllConfig.Click += async (_, _) => await ReadAllConfigAsync();
        _btnRecommendedDefaults.Click += async (_, _) => await ApplyRecommendedDefaultsAsync();
        _btnApplyDisplay.Click += async (_, _) => await ApplyDisplayConfigAsync();
        _btnApplyStore.Click += async (_, _) => await ApplyStoreConfigAsync();
        _btnIntegrationStart.Click += async (_, _) => await ConfigureAndRunIntegrationAsync(":INTEGrate:STARt");
        _btnIntegrationStop.Click += async (_, _) => await ExecuteSetterCommandsAsync([":INTEGrate:STOP"]);
        _btnIntegrationReset.Click += async (_, _) => await ExecuteSetterCommandsAsync([":INTEGrate:RESet"]);
        _btnRemote.Click += async (_, _) => await ExecuteSetterCommandsAsync([":COMMunicate:REMote ON"]);
        _btnLocal.Click += async (_, _) => await ExecuteSetterCommandsAsync([":COMMunicate:REMote OFF"]);
        _btnLockPanel.Click += async (_, _) => await ExecuteSetterCommandsAsync([":COMMunicate:LOCKout ON"]);
        _btnUnlockPanel.Click += async (_, _) => await ExecuteSetterCommandsAsync([":COMMunicate:LOCKout OFF"]);
        _btnResetInstrument.Click += async (_, _) => await ExecuteSetterCommandsAsync(["*RST"]);
        _btnZero.Click += async (_, _) => await QueryAndLogAsync("*CAL?");
        _numDisplayIndex.ValueChanged += (_, _) => RefreshDisplayFunctionChoices();
        _cmbChartChannel.SelectedIndexChanged += (_, _) => RefreshChart();
        _chkLockYAxis.CheckedChanged += (_, _) => RefreshChart();
        _numChartWindow.ValueChanged += (_, _) => RefreshChart();
        _btnHoldReading.Click += async (_, _) => await ToggleHoldReadingAsync();
        _btnChartSettings.Click += (_, _) => ShowChartSettingsDialog();
        _btnPauseChart.Click += (_, _) =>
        {
            _chartPaused = !_chartPaused;
            _btnPauseChart.Text = _chartPaused ? "继续曲线" : "暂停曲线";
        };
        _btnClearChart.Click += (_, _) => { _frameBuffer.Clear(); _chart.Series.ToList().ForEach(s => s.Points.Clear()); };

        FormClosing += OnFormClosing;
    }

    private async Task ToggleHoldReadingAsync()
    {
        if (_transport is null) return;
        bool next = !_chkHold.Checked;
        await ExecuteSetterCommandsAsync([$":NUMeric:HOLD {(next ? "ON" : "OFF")}"]);
        _chkHold.Checked = next;
        UpdateHoldButtonText();
    }

    private void UpdateHoldButtonText() => _btnHoldReading.Text = _chkHold.Checked ? "取消保持" : "保持读数";

    private void ShowChartSettingsDialog()
    {
        using var dialog = new Form
        {
            Text = "图形设置",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            Width = 430,
            Height = 285,
            BackColor = Color.White,
            Font = Font
        };

        var windowPoints = new NumericUpDown { Minimum = _numChartWindow.Minimum, Maximum = _numChartWindow.Maximum, Increment = _numChartWindow.Increment, Value = _numChartWindow.Value, Dock = DockStyle.Fill };
        var lockY = new CheckBox { Text = "锁定 Y 轴范围（不勾选时按当前曲线自动缩放）", Checked = _chkLockYAxis.Checked, AutoSize = true, Dock = DockStyle.Fill };
        var yMin = NewDecimal(_numYMin.Value, _numYMin.Increment, _numYMin.Maximum, _numYMin.Minimum);
        var yMax = NewDecimal(_numYMax.Value, _numYMax.Increment, _numYMax.Maximum, _numYMax.Minimum);
        yMin.Dock = DockStyle.Fill;
        yMax.Dock = DockStyle.Fill;

        var grid = NewFormGrid(5, 120);
        grid.Padding = new Padding(16, 14, 16, 0);
        AddFormRow(grid, 0, "窗口点数", windowPoints);
        grid.Controls.Add(lockY, 1, 1);
        AddFormRow(grid, 2, "Y 最小值", yMin);
        AddFormRow(grid, 3, "Y 最大值", yMax);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12), BackColor = Color.White };
        var ok = new Button { Text = "应用", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(51, 65, 85), ForeColor = Color.White };
        var clear = new Button { Text = "清空曲线" };
        var pause = new Button { Text = _chartPaused ? "继续曲线" : "暂停曲线" };
        foreach (var button in new[] { ok, cancel, clear, pause })
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Height = 32;
            button.AutoSize = true;
            button.Padding = new Padding(12, 0, 12, 0);
            if (button != cancel)
            {
                button.BackColor = Color.FromArgb(37, 99, 235);
                button.ForeColor = Color.White;
            }
        }
        clear.Click += (_, _) => { _frameBuffer.Clear(); _chart.Series.ToList().ForEach(s => s.Points.Clear()); };
        pause.Click += (_, _) =>
        {
            _chartPaused = !_chartPaused;
            _btnPauseChart.Text = _chartPaused ? "继续曲线" : "暂停曲线";
            pause.Text = _chartPaused ? "继续曲线" : "暂停曲线";
        };
        actions.Controls.AddRange([ok, cancel, clear, pause]);
        dialog.Controls.Add(grid);
        dialog.Controls.Add(actions);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _numChartWindow.Value = windowPoints.Value;
        _chkLockYAxis.Checked = lockY.Checked;
        _numYMin.Value = yMin.Value;
        _numYMax.Value = yMax.Value;
        RefreshChart();
    }

    private void UpdatePresetDescription()
    {
        _lblPresetDescription.Text = _cmbPreset.SelectedItem is CommandPreset preset && !string.IsNullOrWhiteSpace(preset.Description)
            ? preset.Description
            : "说明：读取 PA300 当前测量列";
    }

    private void ShowConnectionSettingsDialog()
    {
        using var dialog = new Form
        {
            Text = "连接设置",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            Width = 360,
            Height = 210,
            BackColor = Color.White,
            Font = Font
        };

        var info = new Label
        {
            Text = "这些是连接维护功能，平时只需要在首页点击“连接”和“开始采集”。",
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(16, 14, 16, 0),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), WrapContents = true };
        var refresh = new Button { Text = "刷新设备" };
        var diagnose = new Button { Text = "连接诊断" };
        var repair = new Button { Text = "修复驱动" };
        var forceRelease = new Button { Text = "强制释放 USB" };
        foreach (var button in new[] { refresh, diagnose, repair, forceRelease })
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(37, 99, 235);
            button.ForeColor = Color.White;
            button.Height = 34;
            button.Width = 96;
            button.Margin = new Padding(4, 4, 8, 4);
        }
        refresh.Click += (_, _) => RefreshPorts();
        diagnose.Click += (_, _) => ShowConnectionDiagnostics();
        repair.Click += (_, _) => RepairUsbDriver();
        forceRelease.Click += async (_, _) => await ForceReleasePortAsync(forceRelease);
        actions.Controls.AddRange([refresh, diagnose, repair, forceRelease]);
        dialog.Controls.Add(actions);
        dialog.Controls.Add(info);
        dialog.ShowDialog(this);
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_shutdownComplete) return;
        e.Cancel = true;
        if (_isClosing) return;

        _isClosing = true;
        Enabled = false;
        SetStatus("正在关闭并释放串口…");

        try
        {
            SaveUiSettings();
            await SafeShutdownAsync();
        }
        finally
        {
            _shutdownComplete = true;
            BeginInvoke(new Action(Close));
        }
    }

    private void SetRecommendedDisplayPanelDefaults()
    {
        SelectDisplayCode(_cmbDisplayA, "U");
        SelectDisplayCode(_cmbDisplayB, "I");
        SelectDisplayCode(_cmbDisplayC, "P");
        SelectDisplayCode(_cmbDisplayD, "LAMBDA");
    }

    private void RefreshDisplayFunctionChoices()
    {
        string previous = _cmbDisplayFunction.Text;
        _cmbDisplayFunction.Items.Clear();
        _cmbDisplayFunction.Items.AddRange(AllowedNormalDisplayFunctions((int)_numDisplayIndex.Value).Cast<object>().ToArray());
        int index = _cmbDisplayFunction.Items.Cast<object>().ToList().FindIndex(x =>
            string.Equals(x?.ToString(), previous, StringComparison.OrdinalIgnoreCase));
        _cmbDisplayFunction.SelectedIndex = index >= 0 ? index : 0;
    }

    private void ApplyDisplayItemResponse(ComboBox target, string response)
    {
        string value = response.Trim();
        int lastSpace = value.LastIndexOf(' ');
        if (lastSpace >= 0) value = value[(lastSpace + 1)..].Trim();
        string[] parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        for (int i = 0; i < target.Items.Count; i++)
        {
            if (string.Equals(DisplayCodeFromItem(target.Items[i]), parts[0], StringComparison.OrdinalIgnoreCase))
            {
                target.SelectedIndex = i;
                break;
            }
        }
    }

    private void UpdateConnectionModeUi()
    {
        bool tcp = string.Equals(_cmbConnType.Text, "TCP/IP", StringComparison.OrdinalIgnoreCase);
        bool serial = string.Equals(_cmbConnType.Text, "RS-232", StringComparison.OrdinalIgnoreCase);
        _txtIp.Enabled = tcp;
        _numPort.Enabled = tcp;
        _cmbCom.Enabled = serial;
        _cmbBaud.Enabled = serial;
        _btnRefreshPorts.Enabled = serial || _cmbConnType.Text == "USB";
    }

    private void RefreshPorts()
    {
        string prev = _cmbCom.Text;
        _cmbCom.Items.Clear();

        // 标准串口枚举
        var ports = System.IO.Ports.SerialPort.GetPortNames().OrderBy(x => x).ToList();

        var paDevices = Pa300UsbDiscovery.FindPresentDevices();

        var preferred = paDevices.FirstOrDefault(x => x.Service.Equals("WinUSB", StringComparison.OrdinalIgnoreCase));
        _lblDevice.Text = preferred is not null
            ? "检测到 PA300 · WinUSB 驱动就绪"
            : paDevices.Count > 0 ? $"检测到 PA300 · {paDevices[0].Service}" : "未发现 PA300 USB 设备";

        _cmbCom.Items.AddRange(ports.ToArray());
        if (ports.Contains(prev)) _cmbCom.SelectedItem = prev;
        else if (!string.IsNullOrWhiteSpace(_settings.ComPort) && ports.Contains(_settings.ComPort)) _cmbCom.SelectedItem = _settings.ComPort;
        else if (ports.Count > 0)
        {
            _cmbCom.SelectedItem = ports.OrderBy(PortNumber).Last();
            _cmbBaud.SelectedItem = "115200";
        }
    }

    private static int PortNumber(string portName) =>
        int.TryParse(new string(portName.Where(char.IsDigit).ToArray()), out int value) ? value : 0;

    private async Task ConnectAsync()
    {
        // 立即显示连接状态，防止用户重复点击
        SetStatus("正在连接...");
        _btnConnect.Enabled = false;
        Cursor = Cursors.WaitCursor;

        try
        {
            await DisconnectAsync();
            SaveUiSettings();

            string idn;
            if (_cmbConnType.Text == "RS-232")
            {
                int preferredBaud = int.TryParse(_cmbBaud.Text, out int baud) ? baud : 115200;
                (_transport, idn, int detectedBaud) = await OpenRs232AutoDetectAsync(_cmbCom.Text.Trim(), preferredBaud);
                _cmbBaud.SelectedItem = detectedBaud.ToString(CultureInfo.InvariantCulture);
                _settings.BaudRate = detectedBaud;
                SettingsStore.Save(_settings);
            }
            else
            {
                _transport = _cmbConnType.Text == "TCP/IP"
                    ? new TcpScpiTransport(_txtIp.Text.Trim(), (int)_numPort.Value)
                    : await CreateUsbTransportAsync();
                using var openCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _transport.OpenAsync(openCts.Token);
                idn = await QueryIdentityWithRetryAsync(_transport);
            }

            SetConnectedState(true);
            SetStatus($"已连接 | {idn}");
            AppendRaw($"> *IDN?\r\n{idn}\r\n");
            await RefreshHeadersAsync();
        }
        catch (UnauthorizedAccessException)
        {
            await CleanupTransportAsync();
            SetConnectedState(false);
            SetStatus("连接失败: 端口被占用");
            MessageBox.Show("端口被占用 (Access Denied)\n" +
                "该串口已被其他程序占用，请关闭其他串口助手后重试",
                "端口占用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            await CleanupTransportAsync();
            SetConnectedState(false);
            SetStatus($"连接失败: {ex.Message}");
            MessageBox.Show($"连接失败\n{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (_transport is null) _btnConnect.Enabled = true;
            Cursor = Cursors.Default;
        }
    }

    private static async Task<(IScpiTransport Transport, string Identity, int Baud)> OpenRs232AutoDetectAsync(
        string portName, int preferredBaud)
    {
        Exception? lastError = null;
        int[] candidates = [preferredBaud, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];
        foreach (int baud in candidates)
        {
            var transport = new SerialScpiTransport(portName, baud);
            try
            {
                using var openCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await transport.OpenAsync(openCts.Token);
                using var queryCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1600));
                string idn = await transport.QueryAsync("*IDN?", queryCts.Token);
                if (!string.IsNullOrWhiteSpace(idn)) return (transport, idn, baud);
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or IOException)
            {
                lastError = ex;
            }
            await transport.DisposeAsync();
            await Task.Delay(80);
        }
        throw new TimeoutException($"{portName} 已打开，但自动扫描全部波特率后 PA300 仍未响应。", lastError);
    }

    private static async Task<string> QueryIdentityWithRetryAsync(IScpiTransport transport)
    {
        Exception? lastError = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            using var queryCts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            try
            {
                string idn = await transport.QueryAsync("*IDN?", queryCts.Token);
                if (!string.IsNullOrWhiteSpace(idn)) return idn;
                lastError = new TimeoutException("设备返回空响应");
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or IOException)
            {
                lastError = ex;
            }

            if (attempt < 3) await Task.Delay(600);
        }

        throw new TimeoutException("PA300 已打开，但连续 3 次未响应 *IDN?。请确认仪器 RS-232 参数为 115200、8N1。", lastError);
    }

    /// <summary>
    /// 创建 USB 传输层：先试 WinUSB 直连，失败则回退到串口。
    /// </summary>
    private Task<IScpiTransport> CreateUsbTransportAsync()
    {
        var device = Pa300UsbDiscovery.FindPresentDevices().FirstOrDefault();
        if (device?.IsSerial == true)
            throw new InvalidOperationException(
                $"检测到 {device.PortName} 使用 {device.Service} 串口驱动，但 PA300 USB 使用自定义协议。" +
                "请点击“修复驱动”安装项目随附的致远 WinUSB 驱动，然后重新插拔设备。");
        SetStatus("正在通过 WinUSB 连接...");
        return Task.FromResult<IScpiTransport>(new WinUsbScpiTransport());
    }

    private void ShowConnectionDiagnostics()
    {
        var devices = Pa300UsbDiscovery.FindPresentDevices();
        string detail = devices.Count == 0
            ? "未检测到 VID_04CC / PID_121B。\n\n请检查设备电源、USB 数据线和设备管理器。"
            : string.Join("\n\n", devices.Select(d =>
                $"设备：{d.DisplayName}\n端口：{(string.IsNullOrWhiteSpace(d.PortName) ? "WinUSB" : d.PortName)}\n驱动：{d.Service}\n实例：{d.InstanceId}"));
        MessageBox.Show(detail, "PA300 连接诊断", MessageBoxButtons.OK,
            devices.Count == 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private void RepairUsbDriver()
    {
        string? installer = FindDriverInstaller();
        if (installer is null)
        {
            MessageBox.Show("未找到 PA300-USB\\Drivers\\InstallAll.bat。", "驱动修复",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var answer = MessageBox.Show(
            "将以管理员权限运行项目随附的 PA300 官方驱动安装器。\n\n" +
            "安装过程中设备会短暂断开。完成后请重新插拔 USB，再点击“刷新USB端口”。",
            "修复 PA300 USB 驱动", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
        if (answer != DialogResult.OK) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = installer,
                WorkingDirectory = Path.GetDirectoryName(installer)!,
                UseShellExecute = true,
                Verb = "runas"
            });
            SetStatus("驱动安装器已启动");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            SetStatus("已取消驱动修复");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法启动驱动安装器：{ex.Message}", "驱动修复",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string? FindDriverInstaller()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string[] candidates =
            [
                Path.Combine(directory.FullName, "PA300-USB", "Drivers", "InstallAll.bat"),
                Path.Combine(directory.FullName, "drivers", "PA300-USB", "Drivers", "InstallAll.bat")
            ];
            foreach (string candidate in candidates)
                if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        return null;
    }

    /// <summary>
    /// 强制释放USB端口：通过 devcon 重启 PA300 设备，等效于物理拔插 USB。
    /// </summary>
    private async Task ForceReleasePortAsync(Button? sourceButton = null)
    {
        string devconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devcon_x64.exe");
        if (!File.Exists(devconPath))
        {
            var result = MessageBox.Show(
                "找不到 devcon_x64.exe，是否在指定目录查找？",
                "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "请选择 devcon_x64.exe",
                    Filter = "devcon_x64.exe|devcon_x64.exe",
                    InitialDirectory = Path.GetFullPath(Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\PA300-USB\Drivers"))
                };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                devconPath = dialog.FileName;
            }
            else return;
        }

        SetStatus("正在强制释放 USB 端口...");
        var button = sourceButton ?? _btnForceRelease;
        button.Enabled = false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = devconPath,
                Arguments = @"restart ""USB\VID_04CC&PID_121B""",
                UseShellExecute = true,
                Verb = "runas", // 请求管理员权限
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                SetStatus("释放失败：无法启动 devcon");
                return;
            }

            // 等待设备重启完成
            await Task.Run(() => process.WaitForExit(15000));

            SetStatus("等待设备重新就绪...");
            await Task.Delay(3000);

            // 刷新端口列表
            RefreshPorts();
            SetStatus("USB 端口已释放，请重新连接");
        }
        catch (Exception ex)
        {
            SetStatus($"释放失败: {ex.Message}");
            MessageBox.Show($"无法释放 USB 端口\n{ex.Message}\n\n请手动重新插拔 USB 设备。",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            button.Enabled = true;
        }
    }

    private async Task CleanupTransportAsync()
    {
        await _ioLock.WaitAsync();
        try
        {
        if (_transport is not null)
        {
            try { await _transport.DisposeAsync(); } catch { }
            _transport = null;
        }
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task DisconnectAsync()
    {
        await StopPollingAsync();
        await _ioLock.WaitAsync();
        try
        {
        if (_transport is not null)
        {
            await _transport.DisposeAsync();
            _transport = null;
        }
        }
        finally
        {
            _ioLock.Release();
        }

        SetConnectedState(false);
        SetStatus("未连接");
    }

    private async Task ToggleConnectionAsync()
    {
        if (_transport is null)
            await ConnectAsync();
        else
            await DisconnectAsync();
    }

    private void SetConnectedState(bool connected)
    {
        _btnConnect.Enabled = true;
        _btnConnect.Text = connected ? "断开" : "连接";
        _btnDisconnect.Enabled = connected;
        _btnSend.Enabled = connected;
        _btnReadHeader.Enabled = connected;
        _btnStart.Enabled = connected;
        _btnStart.Text = _pollTask is null ? "开始采集" : "停止采集";
        _btnStartLog.Enabled = connected || _logger.IsRunning;
        _btnHoldReading.Enabled = connected;
        _btnApplyGeneralConfig.Enabled = connected;
        _btnApplyNormalConfig.Enabled = connected;
        _btnApplyListConfig.Enabled = connected;
        _btnRunSetup.Enabled = connected;
        foreach (var control in new Control[]
                 {
                     _btnApplyInput, _btnApplyRange, _btnApplyProcessing, _btnApplyHarmonics, _btnQuickThirdHarmonic, _btnMultiOddHarmonics,
                     _btnReadAllConfig,
                     _btnRecommendedDefaults,
                     _btnApplyDisplay, _btnApplyStore,
                     _btnIntegrationStart, _btnIntegrationStop, _btnIntegrationReset,
                     _btnRemote, _btnLocal, _btnLockPanel, _btnUnlockPanel,
                     _btnResetInstrument, _btnZero
                 })
            control.Enabled = connected;

        if (!connected)
        {
            _btnStop.Enabled = false;
            _btnStopLog.Enabled = false;
            _btnStart.Text = "开始采集";
        }
    }

    private async Task QueryOnceAsync()
    {
        if (_transport is null) return;
        try
        {
            string cmd = Normalize(_txtQueryCmd.Text);
            byte[] raw = await _transport.QueryRawAsync(cmd);
            AppendRaw($"> {cmd}\r\n{ScpiValueParser.FormatRawPreview(raw)}\r\n");
        }
        catch (Exception ex)
        {
            AppendRaw($"单次读取失败: {ex.Message}\r\n");
        }
    }

    private async Task RefreshHeadersAsync()
    {
        if (_transport is null) return;

        try
        {
            if (IsListQueryCommand(_txtQueryCmd.Text) && string.IsNullOrWhiteSpace(_txtHeaderCmd.Text))
            {
                if (_showOddHarmonicsFromThird)
                {
                    _currentHeaders = BuildMultiOddHarmonicHeaders();
                    BuildGrid(_currentHeaders);
                    BuildSeries(_currentHeaders);
                    BuildDashboard(_currentHeaders);
                    AppendRaw($"已使用多次电流谐波中文表头，共 {_currentHeaders.Count} 项\r\n");
                    return;
                }

                _currentHeaders = await QueryListHeadersFromDeviceAsync();
                if (_currentHeaders.Count == 0)
                    _currentHeaders = BuildListHeadersFromCurrentUi(0);

                BuildGrid(_currentHeaders);
                BuildSeries(_currentHeaders);
                BuildDashboard(_currentHeaders);
                AppendRaw($"已同步谐波列表表头，共 {_currentHeaders.Count} 项\r\n");
                return;
            }

            string cmd = Normalize(_txtHeaderCmd.Text);
            if (string.IsNullOrWhiteSpace(cmd)) return;

            string rsp = await _transport.QueryAsync(cmd);
            var headers = ScpiValueParser.ParseHeaders(rsp);
            if (headers.Count == 0)
            {
                AppendRaw($"> {cmd}\r\n{rsp}\r\n");
                return;
            }

            _currentHeaders = headers.Select(FormatHeaderForDisplay).ToList();
            BuildGrid(_currentHeaders);
            BuildSeries(_currentHeaders);
            BuildDashboard(_currentHeaders);
            AppendRaw($"> {cmd}\r\n{rsp}\r\n");
        }
        catch (Exception ex)
        {
            AppendRaw($"读取列名失败: {ex.Message}\r\n");
        }
    }

    private async Task ApplyGeneralConfigAsync()
    {
        if (_transport is null) return;

        var commands = new[]
        {
            $":NUMeric:FORMat {_cmbNumericFormat.Text}",
            $":RATE {RateScpiValue(_cmbRate.Text)}",
            $":NUMeric:HOLD {(_chkHold.Checked ? "ON" : "OFF")}"
        };

        await ExecuteSetterCommandsAsync(commands);
        SaveUiSettings();
        UpdateHoldButtonText();
        SetStatus("基础配置已下发");
    }

    private async Task ApplyNormalConfigAsync()
    {
        if (_transport is null) return;
        _showOddHarmonicsFromThird = false;

        var commands = new[]
        {
            $":NUMeric:NORMal:PRESet {(int)_numNormalPreset.Value}",
            $":NUMeric:NORMal:NUMber {(int)_numNormalCount.Value}"
        };

        await ExecuteSetterCommandsAsync(commands);
        SaveUiSettings();
        if (!string.IsNullOrWhiteSpace(_txtHeaderCmd.Text))
            await RefreshHeadersAsync();
        SetStatus("常规输出已配置");
    }

    private async Task ApplyListConfigAsync()
    {
        if (_transport is null) return;
        _showOddHarmonicsFromThird = false;

        var commands = new[]
        {
            $":NUMeric:LIST:PRESet {(int)_numListPreset.Value}",
            $":NUMeric:LIST:NUMber {(int)_numListCount.Value}",
            $":NUMeric:LIST:ORDer {(int)_numListOrder.Value}",
            $":NUMeric:LIST:SELect {_cmbListSelect.Text}"
        };

        await ExecuteSetterCommandsAsync(commands);
        SaveUiSettings();
        if (IsListQueryCommand(_txtQueryCmd.Text))
            await RefreshHeadersAsync();
        SetStatus("谐波输出已配置");
    }

    private async Task ApplyInputConfigAsync()
    {
        await ExecuteSetterCommandsAsync([
            $":INPut:WIRing {_cmbWiring.Text}",
            $":INPut:MODE {_cmbInputMode.Text}",
            $":INPut:SYNChronize {_cmbSyncSource.Text}",
            $":INPut:VOLTage:RANGe {_cmbVoltageRange.Text}",
            $":INPut:VOLTage:AUTO {(_chkVoltageAuto.Checked ? "ON" : "OFF")}",
            $":INPut:CURRent:RANGe {_cmbCurrentRange.Text}",
            $":INPut:CURRent:AUTO {(_chkCurrentAuto.Checked ? "ON" : "OFF")}"
        ]);
        SetStatus("接线与量程配置已应用");
    }

    private async Task ApplyRangeConfigAsync()
    {
        await ExecuteSetterCommandsAsync([
            $":INPut:VOLTage:RANGe {_cmbVoltageRange.Text}",
            $":INPut:VOLTage:AUTO {(_chkVoltageAuto.Checked ? "ON" : "OFF")}",
            $":INPut:CURRent:RANGe {_cmbCurrentRange.Text}",
            $":INPut:CURRent:AUTO {(_chkCurrentAuto.Checked ? "ON" : "OFF")}"
        ]);
        SetStatus("量程配置已应用");
    }

    private async Task ReadAllConfigAsync()
    {
        if (_transport is null) return;

        _btnReadAllConfig.Enabled = false;
        SetStatus("正在读取仪器全部配置…");
        int success = 0;
        int unsupported = 0;
        Control[] configControls =
        [
            _cmbNumericFormat, _cmbRate, _chkHold,
            _cmbWiring, _cmbInputMode, _cmbSyncSource, _cmbVoltageRange, _chkVoltageAuto, _cmbCurrentRange, _chkCurrentAuto,
            _chkInputFilter, _chkLineFilter, _numCrestFactor, _chkScaling, _numPtRatio, _numCtRatio, _numScaleFactor,
            _chkMaxHold, _chkAveraging, _cmbAverageType, _numAverageCount,
            _chkHarmonics, _numHarmonicElement, _cmbHarmonicSync, _numHarmonicDisplayOrder, _cmbHarmonicThd,
            _cmbIntegrationMode, _numIntegrationHour, _numIntegrationMinute, _numIntegrationSecond,
            _numNormalPreset, _numNormalCount, _numListPreset, _numListCount, _numListOrder, _cmbListSelect,
            _cmbDisplayA, _cmbDisplayB, _cmbDisplayC, _cmbDisplayD,
            _chkStore, _numStoreHour, _numStoreMinute, _numStoreSecond
        ];
        ResetConfigReadMarkers(configControls);

        static string Clean(string value) => value.Trim().Trim('"').Split('\r', '\n')[0].Trim();
        static bool IsOn(string value) => Clean(value) is "1" or "ON" or "TRUE";
        static string? SegmentValue(string snapshot, string key)
        {
            foreach (string raw in snapshot.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                string segment = raw.Trim().TrimStart(':');
                if (!segment.StartsWith(key, StringComparison.OrdinalIgnoreCase)) continue;
                string value = segment[key.Length..].Trim();
                return value.Length > 0 ? value : null;
            }
            return null;
        }
        static void SetCombo(ComboBox combo, string value)
        {
            string wanted = Clean(value).Split(',')[0].Trim();
            wanted = wanted.StartsWith(':') && wanted.Contains(' ')
                ? wanted[(wanted.LastIndexOf(' ') + 1)..].Trim()
                : wanted;
            for (int i = 0; i < combo.Items.Count; i++)
                if (string.Equals(combo.Items[i]?.ToString(), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
        }
        static void SetComboByNormalizedNumber(ComboBox combo, string value)
        {
            string token = Clean(value).Split(',')[0].Trim();
            token = token.StartsWith(':') && token.Contains(' ')
                ? token[(token.LastIndexOf(' ') + 1)..].Trim()
                : token;
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return;
            bool voltageCombo = combo.Items.Cast<object>().Any(x => x?.ToString()?.EndsWith("V", StringComparison.OrdinalIgnoreCase) == true);
            string wanted = voltageCombo ? $"{number:0.###}V" : FormatCurrentRangeToken(number);
            for (int i = 0; i < combo.Items.Count; i++)
                if (string.Equals(combo.Items[i]?.ToString(), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
        }
        static void SetRateCombo(ComboBox combo, string value)
        {
            string token = Clean(value).Split(',')[0].Trim();
            token = token.StartsWith(':') && token.Contains(' ')
                ? token[(token.LastIndexOf(' ') + 1)..].Trim()
                : token;
            if (!double.TryParse(token.TrimEnd('S', 's'), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)) return;
            string wanted = RateLabelFromSeconds(seconds);
            for (int i = 0; i < combo.Items.Count; i++)
                if (string.Equals(combo.Items[i]?.ToString(), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
        }
        static void SetNumber(NumericUpDown control, string value)
        {
            string token = Clean(value).Split(',')[0].Trim();
            if (!decimal.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number)) return;
            control.Value = Math.Clamp(number, control.Minimum, control.Maximum);
        }

        void MarkOk(params Control[] controls) => ResetConfigReadMarkers(controls);
        void MarkFailed(params Control[] controls) => MarkConfigReadFailed(controls);

        async Task Read(string command, Action<string> apply, params Control[] controls)
        {
            try
            {
                using var queryCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
                string response = await _transport.QueryAsync(command, queryCts.Token);
                AppendRaw($"> {command}\r\n{response}\r\n");
                string cleaned = Clean(response);
                if (cleaned.Length == 0 || cleaned.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    unsupported++;
                    MarkFailed(controls);
                    return;
                }
                apply(cleaned);
                MarkOk(controls);
                success++;
            }
            catch (Exception ex)
            {
                unsupported++;
                MarkFailed(controls);
                AppendRaw($"> {command}\r\n读取失败: {ex.Message}\r\n");
            }
        }

        try
        {
            await Read(":NUMeric:FORMat?", v => SetCombo(_cmbNumericFormat, v), _cmbNumericFormat);
            await Read(":RATE?", v => SetRateCombo(_cmbRate, v), _cmbRate);
            await Read(":NUMeric:HOLD?", v => _chkHold.Checked = IsOn(v), _chkHold);
            await Read(":INPut?", v =>
            {
                if (SegmentValue(v, "WIRing") is { } wiring) { SetCombo(_cmbWiring, wiring); MarkOk(_cmbWiring); } else MarkFailed(_cmbWiring);
                if (SegmentValue(v, "MODE") is { } mode) { SetCombo(_cmbInputMode, mode); MarkOk(_cmbInputMode); } else MarkFailed(_cmbInputMode);
                if (SegmentValue(v, "SYNChronize") is { } sync) { SetCombo(_cmbSyncSource, sync); MarkOk(_cmbSyncSource); } else MarkFailed(_cmbSyncSource);
                if (SegmentValue(v, "VOLTage:RANGe") is { } voltageRange) { SetComboByNormalizedNumber(_cmbVoltageRange, voltageRange); MarkOk(_cmbVoltageRange); } else MarkFailed(_cmbVoltageRange);
                if (SegmentValue(v, "VOLTage:AUTO") is { } voltageAuto) { _chkVoltageAuto.Checked = IsOn(voltageAuto); MarkOk(_chkVoltageAuto); } else MarkFailed(_chkVoltageAuto);
                if (SegmentValue(v, "CURRent:RANGe") is { } currentRange) { SetComboByNormalizedNumber(_cmbCurrentRange, currentRange); MarkOk(_cmbCurrentRange); } else MarkFailed(_cmbCurrentRange);
                if (SegmentValue(v, "CURRent:AUTO") is { } currentAuto) { _chkCurrentAuto.Checked = IsOn(currentAuto); MarkOk(_chkCurrentAuto); } else MarkFailed(_chkCurrentAuto);
                if (SegmentValue(v, "FILTer:LINE") is { } lineFilter) { _chkLineFilter.Checked = IsOn(lineFilter); MarkOk(_chkLineFilter); } else MarkFailed(_chkLineFilter);
                if (SegmentValue(v, "FILTer:FREQuency") is { } inputFilter) { _chkInputFilter.Checked = !inputFilter.StartsWith("0", StringComparison.OrdinalIgnoreCase); MarkOk(_chkInputFilter); } else MarkFailed(_chkInputFilter);
                if (SegmentValue(v, "CFACtor") is { } crest) { SetNumber(_numCrestFactor, crest); MarkOk(_numCrestFactor); } else MarkFailed(_numCrestFactor);
                if (SegmentValue(v, "SCALing:STATe") is { } scaling) { _chkScaling.Checked = IsOn(scaling); MarkOk(_chkScaling); } else MarkFailed(_chkScaling);
                if (SegmentValue(v, "SCALing:VT") is { } pt) { SetNumber(_numPtRatio, pt); MarkOk(_numPtRatio); } else MarkFailed(_numPtRatio);
                if (SegmentValue(v, "SCALing:CT") is { } ct) { SetNumber(_numCtRatio, ct); MarkOk(_numCtRatio); } else MarkFailed(_numCtRatio);
                if (SegmentValue(v, "SCALing:SFACtor") is { } sf) { SetNumber(_numScaleFactor, sf); MarkOk(_numScaleFactor); } else MarkFailed(_numScaleFactor);
            });
            await Read(":CONFigure:MHOLd:STATe?", v => _chkMaxHold.Checked = IsOn(v), _chkMaxHold);
            await Read(":CONFigure:AVERaging:STATe?", v => _chkAveraging.Checked = IsOn(v), _chkAveraging);
            await Read(":CONFigure:AVERaging:TYPE?", v =>
            {
                SetCombo(_cmbAverageType, v);
                string[] parts = Clean(v).Split(',');
                if (parts.Length > 1) SetNumber(_numAverageCount, parts[1]);
            }, _cmbAverageType, _numAverageCount);
            await Read(":HARMonics:STATe?", v => _chkHarmonics.Checked = IsOn(v), _chkHarmonics);
            await Read(":HARMonics:ELEMent?", v => SetNumber(_numHarmonicElement, v), _numHarmonicElement);
            await Read(":HARMonics:SYNChronize?", v => SetCombo(_cmbHarmonicSync, v), _cmbHarmonicSync);
            await Read(":HARMonics:DISPlay:ORDer?", v => SetNumber(_numHarmonicDisplayOrder, v), _numHarmonicDisplayOrder);
            await Read(":HARMonics:THD?", v => SetCombo(_cmbHarmonicThd, v), _cmbHarmonicThd);
            await Read(":INTEGrate:MODE?", v => SetCombo(_cmbIntegrationMode, v), _cmbIntegrationMode);
            await Read(":INTEGrate:TIMer?", v =>
            {
                string[] parts = Clean(v).Split(',');
                if (parts.Length > 0) SetNumber(_numIntegrationHour, parts[0]);
                if (parts.Length > 1) SetNumber(_numIntegrationMinute, parts[1]);
                if (parts.Length > 2) SetNumber(_numIntegrationSecond, parts[2]);
            }, _numIntegrationHour, _numIntegrationMinute, _numIntegrationSecond);
            await Read(":NUMeric:NORMal:PRESet?", v => SetNumber(_numNormalPreset, v), _numNormalPreset);
            await Read(":NUMeric:NORMal:NUMber?", v => SetNumber(_numNormalCount, v), _numNormalCount);
            await Read(":NUMeric:LIST:PRESet?", v => SetNumber(_numListPreset, v), _numListPreset);
            await Read(":NUMeric:LIST:NUMber?", v => SetNumber(_numListCount, v), _numListCount);
            await Read(":NUMeric:LIST:ORDer?", v => SetNumber(_numListOrder, v), _numListOrder);
            await Read(":NUMeric:LIST:SELect?", v => SetCombo(_cmbListSelect, v), _cmbListSelect);
            await Read(":DISPlay:NORMal:ITEM1?", v => ApplyDisplayItemResponse(_cmbDisplayA, v), _cmbDisplayA);
            await Read(":DISPlay:NORMal:ITEM2?", v => ApplyDisplayItemResponse(_cmbDisplayB, v), _cmbDisplayB);
            await Read(":DISPlay:NORMal:ITEM3?", v => ApplyDisplayItemResponse(_cmbDisplayC, v), _cmbDisplayC);
            await Read(":DISPlay:NORMal:ITEM4?", v => ApplyDisplayItemResponse(_cmbDisplayD, v), _cmbDisplayD);
            await Read(":STORe:STATe?", v => _chkStore.Checked = IsOn(v), _chkStore);
            await Read(":STORe:INTerval?", v =>
            {
                string[] parts = Clean(v).Split(',');
                if (parts.Length > 0) SetNumber(_numStoreHour, parts[0]);
                if (parts.Length > 1) SetNumber(_numStoreMinute, parts[1]);
                if (parts.Length > 2) SetNumber(_numStoreSecond, parts[2]);
            }, _numStoreHour, _numStoreMinute, _numStoreSecond);
            UpdateHoldButtonText();
            BuildDashboardFromDisplayPanels();
            SetStatus($"配置读取完成：{success} 项有效，{unsupported} 项未返回/不支持");
        }
        finally
        {
            _btnReadAllConfig.Enabled = _transport is not null;
        }
    }

    private async Task ApplyDisplayConfigAsync()
    {
        await ExecuteSetterCommandsAsync([
            BuildDisplayItemCommand(1, DisplayCodeFromItem(_cmbDisplayA.SelectedItem)),
            BuildDisplayItemCommand(2, DisplayCodeFromItem(_cmbDisplayB.SelectedItem)),
            BuildDisplayItemCommand(3, DisplayCodeFromItem(_cmbDisplayC.SelectedItem)),
            BuildDisplayItemCommand(4, DisplayCodeFromItem(_cmbDisplayD.SelectedItem))
        ]);
        await QueryAndLogAsync(":DISPlay?");
        await QueryAndLogAsync(":DISPlay:NORMal:ITEM1?");
        await QueryAndLogAsync(":DISPlay:NORMal:ITEM2?");
        await QueryAndLogAsync(":DISPlay:NORMal:ITEM3?");
        await QueryAndLogAsync(":DISPlay:NORMal:ITEM4?");
        BuildDashboardFromDisplayPanels();
        SetStatus("仪器 ABCD 四块显示已下发并读回确认");
    }

    private async Task ApplyStoreConfigAsync()
    {
        await ExecuteSetterCommandsAsync([
            $":STORe:STATe {(_chkStore.Checked ? "ON" : "OFF")}",
            $":STORe:INTerval {(int)_numStoreHour.Value},{(int)_numStoreMinute.Value},{(int)_numStoreSecond.Value}"
        ]);
        SetStatus("仪器内部存储配置已应用");
    }

    private async Task ApplyProcessingConfigAsync()
    {
        string F(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        await ExecuteSetterCommandsAsync([
            $":CONFigure:FILTer {(_chkInputFilter.Checked ? "ON" : "OFF")}",
            $":CONFigure:LFILTer {(_chkLineFilter.Checked ? "ON" : "OFF")}",
            $":CONFigure:MHOLd:STATe {(_chkMaxHold.Checked ? "ON" : "OFF")}",
            $":CONFigure:AVERaging:STATe {(_chkAveraging.Checked ? "ON" : "OFF")}",
            $":CONFigure:AVERaging:TYPE {_cmbAverageType.Text},{(int)_numAverageCount.Value}",
            $":CONFigure:CFACtor {(int)_numCrestFactor.Value}",
            $":CONFigure:SCALing:PT:ALL {F(_numPtRatio.Value)}",
            $":CONFigure:SCALing:CT:ALL {F(_numCtRatio.Value)}",
            $":CONFigure:SCALing:SFACtor:ALL {F(_numScaleFactor.Value)}",
            $":CONFigure:SCALing:STATe {(_chkScaling.Checked ? "ON" : "OFF")}"
        ]);
        SetStatus("滤波、平均与变比配置已应用");
    }

    private async Task ApplyHarmonicsConfigAsync()
    {
        int element = (int)_numHarmonicElement.Value;
        await ExecuteSetterCommandsAsync([
            $":HARMonics:STATe {(_chkHarmonics.Checked ? "ON" : "OFF")}",
            $":HARMonics:ELEMent {element}",
            $":HARMonics:SYNChronize {_cmbHarmonicSync.Text},{element}",
            $":HARMonics:DISPlay:ORDer {(int)_numHarmonicDisplayOrder.Value}",
            $":HARMonics:THD {_cmbHarmonicThd.Text}"
        ]);
        SetStatus("谐波分析配置已应用");
    }

    private async Task ConfigureThirdHarmonicAsync()
    {
        if (_transport is null) return;

        _chkHarmonics.Checked = true;
        _numHarmonicElement.Value = 1;
        _numHarmonicDisplayOrder.Value = 3;
        _numListOrder.Value = 3;
        _cmbListSelect.SelectedItem = "ALL";
        _numListPreset.Value = 1;
        _numListCount.Value = 1;
        _txtHeaderCmd.Text = string.Empty;
        _txtQueryCmd.Text = ":NUMeric:LIST:VALue?";

        await ExecuteSetterCommandsAsync([
            ":HARMonics:STATe ON",
            ":HARMonics:ELEMent 1",
            $":HARMonics:SYNChronize {_cmbHarmonicSync.Text},1",
            ":HARMonics:ORDer 1,3",
            ":HARMonics:DISPlay:ORDer 3",
            $":HARMonics:THD {_cmbHarmonicThd.Text}",
            ":NUMeric:LIST:PRESet 1",
            ":NUMeric:LIST:NUMber 1",
            ":NUMeric:LIST:ORDer 3",
            ":NUMeric:LIST:SELect ALL"
        ]);

        await RefreshHeadersAsync();
        SetStatus("已切换到三次谐波测量：读取 1~3 次谐波列表");
    }

    private async Task ConfigureMultiOddHarmonicsAsync()
    {
        if (_transport is null) return;

        int maxOrder = ParseHarmonicOrder(_cmbMultiHarmonicMax.Text, 11);
        if (maxOrder < 3) maxOrder = 3;
        if (maxOrder % 2 == 0) maxOrder -= 1;
        _multiHarmonicMaxOrder = Math.Clamp(maxOrder, 3, 49);
        _showOddHarmonicsFromThird = true;

        _chkHarmonics.Checked = true;
        _numHarmonicElement.Value = 1;
        _numHarmonicDisplayOrder.Value = Math.Min(_multiHarmonicMaxOrder, 50);
        _numListPreset.Value = 1;
        _numListCount.Value = 1;
        _numListOrder.Value = _multiHarmonicMaxOrder;
        _cmbListSelect.SelectedItem = "ODD";
        _txtHeaderCmd.Text = string.Empty;
        _txtQueryCmd.Text = ":NUMeric:LIST:VALue?";

        await ExecuteSetterCommandsAsync([
            ":HARMonics:STATe ON",
            ":HARMonics:ELEMent 1",
            $":HARMonics:SYNChronize {_cmbHarmonicSync.Text},1",
            $":HARMonics:ORDer 1,{_multiHarmonicMaxOrder}",
            $":HARMonics:DISPlay:ORDer {_multiHarmonicMaxOrder}",
            $":HARMonics:THD {_cmbHarmonicThd.Text}",
            ":NUMeric:LIST:PRESet 1",
            ":NUMeric:LIST:NUMber 1",
            ":NUMeric:LIST:ITEM1 I,1",
            $":NUMeric:LIST:ORDer {_multiHarmonicMaxOrder}",
            ":NUMeric:LIST:SELect ODD"
        ]);

        _currentHeaders = BuildMultiOddHarmonicHeaders();
        BuildGrid(_currentHeaders);
        BuildSeries(_currentHeaders);
        BuildDashboard(_currentHeaders);
        SetStatus($"已切换到多次电流谐波：同时显示 3~{_multiHarmonicMaxOrder} 次奇次电流谐波");
    }

    private async Task ApplyRecommendedDefaultsAsync()
    {
        if (_transport is null) return;
        bool resumePolling = _pollTask is not null;
        if (resumePolling)
            await StopPollingAsync();

        _showOddHarmonicsFromThird = false;

        _cmbVoltageRange.SelectedItem = "600V";
        _cmbCurrentRange.SelectedItem = "20A";
        _chkVoltageAuto.Checked = false;
        _chkCurrentAuto.Checked = false;
        _chkHarmonics.Checked = true;
        _numHarmonicElement.Value = 1;
        _numHarmonicDisplayOrder.Value = 50;
        SetRecommendedDisplayPanelDefaults();
        _txtHeaderCmd.Text = ":NUMeric:NORMal:HEADer?";
        _txtQueryCmd.Text = ":NUMeric:NORMal:VALue?";

        await ExecuteSetterCommandsAsync([
            ":INPut:VOLTage:RANGe 600V",
            ":INPut:VOLTage:AUTO OFF",
            ":INPut:CURRent:RANGe 20A",
            ":INPut:CURRent:AUTO OFF",
            ":HARMonics:STATe ON",
            ":HARMonics:ELEMent 1",
            $":HARMonics:SYNChronize {_cmbHarmonicSync.Text},1",
            ":HARMonics:ORDer 1,50",
            ":HARMonics:DISPlay:ORDer 50",
            BuildDisplayItemCommand(1, "U"),
            BuildDisplayItemCommand(2, "I"),
            BuildDisplayItemCommand(3, "P"),
            BuildDisplayItemCommand(4, "LAMBDA")
        ]);

        await QueryAndLogAsync(":INPut?");
        await QueryAndLogAsync(":DISPlay?");
        await RefreshHeadersAsync();
        BuildDashboardFromDisplayPanels();
        SetStatus("已恢复推荐默认：600V / 20A / A电压 / B电流 / C有功功率 / D功率因数");

        if (resumePolling && _transport is not null)
            StartPolling();
    }

    private async Task ConfigureAndRunIntegrationAsync(string action)
    {
        await ExecuteSetterCommandsAsync([
            $":INTEGrate:MODE {_cmbIntegrationMode.Text}",
            $":INTEGrate:TIMer {(int)_numIntegrationHour.Value},{(int)_numIntegrationMinute.Value},{(int)_numIntegrationSecond.Value}",
            action
        ]);
        SetStatus("积分已开始");
    }

    private async Task QueryAndLogAsync(string command)
    {
        if (_transport is null) return;
        try
        {
            string response = await _transport.QueryAsync(command);
            AppendRaw($"> {command}\r\n{response}\r\n");
        }
        catch (Exception ex) { AppendRaw($"{command} 失败: {ex.Message}\r\n"); }
    }

    private async Task RunSetupCommandsAsync()
    {
        if (_transport is null) return;

        var commands = ParseSetupCommands(_txtSetupCommands.Text);
        if (commands.Count == 0)
        {
            AppendRaw("没有可执行的批量命令\r\n");
            return;
        }

        foreach (var command in commands)
        {
            if (command.EndsWith("?", StringComparison.Ordinal))
            {
                string response = await _transport.QueryAsync(command);
                AppendRaw($"> {command}\r\n{response}\r\n");
            }
            else
            {
                await _transport.WriteAsync(command);
                AppendRaw($"> {command}\r\n");
            }
        }

        SaveUiSettings();
        if (IsListQueryCommand(_txtQueryCmd.Text) || !string.IsNullOrWhiteSpace(_txtHeaderCmd.Text))
            await RefreshHeadersAsync();
    }

    private async Task ExecuteSetterCommandsAsync(IEnumerable<string> commands)
    {
        if (_transport is null) return;

        await _ioLock.WaitAsync();
        try
        {
            if (_transport is null) return;
            foreach (var command in commands.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                await _transport.WriteAsync(command);
                AppendRaw($"> {command}\r\n");
            }
        }
        catch (ObjectDisposedException)
        {
            AppendRaw("通信对象已释放，命令已取消\r\n");
        }
        finally
        {
            _ioLock.Release();
        }
    }
    private void StartPolling()
    {
        if (_transport is null || _pollTask is not null) return;
        _pollCts = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollLoopAsync(_pollCts.Token));
        _btnStart.Enabled = true;
        _btnStart.Text = "停止采集";
        _btnStop.Enabled = true;
        SetStatus("采集中");
    }

    private async Task TogglePollingAsync()
    {
        if (_pollTask is null)
            StartPolling();
        else
            await StopPollingAsync();
    }

    private async Task StopPollingAsync()
    {
        if (_pollCts is null || _pollTask is null) return;
        _pollCts.Cancel();
        try { await _pollTask; } catch { }
        _pollCts.Dispose();
        _pollCts = null;
        _pollTask = null;
        _btnStop.Enabled = false;
        _btnStart.Enabled = _transport is not null;
        _btnStart.Text = "开始采集";
        if (_transport is not null) SetStatus("已连接");
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds((double)_numInterval.Value));

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await _ioLock.WaitAsync(ct);
                try
                {
                    if (_transport is null) continue;

                    string cmd = Normalize(_txtQueryCmd.Text);
                    byte[] rawBytes = await _transport.QueryRawAsync(cmd, ct);
                    string rawPreview = ScpiValueParser.FormatRawPreview(rawBytes);
                    var values = ScpiValueParser.ParseValues(rawBytes);
                    if (_showOddHarmonicsFromThird && IsListQueryCommand(cmd))
                        values = FilterOddHarmonicsFromThird(values);
                    var headers = ResolveHeadersForValues(cmd, values.Count);
                    (headers, values) = FilterUnavailableColumns(headers, values);

                    _uiQueue.Enqueue(MeasurementFrame.FromValues(rawPreview, headers, values));
                }
                finally
                {
                    if (_ioLock.CurrentCount == 0)
                        _ioLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _uiQueue.Enqueue(MeasurementFrame.FromError(ex.Message));
                if (_chkAutoReconnect.Checked)
                {
                    bool ok = await TryReconnectAsync(ct);
                    if (!ok)
                        await Task.Delay(_settings.ReconnectDelayMs, ct);
                }
            }
        }
    }

    private List<string> ResolveHeadersForValues(string queryCommand, int valueCount)
    {
        if (_currentHeaders.Count == valueCount && valueCount > 0)
            return _currentHeaders;

        if (IsListQueryCommand(queryCommand))
        {
            var listHeaders = BuildListHeadersFromCurrentUi(valueCount);
            if (listHeaders.Count == valueCount)
            {
                _currentHeaders = listHeaders;
                return _currentHeaders;
            }
        }

        _currentHeaders = Enumerable.Range(1, Math.Max(valueCount, 1)).Select(i => $"Value{i}").ToList();
        return _currentHeaders;
    }

    private static (List<string> Headers, List<double?> Values) FilterUnavailableColumns(IReadOnlyList<string> headers, IReadOnlyList<double?> values)
    {
        var filteredHeaders = new List<string>();
        var filteredValues = new List<double?>();
        int count = Math.Min(headers.Count, values.Count);
        for (int i = 0; i < count; i++)
        {
            if (values[i] is null && IsOptionalColumnThatMayBeBlank(headers[i]))
                continue;
            filteredHeaders.Add(headers[i]);
            filteredValues.Add(values[i]);
        }

        for (int i = count; i < values.Count; i++)
        {
            filteredHeaders.Add($"Value{i + 1}");
            filteredValues.Add(values[i]);
        }

        return (filteredHeaders, filteredValues);
    }

    private static bool IsOptionalColumnThatMayBeBlank(string header)
    {
        string text = header.ToUpperInvariant();
        return text.Contains("相位", StringComparison.OrdinalIgnoreCase)
               || text.Contains("PHI", StringComparison.OrdinalIgnoreCase)
               || text.Contains("频率", StringComparison.OrdinalIgnoreCase)
               || text.Contains("FU", StringComparison.OrdinalIgnoreCase)
               || text.Contains("FI", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> TryReconnectAsync(CancellationToken ct)
    {
        try
        {
            if (_transport is not null)
            {
                await _transport.DisposeAsync();
                _transport = null;
            }

            if (string.Equals(_settings.ConnectionType, "TCP/IP", StringComparison.OrdinalIgnoreCase))
                _transport = new TcpScpiTransport(_settings.Ip, _settings.Port);
            else if (string.Equals(_settings.ConnectionType, "RS-232", StringComparison.OrdinalIgnoreCase))
                _transport = new SerialScpiTransport(_settings.ComPort, _settings.BaudRate);
            else
            {
                _transport = new WinUsbScpiTransport();
            }

            await _transport.OpenAsync(ct);
            BeginInvoke(() => SetStatus("已重连"));
            return true;
        }
        catch
        {
            BeginInvoke(() => SetStatus("重连中..."));
            return false;
        }
    }

    private void FlushUiQueue()
    {
        while (_uiQueue.TryDequeue(out var frame))
        {
            if (frame.IsError)
            {
                _errorCount++;
                AppendRaw($"错误: {frame.ErrorMessage}\r\n");
                UpdateStatText();
                continue;
            }

            _frameCount++;
            _frameBuffer.Add(frame);
            if (_logger.IsRunning) _logger.Enqueue(frame);

            AppendRaw($"[{frame.Timestamp:HH:mm:ss.fff}] {frame.Raw}\r\n");
            EnsureColumns(frame.Headers);
            AddGridRow(frame);
            UpdateDashboard(frame);
            RefreshChart();
            UpdateStatText();
        }
    }

    private void EnsureColumns(IReadOnlyList<string> headers)
    {
        if (headers.Count == 0) return;
        int expected = headers.Count + 2;
        if (_grid.Columns.Count == expected) return;
        BuildGrid(headers);
        BuildSeries(headers);
        BuildDashboard(headers);
    }

    private void AddGridRow(MeasurementFrame frame)
    {
        var row = new List<object?> { frame.Timestamp.ToString("HH:mm:ss.fff") };
        for (int i = 0; i < frame.Headers.Count; i++)
        {
            row.Add(i < frame.Values.Count && frame.Values[i].HasValue ? frame.Values[i]!.Value.ToString("F6") : string.Empty);
        }
        row.Add(frame.ValueCount);

        _grid.Rows.Insert(0, row.ToArray());
        while (_grid.Rows.Count > _settings.GridMaxRows)
            _grid.Rows.RemoveAt(_grid.Rows.Count - 1);
    }

    private void RefreshChart()
    {
        if (_chartPaused) return;
        var frames = _frameBuffer.Snapshot();
        string selected = _cmbChartChannel.Text;
        foreach (var series in _chart.Series)
        {
            series.Points.Clear();
            series.Enabled = selected == "全部通道" || series.Name == selected;
        }

        int window = (int)_numChartWindow.Value;
        if (frames.Length > window) frames = frames[^window..];
        int end = Math.Max(window, _frameCount);
        int start = Math.Max(0, end - window);
        int firstSample = Math.Max(0, _frameCount - frames.Length);

        for (int sampleIndex = 0; sampleIndex < frames.Length; sampleIndex++)
        {
            var frame = frames[sampleIndex];
            int max = Math.Min(_chart.Series.Count, frame.Values.Count);
            for (int i = 0; i < max; i++)
            {
                if (_chart.Series[i].Enabled && frame.Values[i].HasValue)
                    _chart.Series[i].Points.AddXY(firstSample + sampleIndex, frame.Values[i]!.Value);
            }
        }

        var area = _chart.ChartAreas["main"];
        area.AxisX.Minimum = start;
        area.AxisX.Maximum = end;
        area.AxisX.Interval = Math.Max(1, window / 6d);
        area.AxisX.IsMarginVisible = false;
        if (_chkLockYAxis.Checked && _numYMax.Value > _numYMin.Value)
        {
            area.AxisY.Minimum = (double)_numYMin.Value;
            area.AxisY.Maximum = (double)_numYMax.Value;
        }
        else
        {
            area.AxisY.Minimum = double.NaN;
            area.AxisY.Maximum = double.NaN;
        }
    }

    private async Task ToggleLoggingAsync()
    {
        if (_logger.IsRunning)
        {
            await StopLoggingAsync(promptOpen: true);
            return;
        }

        StartLogging();
    }

    private void StartLogging()
    {
        string recordDir = CreateRecordDirectory("PA310");
        string csvPath = Path.Combine(recordDir, "data.csv");
        _logger.Start(csvPath);
        _activeRecordDirectory = recordDir;
        _btnStartLog.Text = "停止记录";
        _btnStartLog.Enabled = true;
        _btnStopLog.Enabled = false;
        AppendRaw($"开始记录: {csvPath}\r\n");
    }

    private async Task StopLoggingAsync(bool promptOpen = false)
    {
        if (!_logger.IsRunning) return;

        string recordDir = _activeRecordDirectory;
        await _logger.StopAsync();
        _btnStartLog.Text = "开始记录";
        _btnStartLog.Enabled = _transport is not null;
        _btnStopLog.Enabled = false;
        AppendRaw("记录已停止\r\n");

        string? reportPath = null;
        if (!string.IsNullOrWhiteSpace(recordDir))
            reportPath = ExportReportToDirectory(recordDir, overwriteCsv: false, showNoDataMessage: false);

        if (promptOpen && !string.IsNullOrWhiteSpace(recordDir) && Directory.Exists(recordDir))
        {
            string message = reportPath is null
                ? $"记录数据已保存到：\n{recordDir}\n\n当前还没有足够数据生成图表报告。是否打开保存目录？"
                : $"记录和图表报告已保存到：\n{recordDir}\n\n是否现在打开保存目录？";
            if (MessageBox.Show(message, "记录完成", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                OpenDirectory(recordDir);
        }

        _activeRecordDirectory = string.Empty;
    }

    private void ExportReport()
    {
        string reportDir = CreateRecordDirectory("PA310_Report");
        string? reportPath = ExportReportToDirectory(reportDir, overwriteCsv: true, showNoDataMessage: true);
        if (reportPath is null) return;

        AppendRaw($"已导出图表报告: {reportPath}\r\n");
        if (MessageBox.Show($"导出完成：\n{reportDir}\n\n是否打开保存目录？", "导出图表报告",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            OpenDirectory(reportDir);
    }

    private string? ExportReportToDirectory(string dir, bool overwriteCsv, bool showNoDataMessage)
    {
        var frames = _frameBuffer.Snapshot().Where(x => !x.IsError && x.Values.Count > 0).ToArray();
        if (frames.Length == 0)
        {
            if (showNoDataMessage)
                MessageBox.Show("当前还没有可导出的采集数据。请先开始采集一段时间。", "导出图表报告",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        Directory.CreateDirectory(dir);

        string reportPath = Path.Combine(dir, "report.html");
        string csvPath = Path.Combine(dir, "data.csv");
        if (overwriteCsv || !File.Exists(csvPath))
            ExportFramesCsv(frames, csvPath);
        string rawPath = Path.Combine(dir, "raw.txt");
        File.WriteAllLines(rawPath, frames.Select(f => $"[{f.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {f.Raw}"), new UTF8Encoding(true));

        var headers = frames.First().Headers.Count > 0
            ? frames.First().Headers.ToArray()
            : Enumerable.Range(1, frames.First().Values.Count).Select(i => $"Value{i}").ToArray();

        var chartFiles = new List<(string Title, string File, string Note)>();
        AddReportChart(frames, headers, dir, chartFiles, "输入功率 / 有功功率 P", "power.png", h => HeaderMatchesDisplayCode(h, "P"));
        AddReportChart(frames, headers, dir, chartFiles, "功率因数 PF", "pf.png", h => HeaderMatchesDisplayCode(h, "LAMBDA"));
        AddReportChart(frames, headers, dir, chartFiles, "THD / 总谐波畸变率", "thd.png", h => h.Contains("THD", StringComparison.OrdinalIgnoreCase) || h.Contains("谐波畸变", StringComparison.OrdinalIgnoreCase));

        var missing = new List<string>();
        if (!chartFiles.Any(x => x.File == "power.png")) missing.Add("输入功率/有功功率 P");
        if (!chartFiles.Any(x => x.File == "pf.png")) missing.Add("功率因数 PF");
        if (!chartFiles.Any(x => x.File == "thd.png")) missing.Add("THD/总谐波畸变率");

        string html = BuildReportHtml(frames.Length, string.Empty, chartFiles, missing);
        File.WriteAllText(reportPath, html, new UTF8Encoding(true));
        return reportPath;
    }

    private static string CreateRecordDirectory(string prefix)
    {
        string root = Path.Combine(AppContext.BaseDirectory, "数据保存");
        string dir = Path.Combine(root, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void OpenDirectory(string dir)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开目录：{ex.Message}", "打开保存目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void ExportFramesCsv(IReadOnlyList<MeasurementFrame> frames, string path)
    {
        var headers = frames.First().Headers.Count > 0
            ? frames.First().Headers.ToArray()
            : Enumerable.Range(1, frames.First().Values.Count).Select(i => $"Value{i}").ToArray();

        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        sw.WriteLine(string.Join(',', new[] { "时间" }.Concat(headers.Select(EscapeCsv))));
        foreach (var frame in frames)
        {
            var row = new List<string>
            {
                EscapeCsv(frame.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            };
            for (int i = 0; i < headers.Length; i++)
                row.Add(i < frame.Values.Count && frame.Values[i].HasValue
                    ? frame.Values[i]!.Value.ToString("G", CultureInfo.InvariantCulture)
                    : string.Empty);
            sw.WriteLine(string.Join(',', row));
        }
    }

    private static void AddReportChart(
        IReadOnlyList<MeasurementFrame> frames,
        IReadOnlyList<string> headers,
        string dir,
        List<(string Title, string File, string Note)> chartFiles,
        string title,
        string fileName,
        Func<string, bool> matcher)
    {
        var indexes = headers.Select((h, i) => (Header: h, Index: i)).Where(x => matcher(x.Header)).Take(4).ToList();
        if (indexes.Count == 0) return;

        using var chart = new Chart { Width = 1200, Height = 520, BackColor = Color.White };
        var area = new ChartArea("main");
        area.AxisX.Title = "采样序列";
        area.AxisY.Title = title;
        area.AxisX.MajorGrid.LineColor = Color.FromArgb(235, 241, 248);
        area.AxisY.MajorGrid.LineColor = Color.FromArgb(226, 232, 240);
        chart.ChartAreas.Add(area);
        chart.Legends.Add(new Legend { Docking = Docking.Top, BackColor = Color.Transparent });
        chart.Titles.Add(new Title(title, Docking.Top, new Font("Microsoft YaHei UI", 13, FontStyle.Bold), Color.FromArgb(15, 23, 42)));

        foreach (var item in indexes)
        {
            var series = new Series(item.Header) { ChartType = SeriesChartType.FastLine, BorderWidth = 2, ChartArea = "main" };
            for (int i = 0; i < frames.Count; i++)
                if (item.Index < frames[i].Values.Count && frames[i].Values[item.Index].HasValue)
                    series.Points.AddXY(i + 1, frames[i].Values[item.Index]!.Value);
            chart.Series.Add(series);
        }

        string path = Path.Combine(dir, fileName);
        chart.SaveImage(path, ChartImageFormat.Png);
        chartFiles.Add((title, fileName, string.Join("、", indexes.Select(x => x.Header))));
    }

    private static string BuildReportHtml(int frameCount, string assetFolder, IReadOnlyList<(string Title, string File, string Note)> charts, IReadOnlyList<string> missing)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>PA310 测量报告</title>");
        sb.AppendLine("<style>body{font-family:'Microsoft YaHei UI',Arial,sans-serif;margin:28px;color:#0f172a}img{max-width:100%;border:1px solid #e2e8f0;margin:10px 0 28px}.note{color:#64748b}.warn{color:#b45309}</style></head><body>");
        sb.AppendLine("<h1>PA310 测量报告</h1>");
        sb.AppendLine($"<p class=\"note\">导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}；采样帧数：{frameCount}。</p>");
        sb.AppendLine($"<p>整理后的表格数据：<a href=\"{EscapeHtml(AssetHref(assetFolder, "data.csv"))}\">data.csv</a>；原始通信返回：<a href=\"{EscapeHtml(AssetHref(assetFolder, "raw.txt"))}\">raw.txt</a></p>");
        foreach (var chart in charts)
        {
            sb.AppendLine($"<h2>{EscapeHtml(chart.Title)}</h2>");
            sb.AppendLine($"<p class=\"note\">包含列：{EscapeHtml(chart.Note)}</p>");
            sb.AppendLine($"<img src=\"{EscapeHtml(AssetHref(assetFolder, chart.File))}\" alt=\"{EscapeHtml(chart.Title)}\">");
        }
        if (missing.Count > 0)
            sb.AppendLine($"<p class=\"warn\">以下图表未生成，因为当前采集数据中没有对应列：{EscapeHtml(string.Join("、", missing))}。</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string AssetHref(string assetFolder, string fileName) =>
        string.IsNullOrWhiteSpace(assetFolder) ? fileName : $"{assetFolder}/{fileName}";

    private static string EscapeCsv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string EscapeHtml(string value) => System.Net.WebUtility.HtmlEncode(value);

    private void ApplySelectedPreset()
    {
        if (_cmbPreset.SelectedItem is not CommandPreset preset) return;
        _txtQueryCmd.Text = preset.QueryCommand;
        _txtHeaderCmd.Text = preset.HeaderCommand;
        AppendRaw($"已应用测量内容: {preset.Name} | {preset.Description}\r\n");
    }

    private void SaveCurrentPreset()
    {
        string name = Microsoft.VisualBasic.Interaction.InputBox("请输入参数组名称", "保存参数组", $"参数组{_settings.Presets.Count + 1}");
        if (string.IsNullOrWhiteSpace(name)) return;

        var preset = new CommandPreset
        {
            Name = name.Trim(),
            HeaderCommand = Normalize(_txtHeaderCmd.Text),
            QueryCommand = Normalize(_txtQueryCmd.Text),
            Description = "用户保存"
        };

        _settings.Presets.Add(preset);
        _cmbPreset.Items.Add(preset);
        _cmbPreset.SelectedItem = preset;
        SaveUiSettings();
        AppendRaw($"参数组已保存: {preset.Name}\r\n");
    }
    private async Task<List<string>> QueryListHeadersFromDeviceAsync()
    {
        if (_transport is null) return new List<string>();

        int itemCount = await QueryIntSettingAsync(":NUMeric:LIST:NUMber?", (int)_numListCount.Value);
        int order = await QueryIntSettingAsync(":NUMeric:LIST:ORDer?", (int)_numListOrder.Value);
        string select = await QueryTextSettingAsync(":NUMeric:LIST:SELect?", _cmbListSelect.Text);

        var descriptors = new List<HarmonicListDescriptor>(itemCount);
        for (int i = 1; i <= itemCount; i++)
        {
            string response = await _transport.QueryAsync($":NUMeric:LIST:ITEM{i}?");
            AppendRaw($"> :NUMeric:LIST:ITEM{i}?\r\n{response}\r\n");
            descriptors.Add(ParseListDescriptor(response, i));
        }

        return HarmonicHeaderBuilder.Build(descriptors, order, select);
    }

    private async Task<int> QueryIntSettingAsync(string command, int fallback)
    {
        if (_transport is null) return fallback;
        try
        {
            string response = await _transport.QueryAsync(command);
            AppendRaw($"> {command}\r\n{response}\r\n");
            return ScpiValueParser.ParseIntValue(response) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private async Task<string> QueryTextSettingAsync(string command, string fallback)
    {
        if (_transport is null) return fallback;
        try
        {
            string response = await _transport.QueryAsync(command);
            AppendRaw($"> {command}\r\n{response}\r\n");
            string parsed = ScpiValueParser.ParseKeywordValue(response);
            return string.IsNullOrWhiteSpace(parsed) ? fallback : parsed;
        }
        catch
        {
            return fallback;
        }
    }

    private List<string> BuildListHeadersFromCurrentUi(int valueCount)
    {
        if (_showOddHarmonicsFromThird)
            return BuildMultiOddHarmonicHeaders();

        var descriptors = BuildPresetDescriptors((int)_numListPreset.Value, (int)_numListCount.Value);
        var headers = HarmonicHeaderBuilder.Build(descriptors, (int)_numListOrder.Value, _cmbListSelect.Text);
        if (valueCount > 0 && headers.Count > valueCount)
            headers = headers.Take(valueCount).ToList();
        return headers;
    }

    private List<string> BuildMultiOddHarmonicHeaders()
    {
        var descriptors = BuildDescriptorSequence(("I", 1));
        var headers = HarmonicHeaderBuilder.Build(descriptors, _multiHarmonicMaxOrder, "ODD3");
        return headers.Select(FormatHeaderForDisplay).ToList();
    }

    private List<double?> FilterOddHarmonicsFromThird(List<double?> values)
    {
        int descriptorCount = Math.Max(1, (int)_numListCount.Value);
        if (values.Count == 0 || values.Count % descriptorCount != 0) return values;

        int perDescriptor = values.Count / descriptorCount;
        int desiredPerDescriptor = Enumerable.Range(3, Math.Max(0, _multiHarmonicMaxOrder - 2)).Count(x => x % 2 == 1);
        if (desiredPerDescriptor <= 0 || perDescriptor == desiredPerDescriptor) return values;

        int oddIncludingFundamental = Enumerable.Range(1, _multiHarmonicMaxOrder).Count(x => x % 2 == 1);
        int skip = perDescriptor switch
        {
            var n when n == oddIncludingFundamental + 2 => 3, // TOT, DC, H1
            var n when n == oddIncludingFundamental => 1,     // H1
            _ => Math.Max(0, perDescriptor - desiredPerDescriptor)
        };

        var filtered = new List<double?>(descriptorCount * desiredPerDescriptor);
        for (int block = 0; block < descriptorCount; block++)
        {
            int start = block * perDescriptor;
            for (int i = skip; i < perDescriptor && filtered.Count < (block + 1) * desiredPerDescriptor; i++)
                filtered.Add(values[start + i]);
        }

        return filtered.Count > 0 ? filtered : values;
    }

    private static int ParseHarmonicOrder(string text, int fallback)
    {
        string digits = new(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
    }

    private static List<HarmonicListDescriptor> BuildPresetDescriptors(int preset, int count)
    {
        var all = preset switch
        {
            1 => BuildDescriptorSequence(("U", 1), ("I", 1), ("P", 1), ("U", 2), ("I", 2), ("P", 2), ("U", 3), ("I", 3), ("P", 3)),
            2 => BuildDescriptorSequence(("U", 1), ("I", 1), ("P", 1), ("PHIU", 1), ("PHII", 1), ("U", 2), ("I", 2), ("P", 2), ("PHIU", 2), ("PHII", 2), ("U", 3), ("I", 3), ("P", 3), ("PHIU", 3), ("PHII", 3)),
            3 => BuildDescriptorSequence(("U", 1), ("I", 1), ("P", 1), ("UHDF", 1), ("IHDF", 1), ("PHDF", 1), ("U", 2), ("I", 2), ("P", 2), ("UHDF", 2), ("IHDF", 2), ("PHDF", 2), ("U", 3), ("I", 3), ("P", 3), ("UHDF", 3), ("IHDF", 3), ("PHDF", 3)),
            4 => BuildDescriptorSequence(("U", 1), ("I", 1), ("P", 1), ("PHIU", 1), ("PHII", 1), ("UHDF", 1), ("IHDF", 1), ("PHDF", 1), ("U", 2), ("I", 2), ("P", 2), ("PHIU", 2), ("PHII", 2), ("UHDF", 2), ("IHDF", 2), ("PHDF", 2), ("U", 3), ("I", 3), ("P", 3), ("PHIU", 3), ("PHII", 3), ("UHDF", 3), ("IHDF", 3), ("PHDF", 3)),
            _ => BuildDescriptorSequence(("U", 1))
        };

        return all.Take(Math.Max(count, 1)).ToList();
    }

    private static List<HarmonicListDescriptor> BuildDescriptorSequence(params (string Function, int Element)[] items)
    {
        return items.Select(x => new HarmonicListDescriptor { Function = x.Function, Element = x.Element }).ToList();
    }

    private static HarmonicListDescriptor ParseListDescriptor(string response, int index)
    {
        string? value = ScpiValueParser.ParseItemValue(response);
        if (string.IsNullOrWhiteSpace(value))
            return new HarmonicListDescriptor { Function = $"ITEM{index:00}", Element = 1 };

        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts[0].Equals("NONE", StringComparison.OrdinalIgnoreCase))
            return new HarmonicListDescriptor { Function = $"ITEM{index:00}", Element = 1 };

        int element = 1;
        if (parts.Length > 1 && int.TryParse(parts[1], out int parsed))
            element = parsed;

        return new HarmonicListDescriptor { Function = parts[0], Element = element };
    }

    private void AppendRaw(string text)
    {
        if (_txtRaw.TextLength > _settings.RawTextMaxChars)
            _txtRaw.Clear();
        _txtRaw.AppendText(text);
    }

    private void SetStatus(string text) => _lblStatus.Text = text;

    private void UpdateStatText()
    {
        _lblStat.Text = $"{_frameCount} 帧 | {_errorCount} 错误" + (_logger.IsRunning ? $" | 记录中: {Path.GetFileName(_logger.CurrentPath)}" : string.Empty);
    }

    private void SaveUiSettings()
    {
        _settings.ConnectionType = _cmbConnType.Text;
        _settings.Ip = _txtIp.Text.Trim();
        _settings.Port = (int)_numPort.Value;
        _settings.ComPort = _cmbCom.Text.Trim();
        _settings.BaudRate = int.TryParse(_cmbBaud.Text, out int baud) ? baud : 115200;
        _settings.PollIntervalMs = (int)_numInterval.Value;
        _settings.AutoReconnect = _chkAutoReconnect.Checked;
        _settings.DefaultHeaderCommand = Normalize(_txtHeaderCmd.Text);
        _settings.DefaultQueryCommand = Normalize(_txtQueryCmd.Text);
        _settings.NumericFormat = _cmbNumericFormat.Text;
        _settings.Rate = RateScpiValue(_cmbRate.Text);
        _settings.NumericHoldEnabled = _chkHold.Checked;
        _settings.NormalPresetMode = (int)_numNormalPreset.Value;
        _settings.NormalItemCount = (int)_numNormalCount.Value;
        _settings.ListPresetMode = (int)_numListPreset.Value;
        _settings.ListItemCount = (int)_numListCount.Value;
        _settings.ListOrder = (int)_numListOrder.Value;
        _settings.ListSelect = _cmbListSelect.Text;
        _settings.SetupCommands = _txtSetupCommands.Text;
        SettingsStore.Save(_settings);
    }
    private async Task SafeShutdownAsync()
    {
        try
        {
            await StopPollingAsync();
            await StopLoggingAsync();
            if (_transport is not null)
            {
                await _transport.DisposeAsync();
                _transport = null;
            }
        }
        finally
        {
            _uiTimer.Stop();
        }
    }

    private static bool IsListQueryCommand(string command)
    {
        return Normalize(command).StartsWith(":NUMERIC:LIST:VALUE?", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ParseSetupCommands(string raw)
    {
        return raw.Replace("\r", string.Empty)
                  .Split(['\n', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                  .Select(Normalize)
                  .Where(x => !string.IsNullOrWhiteSpace(x))
                  .ToList();
    }

    private static string Normalize(string cmd) => cmd.Trim();

    private static string RateScpiValue(string label) => label.Trim().ToUpperInvariant() switch
    {
        "100 MS" or "100MS" or "0.1S" => "0.1S",
        "250 MS" or "250MS" or "0.25S" => "0.25S",
        "500 MS" or "500MS" or "0.5S" => "0.5S",
        "1 S" or "1S" => "1S",
        "2 S" or "2S" => "2S",
        "5 S" or "5S" => "5S",
        "10 S" or "10S" => "10S",
        "20 S" or "20S" => "20S",
        _ => "0.25S"
    };

    private static string RateLabelFromSetting(string value)
    {
        string normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "100MS" or "0.1S" or "0.10S" => "100 ms",
            "250MS" or "0.25S" => "250 ms",
            "500MS" or "0.5S" or "0.50S" => "500 ms",
            "1S" => "1 s",
            "2S" => "2 s",
            "5S" => "5 s",
            "10S" => "10 s",
            "20S" => "20 s",
            _ => value
        };
    }

    private static string RateLabelFromSeconds(double seconds)
    {
        if (Math.Abs(seconds - 0.1) < 0.001) return "100 ms";
        if (Math.Abs(seconds - 0.25) < 0.001) return "250 ms";
        if (Math.Abs(seconds - 0.5) < 0.001) return "500 ms";
        return $"{seconds:0.###} s";
    }

    private static string FormatCurrentRangeToken(double amps)
    {
        if (amps < 1)
        {
            double milliAmps = amps * 1000;
            return $"{milliAmps:0.###}mA";
        }

        return $"{amps:0.###}A";
    }

    private static string DisplayCodeLabel(string code) => code.ToUpperInvariant() switch
    {
        "U" => "电压 U",
        "I" => "电流 I",
        "P" => "有功功率 P / W",
        "S" => "视在功率 S / VA",
        "Q" => "无功功率 Q / var",
        "LAMBDA" => "功率因数 PF / λ (LAMBDA)",
        "PHI" => "相位角 φ (PHI)",
        "FU" => "电压频率 fU / Hz",
        "FI" => "电流频率 fI / Hz",
        "UTHD" => "电压总谐波畸变率 UTHD",
        "ITHD" => "电流总谐波畸变率 ITHD",
        "WH" => "电能 Wh",
        "WHP" => "正向电能 Wh+ (WHP)",
        "WHM" => "反向电能 Wh- (WHM)",
        "AH" => "电量 Ah",
        "AHP" => "正向电量 Ah+ (AHP)",
        "AHM" => "反向电量 Ah- (AHM)",
        "TIME" => "积分/累计时间 TIME",
        "MATH" => "数学运算 MATH",
        "UPPEAK" => "电压正峰值：电压波形最高点（U+ Peak）",
        "UMPEAK" => "电压负峰值：电压波形最低点（U- Peak）",
        "IPPEAK" => "电流正峰值：电流波形最高点（I+ Peak）",
        "IMPEAK" => "电流负峰值：电流波形最低点（I- Peak）",
        "PPPEAK" => "功率正峰值：瞬时功率最高点（P+ Peak）",
        "PMPEAK" => "功率负峰值：瞬时功率最低点（P- Peak）",
        _ => code
    };

    private static string DisplayCodeFromItem(object? item)
    {
        string text = item?.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        foreach (string code in KnownDisplayCodes)
        {
            if (text.Equals(code, StringComparison.OrdinalIgnoreCase) ||
                text.Contains(code, StringComparison.OrdinalIgnoreCase))
                return code;
        }
        return text.Split(' ', '/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? text;
    }

    private static readonly string[] KnownDisplayCodes =
    [
        "LAMBDA", "UPPEAK", "UMPEAK", "IPPEAK", "IMPEAK", "PPPEAK", "PMPEAK",
        "UTHD", "ITHD", "TIME", "MATH", "WHP", "WHM", "AHP", "AHM",
        "PHI", "FU", "FI", "WH", "AH", "U", "I", "P", "S", "Q"
    ];

    private static IReadOnlyList<string> AllowedNormalDisplayFunctions(int displayIndex) => displayIndex switch
    {
        1 => ["U", "I", "P", "S", "Q", "TIME"],
        2 => ["U", "I", "P", "LAMBDA", "PHI"],
        3 => ["U", "I", "P", "UPPEAK", "UMPEAK", "IPPEAK", "IMPEAK", "PPPEAK", "PMPEAK", "WH", "WHP", "WHM", "AH", "AHP", "AHM", "MATH"],
        4 => ["U", "I", "P", "LAMBDA", "FU", "FI", "UTHD", "ITHD"],
        _ => ["U", "I", "P"]
    };

    private static bool DisplayFunctionNeedsElement(string function)
        => !function.Equals("TIME", StringComparison.OrdinalIgnoreCase)
           && !function.Equals("MATH", StringComparison.OrdinalIgnoreCase);

    private static string BuildDisplayItemCommand(int index, string function)
    {
        string suffix = DisplayFunctionNeedsElement(function) ? ",1" : string.Empty;
        return $":DISPlay:NORMal:ITEM{index} {function}{suffix}";
    }

    private static void SelectDisplayCode(ComboBox combo, string code)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (string.Equals(DisplayCodeFromItem(combo.Items[i]), code, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    private static string DisplaySlotName(int index) => index switch
    {
        1 => "A",
        2 => "B",
        3 => "C",
        4 => "D",
        _ => index.ToString(CultureInfo.InvariantCulture)
    };

    private static string FormatHeaderForDisplay(string header)
    {
        string normalized = header.Trim().Trim(':');
        string[] parts = normalized.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string function = parts.ElementAtOrDefault(0)?.ToUpperInvariant() ?? normalized.ToUpperInvariant();
        string element = parts.ElementAtOrDefault(1)?.Replace("E", string.Empty, StringComparison.OrdinalIgnoreCase) ?? string.Empty;
        string harmonic = parts.ElementAtOrDefault(2)?.ToUpperInvariant() ?? string.Empty;
        string suffix = string.IsNullOrWhiteSpace(element) ? string.Empty : element;

        if (harmonic.StartsWith("H", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(harmonic[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int harmonicOrder))
        {
            string baseName = function switch
            {
                "U" => "电压",
                "I" => "电流",
                "P" => "有功功率",
                "UHDF" => "电压谐波含有率",
                "IHDF" => "电流谐波含有率",
                "PHDF" => "功率谐波含有率",
                "PHIU" => "电压谐波相位",
                "PHII" => "电流谐波相位",
                _ => function
            };
            return $"{baseName} {harmonicOrder}次谐波";
        }

        return function switch
        {
            "U" => $"电压 U{suffix} (V)",
            "I" => $"电流 I{suffix} (A)",
            "P" => $"有功功率 P{suffix} (W)",
            "S" => $"视在功率 S{suffix} (VA)",
            "Q" => $"无功功率 Q{suffix} (var)",
            "LAMBDA" => $"功率因数 PF{suffix}",
            "PHI" => $"相位角 φ{suffix} (°)",
            "FU" => $"电压频率 fU{suffix} (Hz)",
            "FI" => $"电流频率 fI{suffix} (Hz)",
            "NONE" => "未配置",
            _ => header
        };
    }
}
