using System.Text;
using PrinterHub.App.Gdi;
using PrinterHub.Core;
using PrinterHub.Core.Config;
using PrinterHub.Core.Model;
using PrinterHub.Core.Util;
using PrinterHub.Languages;
using PrinterHub.Transports;
using PrinterHub.Windows;

namespace PrinterHub.App;

/// <summary>
/// Cửa sổ chính PrinterHub (WinForms, dựng giao diện bằng code như dự án cp932).
/// Tab: Lệnh raw · Nhãn · Máy in Windows · Tìm máy in mạng · Profiles.
/// </summary>
public sealed class MainForm : Form
{
    // ===== Core =====
    private readonly HubContext _hub = new();
    private readonly PrinterService _service;
    private readonly AppSettings _settings = AppSettings.Load();
    private CancellationTokenSource? _busyCts;

    // ===== Thanh trên =====
    private readonly ComboBox _cboRoute = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 420 };
    private readonly ComboBox _cboLang = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly ComboBox _cboEncoding = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly Label _lblPrinterState = new() { AutoSize = true, Text = "●  chưa kiểm tra", Margin = new Padding(12, 9, 3, 3) };

    // ===== Tab Raw =====
    private readonly TextBox _txtRaw = MakeEditor();
    private readonly TextBox _txtResponse = MakeEditor(readOnly: true);
    private readonly CheckBox _chkSbplTags = new() { Text = "Ký hiệu SATO <A>=ESC A", AutoSize = true, Margin = new Padding(6, 8, 3, 3) };
    private readonly CheckBox _chkStripNewLines = new() { Text = "Bỏ xuống dòng của editor", AutoSize = true, Checked = true, Margin = new Padding(6, 8, 3, 3) };
    private readonly NumericUpDown _numWait = new() { Minimum = 0, Maximum = 30000, Increment = 250, Value = 1000, Width = 70, Margin = new Padding(3, 6, 3, 3) };
    private readonly ComboBox _cboSamples = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };

    // ===== Tab Nhãn =====
    private readonly TextBox _txtLabelJson = MakeEditor();
    private readonly TextBox _txtCommands = MakeEditor(readOnly: true);
    private readonly PictureBox _picPreview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.BgDark };
    private readonly NumericUpDown _numCopies = new() { Minimum = 1, Maximum = 99999, Value = 1, Width = 70, Margin = new Padding(3, 6, 3, 3) };
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 700 };

    // ===== Tab Máy in Windows =====
    private readonly ListView _lvPrinters = MakeList(("Tên máy in", 260), ("Hãng", 70), ("Port", 160), ("Driver", 240), ("Trạng thái", 110), ("Job", 45));
    private readonly ListView _lvJobs = MakeList(("Job", 60), ("Tài liệu", 260), ("User", 100), ("Trạng thái", 140), ("Trang", 80));

    // ===== Tab Discover =====
    private readonly TextBox _txtSubnet = new() { Width = 160 };
    private readonly TextBox _txtPorts = new() { Width = 260, Text = string.Join(",", NetworkDiscovery.DefaultPorts) };
    private readonly ListView _lvFound = MakeList(("Địa chỉ", 140), ("Cổng mở", 200), ("Nhận diện", 520));

    // ===== Tab Profiles =====
    private readonly TextBox _txtProfiles = MakeEditor();
    private readonly ComboBox _cboProfile = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };

    // ===== Log + status =====
    private readonly RichTextBox _txtLog = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, BackColor = Theme.BgInput, ForeColor = Theme.Text,
        BorderStyle = BorderStyle.None, Font = Theme.MonoFont, WordWrap = false,
    };
    private readonly ToolStripStatusLabel _lblStatus = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.TextDim };
    private readonly CheckBox _chkHexLog = new() { Text = "Log hex", AutoSize = true, Margin = new Padding(6, 8, 3, 3) };

    public MainForm()
    {
        _service = new PrinterService(_hub);
        _hub.Log = new DelegateLog(AppendLog, showData: true);
        _hub.Transports.AddNetworkTransports().AddWindowsTransports();
        _hub.Transports.Register("passthrough", (r, l) => new PassthroughTransport(r, l), "Driver passthrough ${…}$ (GDI)");
        _hub.Languages.AddBuiltInLanguages();

        Text = "PrinterHub — SATO · Zebra · Godex · FUJIFILM · Fujitsu";
        Size = new Size(1280, 860);
        MinimumSize = new Size(1000, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.BgDark;
        ForeColor = Theme.Text;
        Font = Theme.UiFont;

        BuildLayout();
        Theme.Apply(this);
        _lblPrinterState.ForeColor = Theme.TextDim;
        LoadInitialState();
    }

    // =====================================================================
    //  LAYOUT
    // =====================================================================

    private void BuildLayout()
    {
        // ---- Thanh trên: route / ngôn ngữ / encoding ----
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 74, Padding = new Padding(10, 6, 10, 4), BackColor = Theme.BgPanel, WrapContents = true,
        };
        var title = new Label { Text = "🖨 PrinterHub", Font = Theme.TitleFont, ForeColor = Theme.Accent, AutoSize = true, Margin = new Padding(3, 3, 20, 3) };
        top.Controls.Add(title);
        top.Controls.Add(Theme.MakeLabel("Route:"));
        top.Controls.Add(_cboRoute);
        top.Controls.Add(Theme.MakeLabel("Ngôn ngữ:"));
        top.Controls.Add(_cboLang);
        top.Controls.Add(Theme.MakeLabel("Encoding:"));
        top.Controls.Add(_cboEncoding);
        top.Controls.Add(Theme.MakeButton("Trạng thái", async (_, _) => await CheckStatusAsync()));
        top.Controls.Add(Theme.MakeButton("Trợ giúp route", (_, _) => ShowRouteHelp()));
        top.Controls.Add(_lblPrinterState);

        // ---- Tabs ----
        var tabs = new DarkTabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildRawTab());
        tabs.TabPages.Add(BuildLabelTab());
        tabs.TabPages.Add(BuildWindowsTab());
        tabs.TabPages.Add(BuildDiscoverTab());
        tabs.TabPages.Add(BuildProfilesTab());

        // ---- Log ----
        var logPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 4), BackColor = Theme.BgDark };
        var logBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, BackColor = Theme.BgDark };
        logBar.Controls.Add(new Label { Text = "Nhật ký", ForeColor = Theme.AccentCyan, AutoSize = true, Margin = new Padding(3, 9, 12, 3) });
        logBar.Controls.Add(_chkHexLog);
        logBar.Controls.Add(Theme.MakeButton("Xoá log", (_, _) => _txtLog.Clear()));
        logBar.Controls.Add(Theme.MakeButton("Huỷ thao tác", (_, _) => _busyCts?.Cancel()));
        logPanel.Controls.Add(_txtLog);
        logPanel.Controls.Add(logBar);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6, BackColor = Theme.Border,
        };
        split.Panel1.Controls.Add(tabs);
        split.Panel2.Controls.Add(logPanel);

        var status = new StatusStrip { BackColor = Theme.BgPanel, SizingGrip = false };
        status.Items.Add(_lblStatus);

        Controls.Add(split);
        Controls.Add(top);
        Controls.Add(status);
        Shown += (_, _) => split.SplitterDistance = (int)(split.Height * 0.68);
    }

    private TabPage BuildRawTab()
    {
        var page = new TabPage("Lệnh raw");
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4) };
        bar.Controls.Add(Theme.MakeLabel("Mẫu:"));
        bar.Controls.Add(_cboSamples);
        bar.Controls.Add(Theme.MakeButton("Mở file...", (_, _) => OpenRawFile()));
        bar.Controls.Add(_chkSbplTags);
        bar.Controls.Add(_chkStripNewLines);
        bar.Controls.Add(Theme.MakeLabel("Chờ phản hồi (ms):"));
        bar.Controls.Add(_numWait);
        bar.Controls.Add(Theme.MakeButton("▶ Gửi", async (_, _) => await SendRawAsync()));
        bar.Controls.Add(Theme.MakeButton("Xem hex", (_, _) => ShowRawHex()));

        _cboSamples.Items.AddRange(RawSamples.All.Select(s => (object)$"{s.Brand} · {s.Title}").ToArray());
        _cboSamples.SelectedIndexChanged += (_, _) =>
        {
            if (_cboSamples.SelectedIndex < 0) return;
            var s = RawSamples.All[_cboSamples.SelectedIndex];
            _txtRaw.Text = s.Text.Replace("\n", Environment.NewLine);
            _cboLang.SelectedItem = s.Language;
            _cboEncoding.SelectedItem = s.Encoding;
            _chkSbplTags.Checked = s.SbplTags;
            _chkStripNewLines.Checked = s.Language is "SBPL" or "ZPL";
            if (s.WaitReply && _numWait.Value == 0) _numWait.Value = 1000;
        };

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = 6, BackColor = Theme.Border };
        split.Panel1.Controls.Add(WithCaption(_txtRaw, "Lệnh gửi (dùng <STX> <ESC> <ETX> <CR> <LF> <0x1B>... cho byte điều khiển)"));
        split.Panel2.Controls.Add(WithCaption(_txtResponse, "Phản hồi từ máy in"));
        page.Controls.Add(split);
        page.Controls.Add(bar);
        OnFirstLayout(split, () => split.SplitterDistance = split.Width * 3 / 5);
        return page;
    }

    private TabPage BuildLabelTab()
    {
        var page = new TabPage("Nhãn");
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4) };
        bar.Controls.Add(Theme.MakeButton("Nhãn mẫu", (_, _) => LoadDemoLabel(false)));
        bar.Controls.Add(Theme.MakeButton("Template {biến}", (_, _) => LoadDemoLabel(true)));
        bar.Controls.Add(Theme.MakeButton("Mở JSON...", (_, _) => OpenLabelJson()));
        bar.Controls.Add(Theme.MakeButton("Lưu JSON...", (_, _) => SaveLabelJson()));
        bar.Controls.Add(Theme.MakeButton("Thêm logo...", (_, _) => AddLogo()));
        bar.Controls.Add(Theme.MakeLabel("Số bản:"));
        bar.Controls.Add(_numCopies);
        bar.Controls.Add(Theme.MakeButton("▶ In qua route", async (_, _) => await PrintLabelAsync()));
        bar.Controls.Add(Theme.MakeButton("In qua driver (GDI)", (_, _) => PrintLabelGdi()));
        bar.Controls.Add(Theme.MakeButton("In hàng loạt CSV...", async (_, _) => await PrintCsvBatchAsync()));
        bar.Controls.Add(Theme.MakeButton("Lưu lệnh ra file...", (_, _) => SaveRenderedCommands()));

        var right = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6, BackColor = Theme.Border };
        right.Panel1.Controls.Add(WithCaption(_picPreview, "Xem trước (GDI — QR/barcode khác Code128 là khung giữ chỗ)"));
        right.Panel2.Controls.Add(WithCaption(_txtCommands, "Lệnh sinh ra theo ngôn ngữ đang chọn"));

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = 6, BackColor = Theme.Border };
        split.Panel1.Controls.Add(WithCaption(_txtLabelJson, "Nhãn (JSON, toạ độ tính bằng dot)"));
        split.Panel2.Controls.Add(right);

        page.Controls.Add(split);
        page.Controls.Add(bar);
        OnFirstLayout(split, () => split.SplitterDistance = split.Width * 2 / 5);
        OnFirstLayout(right, () => right.SplitterDistance = right.Height / 2);

        _txtLabelJson.TextChanged += (_, _) => { _previewTimer.Stop(); _previewTimer.Start(); };
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); RefreshLabelPreview(); };
        _cboLang.SelectedIndexChanged += (_, _) => RefreshLabelPreview();
        _cboEncoding.SelectedIndexChanged += (_, _) => RefreshLabelPreview();
        return page;
    }

    private TabPage BuildWindowsTab()
    {
        var page = new TabPage("Máy in Windows");
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4) };
        bar.Controls.Add(Theme.MakeButton("Làm mới", (_, _) => RefreshWindowsPrinters()));
        bar.Controls.Add(Theme.MakeButton("Dùng spooler:// (RAW)", (_, _) => UseSelectedPrinter("spooler")));
        bar.Controls.Add(Theme.MakeButton("Dùng passthrough://", (_, _) => UseSelectedPrinter("passthrough")));
        bar.Controls.Add(Theme.MakeButton("Xem hàng đợi", (_, _) => RefreshJobs()));

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6, BackColor = Theme.Border };
        split.Panel1.Controls.Add(WithCaption(_lvPrinters, "Máy in đã cài trong Windows (driver SATO / ZDesigner / Godex / FUJIFILM...)"));
        split.Panel2.Controls.Add(WithCaption(_lvJobs, "Job trong hàng đợi"));
        _lvPrinters.DoubleClick += (_, _) => UseSelectedPrinter("spooler");
        _lvPrinters.SelectedIndexChanged += (_, _) => RefreshJobs();

        page.Controls.Add(split);
        page.Controls.Add(bar);
        return page;
    }

    private TabPage BuildDiscoverTab()
    {
        var page = new TabPage("Tìm máy in mạng");
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4) };
        bar.Controls.Add(Theme.MakeLabel("Subnet (/24):"));
        bar.Controls.Add(_txtSubnet);
        bar.Controls.Add(Theme.MakeLabel("Cổng:"));
        bar.Controls.Add(_txtPorts);
        bar.Controls.Add(Theme.MakeButton("▶ Quét", async (_, _) => await DiscoverAsync()));
        bar.Controls.Add(Theme.MakeButton("Dùng route đã chọn", (_, _) => UseDiscovered()));
        _lvFound.DoubleClick += (_, _) => UseDiscovered();
        page.Controls.Add(WithCaption(_lvFound, "Kết quả (double-click để dùng làm route)"));
        page.Controls.Add(bar);
        return page;
    }

    private TabPage BuildProfilesTab()
    {
        var page = new TabPage("Profiles");
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4) };
        bar.Controls.Add(Theme.MakeButton("Tải lại", (_, _) => LoadProfilesText()));
        bar.Controls.Add(Theme.MakeButton("Lưu", (_, _) => SaveProfilesText()));
        bar.Controls.Add(Theme.MakeLabel("Profile:"));
        bar.Controls.Add(_cboProfile);
        bar.Controls.Add(Theme.MakeButton("Áp dụng (route/ngôn ngữ)", (_, _) => ApplySelectedProfile()));
        bar.Controls.Add(Theme.MakeButton("▶ In nhãn hiện tại (failover)", async (_, _) => await PrintWithProfileAsync()));
        page.Controls.Add(WithCaption(_txtProfiles, $"profiles.json — {AppSettings.ProfilesPath}"));
        page.Controls.Add(bar);
        return page;
    }

    // =====================================================================
    //  KHỞI TẠO
    // =====================================================================

    private void LoadInitialState()
    {
        _cboLang.Items.AddRange(_hub.Languages.Names.OrderBy(n => n).Cast<object>().ToArray());
        _cboEncoding.Items.AddRange(new object[] { "utf-8", "shift_jis", "windows-1258", "ascii", "utf-16" });
        _cboLang.SelectedItem = _settings.Language ?? "SBPL";
        _cboEncoding.SelectedItem = _settings.Encoding ?? "shift_jis";

        var presets = new List<string>(_settings.RouteHistory)
        {
            "tcp://127.0.0.1:9100",
            "sato4://127.0.0.1?data=1024&status=1025",
            "lpr://127.0.0.1:5515/lp",
            "serial://COM1?baud=9600&handshake=rtscts",
            @"file://C:\Temp\printerhub\job.prn",
        };
        foreach (var r in presets.Distinct()) _cboRoute.Items.Add(r);
        _cboRoute.Text = _settings.LastRoute ?? "tcp://127.0.0.1:9100";

        _txtSubnet.Text = NetworkDiscovery.LocalSubnets().FirstOrDefault() ?? "192.168.1";
        _cboSamples.SelectedIndex = 0;
        _chkHexLog.CheckedChanged += (_, _) => _hub.Log = new DelegateLog(AppendLog, _chkHexLog.Checked);
        _chkHexLog.Checked = true;
        LoadDemoLabel(false);
        LoadProfilesText();
        RefreshWindowsPrinters();
        SetStatus("Sẵn sàng. Chạy tools/PrinterHub.Simulator để thử không cần máy in thật.");

        FormClosing += (_, _) =>
        {
            _settings.LastRoute = _cboRoute.Text;
            _settings.Language = _cboLang.SelectedItem as string;
            _settings.Encoding = _cboEncoding.SelectedItem as string;
            _settings.Save();
        };
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================

    /// <summary>Đặt vị trí splitter một lần khi control có kích thước thật.</summary>
    private static void OnFirstLayout(SplitContainer split, Action apply)
    {
        bool done = false;
        split.SizeChanged += (_, _) =>
        {
            if (done || split.Width < 200 || split.Height < 120) return;
            done = true;
            try { apply(); } catch (InvalidOperationException) { } catch (ArgumentOutOfRangeException) { }
        };
    }

    private static TextBox MakeEditor(bool readOnly = false) => new()
    {
        Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill,
        Font = Theme.MonoFont, AcceptsReturn = true, AcceptsTab = true, ReadOnly = readOnly,
    };

    private static ListView MakeList(params (string text, int width)[] columns)
    {
        var lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
        foreach (var (text, width) in columns) lv.Columns.Add(text, width);
        return lv;
    }

    private static Control WithCaption(Control inner, string caption)
    {
        var p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 2, 6, 6), BackColor = Theme.BgDark };
        var lbl = new Label { Text = caption, Dock = DockStyle.Top, Height = 22, ForeColor = Theme.TextDim, TextAlign = ContentAlignment.MiddleLeft };
        inner.Dock = DockStyle.Fill;
        p.Controls.Add(inner);
        p.Controls.Add(lbl);
        return p;
    }

    private RouteConfig CurrentRoute()
    {
        string text = _cboRoute.Text.Trim();
        var route = RouteConfig.Parse(text);
        _settings.Remember(text);
        if (!_cboRoute.Items.Contains(text)) _cboRoute.Items.Insert(0, text);
        return route;
    }

    private ICommandLanguage CurrentLanguage() =>
        _hub.Languages.Create(_cboLang.SelectedItem as string ?? "RAW", _cboEncoding.SelectedItem as string);

    private Encoding CurrentEncoding() => TextEncodings.Get(_cboEncoding.SelectedItem as string);

    private void AppendLog(string tag, string message)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(() => AppendLog(tag, message)); } catch (InvalidOperationException) { }
            return;
        }
        Color color = tag switch
        {
            "ERR" => Theme.Error, "WRN" => Theme.Warn, "TX" => Theme.Accent, "RX" => Theme.AccentCyan, _ => Theme.Text,
        };
        _txtLog.SelectionStart = _txtLog.TextLength;
        _txtLog.SelectionColor = Theme.TextDim;
        _txtLog.AppendText($"{DateTime.Now:HH:mm:ss.fff} ");
        _txtLog.SelectionColor = color;
        _txtLog.AppendText($"[{tag}] {message}{Environment.NewLine}");
        _txtLog.ScrollToCaret();
        if (_txtLog.TextLength > 2_000_000) _txtLog.Clear();
    }

    private void SetStatus(string text) => _lblStatus.Text = text;

    private void ShowState(PrinterStatus st)
    {
        _lblPrinterState.Text = $"●  {st}";
        _lblPrinterState.ForeColor = st.IsError ? Theme.Error
            : st.State == PrinterState.Unknown ? Theme.TextDim
            : st.Warnings != PrinterWarnings.None || st.State == PrinterState.Offline ? Theme.Warn
            : Theme.Ok;
    }

    /// <summary>Chạy thao tác bất đồng bộ có khoá UI, bắt lỗi và hiển thị.</summary>
    private async Task RunBusyAsync(string what, Func<CancellationToken, Task> action)
    {
        if (_busyCts is not null) { SetStatus("Đang bận, chờ thao tác trước..."); return; }
        _busyCts = new CancellationTokenSource();
        UseWaitCursor = true;
        SetStatus(what + "...");
        try
        {
            await action(_busyCts.Token);
            SetStatus(what + " — xong.");
        }
        catch (OperationCanceledException) { SetStatus(what + " — đã huỷ."); }
        catch (Exception ex)
        {
            _hub.Log.Error(what, ex);
            SetStatus($"{what} — lỗi: {ex.Message}");
        }
        finally
        {
            UseWaitCursor = false;
            _busyCts.Dispose();
            _busyCts = null;
        }
    }

    private void ShowRouteHelp() => MessageBox.Show(this, """
        tcp://192.168.1.50:9100                      Raw TCP (SATO/Zebra/Godex/FUJIFILM) – hai chiều
        sato4://192.168.1.50?data=1024&status=1025   SATO Status4 hai cổng
        lpr://192.168.1.60/lp                        LPR/LPD (515)
        spooler://Tên máy in Windows                 Spooler RAW qua driver bất kỳ (USB/LPT/COM/TCP)
        passthrough://Tên máy in?prefix=${&suffix=}$ Driver passthrough (ZDesigner, SATO Command Font)
        serial://COM3?baud=9600&handshake=rtscts     RS-232C / USB-Serial / Bluetooth SPP
        file://\\PC01\SharedPrinter                  Máy in chia sẻ (copy /b) hoặc file

        Tuỳ chọn chung: connectTimeout=3000, readTimeout=2000
        """, "Cú pháp route", MessageBoxButtons.OK, MessageBoxIcon.Information);

    // =====================================================================
    //  TRẠNG THÁI
    // =====================================================================

    private Task CheckStatusAsync() => RunBusyAsync("Đọc trạng thái", async ct =>
    {
        var st = await _service.GetStatusAsync(CurrentRoute(), CurrentLanguage(), ct);
        ShowState(st);
        _hub.Log.Info($"Trạng thái: {st}");
    });

    // =====================================================================
    //  TAB RAW
    // =====================================================================

    private byte[] BuildRawBytes()
    {
        string text = _txtRaw.Text;
        text = _chkStripNewLines.Checked
            ? text.Replace("\r", "").Replace("\n", "")
            : text.Replace("\r\n", "\n").Replace("\n", "\r\n");
        return ControlChars.Parse(text, CurrentEncoding(), _chkSbplTags.Checked);
    }

    private Task SendRawAsync() => RunBusyAsync("Gửi lệnh raw", async ct =>
    {
        byte[] data = BuildRawBytes();
        var lang = CurrentLanguage();
        byte[] reply = await _service.SendAndReceiveAsync(CurrentRoute(), data, TimeSpan.FromMilliseconds((double)_numWait.Value), ct);
        var sb = new StringBuilder();
        sb.AppendLine($"Đã gửi {data.Length} bytes. Phản hồi {reply.Length} bytes.");
        if (reply.Length > 0)
        {
            sb.AppendLine().AppendLine(ControlChars.ToDisplay(reply, CurrentEncoding()));
            sb.AppendLine().AppendLine(HexDump.Format(reply));
            if (lang.TryParseStatus(reply, out var st))
            {
                sb.AppendLine($"→ Trạng thái ({lang.Name}): {st}");
                ShowState(st);
            }
        }
        _txtResponse.Text = sb.ToString().Replace("\n", Environment.NewLine).Replace("\r\r", "\r");
    });

    private void ShowRawHex()
    {
        try
        {
            byte[] data = BuildRawBytes();
            _txtResponse.Text = $"{data.Length} bytes sẽ gửi:{Environment.NewLine}{HexDump.Format(data).Replace("\n", Environment.NewLine)}";
        }
        catch (Exception ex) { _txtResponse.Text = ex.Message; }
    }

    private void OpenRawFile()
    {
        using var dlg = new OpenFileDialog { Filter = "Lệnh máy in|*.prn;*.txt;*.zpl;*.sbpl;*.ezpl;*.pcl;*.ps;*.pdf|Tất cả|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        byte[] bytes = File.ReadAllBytes(dlg.FileName);
        _chkSbplTags.Checked = false;
        _chkStripNewLines.Checked = false;
        // Hiển thị dạng ký hiệu để giữ nguyên byte điều khiển khi gửi lại
        _txtRaw.Text = ControlChars.ToDisplay(bytes, CurrentEncoding(), keepNewLines: false);
        _chkStripNewLines.Checked = true;
        _hub.Log.Info($"Đã mở {dlg.FileName} ({bytes.Length} bytes)");
    }

    // =====================================================================
    //  TAB NHÃN
    // =====================================================================

    private void LoadDemoLabel(bool template)
    {
        var doc = template ? DemoLabels.Template(203) : DemoLabels.Product(203);
        _txtLabelJson.Text = DemoLabels.ToJson(doc).Replace("\n", Environment.NewLine);
    }

    private LabelDocument CurrentLabel()
    {
        var doc = DemoLabels.FromJson(_txtLabelJson.Text);
        doc.Copies = (int)_numCopies.Value;
        return doc;
    }

    private void RefreshLabelPreview()
    {
        try
        {
            var doc = CurrentLabel();
            float scale = Math.Min(1.5f, 900f / Math.Max(1, doc.Width));
            var old = _picPreview.Image;
            _picPreview.Image = GdiLabelRenderer.Preview(doc, scale);
            old?.Dispose();

            var lang = CurrentLanguage();
            if (lang.CanRenderLabel)
            {
                byte[] bytes = lang.Render(doc);
                _txtCommands.Text = $"{lang.Name} · {bytes.Length} bytes · {lang.TextEncoding.WebName}{Environment.NewLine}{Environment.NewLine}" +
                    ControlChars.ToDisplay(bytes, lang.TextEncoding).Replace("\n", Environment.NewLine);
            }
            else _txtCommands.Text = $"{lang.Name} không render nhãn — chọn SBPL / ZPL / EZPL.";
        }
        catch (Exception ex)
        {
            _txtCommands.Text = "Lỗi JSON / render: " + ex.Message;
        }
    }

    private Task PrintLabelAsync() => RunBusyAsync("In nhãn", async ct =>
    {
        var r = await _service.PrintAsync(CurrentRoute(), CurrentLanguage(), PrintJob.FromLabel(CurrentLabel()), 1, 1000, ct);
        _hub.Log.Info(r.ToString());
        if (r.StatusBefore is { } st) ShowState(st);
    });

    private void PrintLabelGdi()
    {
        try
        {
            string? printer = _cboRoute.Text.StartsWith("spooler://", StringComparison.OrdinalIgnoreCase) ||
                              _cboRoute.Text.StartsWith("passthrough://", StringComparison.OrdinalIgnoreCase)
                ? RouteConfig.Parse(_cboRoute.Text).Queue
                : _lvPrinters.SelectedItems.Count > 0 ? _lvPrinters.SelectedItems[0].Text : WindowsPrinters.GetDefaultPrinterName();
            if (string.IsNullOrEmpty(printer)) throw new InvalidOperationException("Chọn máy in Windows (tab Máy in Windows) hoặc route spooler://.");
            GdiLabelPrinter.Print(printer, CurrentLabel(), _hub.Log);
            SetStatus($"Đã gửi nhãn GDI tới '{printer}'.");
        }
        catch (Exception ex) { _hub.Log.Error("In GDI", ex); }
    }

    private Task PrintCsvBatchAsync()
    {
        using var dlg = new OpenFileDialog { Filter = "CSV|*.csv;*.txt|Tất cả|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return Task.CompletedTask;
        string path = dlg.FileName;
        return RunBusyAsync("In hàng loạt CSV", async ct =>
        {
            var template = CurrentLabel();
            var rows = CsvLite.ReadFile(path);
            var route = CurrentRoute();
            var lang = CurrentLanguage();
            int ok = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var doc = template.Bind(rows[i]);
                if (rows[i].TryGetValue("Copies", out var c) && int.TryParse(c, out int copies)) doc.Copies = copies;
                var r = await _service.PrintAsync(route, lang, PrintJob.FromLabel(doc, $"CSV#{i + 1}"), 1, 1000, ct);
                if (r.Success) ok++;
                SetStatus($"In hàng loạt: {i + 1}/{rows.Count} (thành công {ok})");
            }
            _hub.Log.Info($"In hàng loạt xong: {ok}/{rows.Count}");
        });
    }

    private void OpenLabelJson()
    {
        using var dlg = new OpenFileDialog { Filter = "Nhãn JSON|*.json|Tất cả|*.*" };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _txtLabelJson.Text = TextEncodings.ReadAllTextAuto(dlg.FileName).text.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
    }

    private void SaveLabelJson()
    {
        using var dlg = new SaveFileDialog { Filter = "Nhãn JSON|*.json", FileName = "label.json" };
        if (dlg.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dlg.FileName, _txtLabelJson.Text, TextEncodings.Utf8);
    }

    private void SaveRenderedCommands()
    {
        try
        {
            var lang = CurrentLanguage();
            byte[] bytes = lang.Render(CurrentLabel());
            using var dlg = new SaveFileDialog { Filter = "Lệnh máy in|*.prn|Tất cả|*.*", FileName = $"label.{lang.Name.ToLowerInvariant()}.prn" };
            if (dlg.ShowDialog(this) == DialogResult.OK) File.WriteAllBytes(dlg.FileName, bytes);
        }
        catch (Exception ex) { _hub.Log.Error("Lưu lệnh", ex); }
    }

    private void AddLogo()
    {
        using var dlg = new OpenFileDialog { Filter = "Ảnh|*.png;*.bmp;*.jpg;*.gif|Tất cả|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var doc = CurrentLabel();
            var img = GdiLabelRenderer.FromImageFile(dlg.FileName, maxWidthDots: doc.Width / 3);
            doc.Image(doc.Width - img.Width - doc.Mm(4), doc.Height - img.Height - doc.Mm(4), img);
            _txtLabelJson.Text = DemoLabels.ToJson(doc).Replace("\n", Environment.NewLine);
        }
        catch (Exception ex) { _hub.Log.Error("Thêm logo", ex); }
    }

    // =====================================================================
    //  TAB MÁY IN WINDOWS
    // =====================================================================

    private void RefreshWindowsPrinters()
    {
        _lvPrinters.Items.Clear();
        try
        {
            foreach (var p in WindowsPrinters.List())
            {
                var item = new ListViewItem(new[] { p.Name, p.Brand, p.PortName ?? "", p.DriverName ?? "", p.StatusText, p.Jobs.ToString() });
                if (p.IsDefault) item.Font = new Font(Theme.UiFont, FontStyle.Bold);
                _lvPrinters.Items.Add(item);
                string route = $"spooler://{p.Name}";
                if (!_cboRoute.Items.Contains(route)) _cboRoute.Items.Add(route);
            }
        }
        catch (Exception ex) { _hub.Log.Error("Liệt kê máy in", ex); }
    }

    private void RefreshJobs()
    {
        _lvJobs.Items.Clear();
        if (_lvPrinters.SelectedItems.Count == 0) return;
        try
        {
            foreach (var j in WindowsPrinters.ListJobs(_lvPrinters.SelectedItems[0].Text))
                _lvJobs.Items.Add(new ListViewItem(new[] { j.JobId.ToString(), j.Document ?? "", j.User ?? "", j.StatusText ?? $"0x{j.Status:X}", $"{j.PagesPrinted}/{j.TotalPages}" }));
        }
        catch (Exception ex) { _hub.Log.Error("Liệt kê job", ex); }
    }

    private void UseSelectedPrinter(string kind)
    {
        if (_lvPrinters.SelectedItems.Count == 0) return;
        var item = _lvPrinters.SelectedItems[0];
        _cboRoute.Text = $"{kind}://{item.Text}";
        _cboLang.SelectedItem = item.SubItems[1].Text switch
        {
            "SATO" => "SBPL", "Zebra" => "ZPL", "Godex" => "EZPL", "FUJIFILM" => "PJL", _ => _cboLang.SelectedItem,
        };
        SetStatus($"Route: {_cboRoute.Text}");
    }

    // =====================================================================
    //  TAB DISCOVER
    // =====================================================================

    private Task DiscoverAsync() => RunBusyAsync("Quét mạng", async ct =>
    {
        _lvFound.Items.Clear();
        int[] ports = _txtPorts.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
        var progress = new Progress<string>(m => _hub.Log.Info("Tìm thấy " + m));
        var found = await NetworkDiscovery.ScanAsync(NetworkDiscovery.Subnet(_txtSubnet.Text.Trim()), ports, 300, progress: progress, ct: ct);
        foreach (var f in found)
            _lvFound.Items.Add(new ListViewItem(new[] { f.Host, string.Join(",", f.OpenPorts), f.Guess }) { Tag = f });
        _hub.Log.Info($"Quét xong: {found.Count} thiết bị.");
    });

    private void UseDiscovered()
    {
        if (_lvFound.SelectedItems.Count == 0 || _lvFound.SelectedItems[0].Tag is not DiscoveredPrinter p) return;
        _cboRoute.Text = p.OpenPorts.Contains(1024) && p.OpenPorts.Contains(1025)
            ? $"sato4://{p.Host}?data=1024&status=1025"
            : p.OpenPorts.Contains(9100) ? $"tcp://{p.Host}:9100"
            : p.OpenPorts.Contains(515) ? $"lpr://{p.Host}/lp"
            : $"tcp://{p.Host}:{p.OpenPorts[0]}";
        if (p.OpenPorts.Contains(1024) && p.OpenPorts.Contains(1025)) _cboLang.SelectedItem = "SBPL";
        else if (p.OpenPorts.Contains(9200) || p.OpenPorts.Contains(6101)) _cboLang.SelectedItem = "ZPL";
        SetStatus($"Route: {_cboRoute.Text}");
    }

    // =====================================================================
    //  TAB PROFILES
    // =====================================================================

    private void LoadProfilesText()
    {
        try
        {
            if (!File.Exists(AppSettings.ProfilesPath)) AppSettings.DefaultProfiles().Save(AppSettings.ProfilesPath);
            _txtProfiles.Text = File.ReadAllText(AppSettings.ProfilesPath).Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            RefreshProfileCombo();
        }
        catch (Exception ex) { _hub.Log.Error("Đọc profiles", ex); }
    }

    private ProfileStore ParseProfiles() =>
        System.Text.Json.JsonSerializer.Deserialize<ProfileStore>(_txtProfiles.Text, ProfileStore.JsonOptions) ?? new ProfileStore();

    private void RefreshProfileCombo()
    {
        _cboProfile.Items.Clear();
        foreach (var p in ParseProfiles().Printers) _cboProfile.Items.Add(p.Name);
        if (_cboProfile.Items.Count > 0) _cboProfile.SelectedIndex = 0;
    }

    private void SaveProfilesText()
    {
        try
        {
            ParseProfiles(); // kiểm tra JSON hợp lệ
            File.WriteAllText(AppSettings.ProfilesPath, _txtProfiles.Text, TextEncodings.Utf8);
            RefreshProfileCombo();
            SetStatus("Đã lưu profiles.json");
        }
        catch (Exception ex) { _hub.Log.Error("Lưu profiles (JSON không hợp lệ?)", ex); }
    }

    private PrinterProfile? SelectedProfile() =>
        _cboProfile.SelectedItem is string name ? ParseProfiles().Find(name) : null;

    private void ApplySelectedProfile()
    {
        if (SelectedProfile() is not { } p) return;
        if (p.Routes.Count > 0) _cboRoute.Text = p.Routes[0];
        _cboLang.SelectedItem = p.Language.ToUpperInvariant();
        _cboEncoding.SelectedItem = TextEncodings.Get(p.Encoding).CodePage == 932 ? "shift_jis" : p.Encoding.ToLowerInvariant();
        SetStatus($"Áp dụng profile {p.Name}");
    }

    private Task PrintWithProfileAsync() => RunBusyAsync("In theo profile", async ct =>
    {
        var p = SelectedProfile() ?? throw new InvalidOperationException("Chưa chọn profile.");
        var r = await _service.PrintAsync(p, PrintJob.FromLabel(CurrentLabel()), ct);
        _hub.Log.Info(r.ToString());
    });
}
