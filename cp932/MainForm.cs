using System.Text;
using System.Drawing.Drawing2D;

namespace CsvReader;

public class MainForm : Form
{
    // === UI Controls ===
    private MenuStrip menuStrip = null!;
    private ToolStrip toolStrip = null!;
    private DataGridView dataGridView = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel lblStatus = null!;
    private ToolStripStatusLabel lblEncoding = null!;
    private ToolStripStatusLabel lblRowCount = null!;
    private ToolStripStatusLabel lblDelimiter = null!;
    private ComboBox cboEncoding = null!;
    private Panel topPanel = null!;
    private Label lblTitle = null!;

    // === State ===
    private string? currentFilePath;
    private byte[]? currentFileData;
    private Encoding currentEncoding = Encoding.UTF8;
    private char currentDelimiter = ',';

    // === Colors (Dark Theme) ===
    private static readonly Color BgDark = Color.FromArgb(24, 24, 32);
    private static readonly Color BgPanel = Color.FromArgb(32, 34, 48);
    private static readonly Color BgGrid = Color.FromArgb(28, 30, 42);
    private static readonly Color BgGridAlt = Color.FromArgb(36, 38, 54);
    private static readonly Color BgHeader = Color.FromArgb(48, 52, 72);
    private static readonly Color AccentBlue = Color.FromArgb(80, 140, 255);
    private static readonly Color AccentCyan = Color.FromArgb(0, 210, 210);
    private static readonly Color TextPrimary = Color.FromArgb(230, 235, 245);
    private static readonly Color TextSecondary = Color.FromArgb(150, 160, 180);
    private static readonly Color BorderColor = Color.FromArgb(55, 60, 80);

    public MainForm()
    {
        InitializeComponent();
        ApplyDarkTheme();
    }

    private void InitializeComponent()
    {
        // === Form Settings ===
        Text = "CSV Reader — Shift-JIS ＆ UTF-8";
        Size = new Size(1100, 700);
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9.5f);
        DoubleBuffered = true;

        // === Menu Strip ===
        menuStrip = new MenuStrip
        {
            BackColor = BgPanel,
            ForeColor = TextPrimary,
            Padding = new Padding(8, 2, 0, 2),
            RenderMode = ToolStripRenderMode.Professional,
            Renderer = new DarkToolStripRenderer()
        };

        var fileMenu = new ToolStripMenuItem("📁 File (&F)") { ForeColor = TextPrimary };
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("Mở file CSV... (&O)", null, OnOpenFile) { ShortcutKeys = Keys.Control | Keys.O, ForeColor = TextPrimary });
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("Xuất ra UTF-8... (&E)", null, OnExportUtf8) { ShortcutKeys = Keys.Control | Keys.E, ForeColor = TextPrimary });
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("Thoát (&X)", null, (s, e) => Close()) { ShortcutKeys = Keys.Alt | Keys.F4, ForeColor = TextPrimary });

        var viewMenu = new ToolStripMenuItem("👁 View (&V)") { ForeColor = TextPrimary };
        viewMenu.DropDownItems.Add(new ToolStripMenuItem("Reload với UTF-8", null, (s, e) => ReloadWithEncoding(Encoding.UTF8)) { ForeColor = TextPrimary });
        viewMenu.DropDownItems.Add(new ToolStripMenuItem("Reload với Shift-JIS (932)", null, (s, e) => ReloadWithEncoding(Encoding.GetEncoding(932))) { ForeColor = TextPrimary });
        viewMenu.DropDownItems.Add(new ToolStripMenuItem("Reload với UTF-16", null, (s, e) => ReloadWithEncoding(Encoding.Unicode)) { ForeColor = TextPrimary });

        var helpMenu = new ToolStripMenuItem("❓ Help (&H)") { ForeColor = TextPrimary };
        helpMenu.DropDownItems.Add(new ToolStripMenuItem("Giới thiệu...", null, OnAbout) { ForeColor = TextPrimary });

        menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, viewMenu, helpMenu });
        MainMenuStrip = menuStrip;

        // === Top Panel (Title + Encoding selector) ===
        topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = BgPanel,
            Padding = new Padding(16, 8, 16, 8)
        };

        lblTitle = new Label
        {
            Text = "📄 CSV Reader",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = AccentBlue,
            AutoSize = true,
            Location = new Point(16, 14)
        };

        var lblEnc = new Label
        {
            Text = "Encoding:",
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(680, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        cboEncoding = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = BgGrid,
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Width = 180,
            Location = new Point(760, 16),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        cboEncoding.Items.AddRange(new object[]
        {
            "Tự động phát hiện",
            "UTF-8",
            "Shift-JIS (CP932)",
            "UTF-16 LE",
            "UTF-16 BE"
        });
        cboEncoding.SelectedIndex = 0;
        cboEncoding.SelectedIndexChanged += OnEncodingChanged;

        topPanel.Controls.AddRange(new Control[] { lblTitle, lblEnc, cboEncoding });

        // === ToolStrip ===
        toolStrip = new ToolStrip
        {
            BackColor = BgPanel,
            ForeColor = TextPrimary,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(12, 0, 0, 0),
            Renderer = new DarkToolStripRenderer()
        };

        var btnOpen = new ToolStripButton("📂 Mở File") { ForeColor = TextPrimary };
        btnOpen.Click += OnOpenFile;

        var btnReload = new ToolStripButton("🔄 Reload") { ForeColor = TextPrimary };
        btnReload.Click += (s, e) => { if (currentFilePath != null) LoadFile(currentFilePath); };

        var btnExport = new ToolStripButton("💾 Xuất UTF-8") { ForeColor = TextPrimary };
        btnExport.Click += OnExportUtf8;

        toolStrip.Items.AddRange(new ToolStripItem[]
        {
            btnOpen,
            new ToolStripSeparator(),
            btnReload,
            new ToolStripSeparator(),
            btnExport
        });

        // === DataGridView ===
        dataGridView = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
            BackgroundColor = BgGrid,
            BorderStyle = BorderStyle.None,
            GridColor = BorderColor,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 38,
            RowTemplate = { Height = 32 }
        };

        // Header style
        dataGridView.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = BgHeader,
            ForeColor = AccentCyan,
            Font = new Font("Segoe UI Semibold", 10f),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0),
            SelectionBackColor = BgHeader,
            SelectionForeColor = AccentCyan
        };

        // Cell style
        dataGridView.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = BgGrid,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9.5f),
            SelectionBackColor = Color.FromArgb(50, 80, 140, 255),
            SelectionForeColor = TextPrimary,
            Padding = new Padding(6, 2, 6, 2)
        };

        // Alternating row style
        dataGridView.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = BgGridAlt,
            ForeColor = TextPrimary,
            SelectionBackColor = Color.FromArgb(50, 80, 140, 255),
            SelectionForeColor = TextPrimary
        };

        // === Status Strip ===
        statusStrip = new StatusStrip
        {
            BackColor = BgPanel,
            ForeColor = TextSecondary,
            SizingGrip = false
        };

        lblStatus = new ToolStripStatusLabel("Sẵn sàng. Mở file CSV để bắt đầu. (Ctrl+O)")
        {
            ForeColor = TextSecondary,
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        lblEncoding = new ToolStripStatusLabel("Encoding: —")
        {
            ForeColor = AccentCyan,
            BorderSides = ToolStripStatusLabelBorderSides.Left,
            BorderStyle = Border3DStyle.Etched
        };
        lblRowCount = new ToolStripStatusLabel("Dòng: 0")
        {
            ForeColor = TextSecondary,
            BorderSides = ToolStripStatusLabelBorderSides.Left,
            BorderStyle = Border3DStyle.Etched
        };
        lblDelimiter = new ToolStripStatusLabel("Delimiter: ,")
        {
            ForeColor = TextSecondary,
            BorderSides = ToolStripStatusLabelBorderSides.Left,
            BorderStyle = Border3DStyle.Etched
        };

        statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus, lblEncoding, lblRowCount, lblDelimiter });

        // === Layout ===
        Controls.Add(dataGridView);
        Controls.Add(toolStrip);
        Controls.Add(topPanel);
        Controls.Add(menuStrip);
        Controls.Add(statusStrip);

        // === Drag and Drop ===
        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
    }

    // ============================
    // === Event Handlers ===
    // ============================

    private void OnOpenFile(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Mở file CSV",
            Filter = "CSV Files (*.csv)|*.csv|TSV Files (*.tsv;*.tab)|*.tsv;*.tab|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            LoadFile(ofd.FileName);
        }
    }

    private void OnExportUtf8(object? sender, EventArgs e)
    {
        if (currentFileData == null || currentFilePath == null)
        {
            MessageBox.Show("Chưa mở file nào.", "Thông báo",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Title = "Xuất ra file UTF-8",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = Path.GetFileNameWithoutExtension(currentFilePath) + "_utf8.csv"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var (enc, bomLen) = EncodingDetector.DetectEncoding(currentFileData);
                if (cboEncoding.SelectedIndex > 0)
                    enc = GetSelectedEncoding();

                string content = enc.GetString(currentFileData, bomLen, currentFileData.Length - bomLen);
                File.WriteAllText(sfd.FileName, content, new UTF8Encoding(true));

                lblStatus.Text = $"✅ Đã xuất ra: {sfd.FileName}";
                MessageBox.Show($"Đã xuất thành công!\n{sfd.FileName}", "Hoàn tất",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất file:\n{ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void OnEncodingChanged(object? sender, EventArgs e)
    {
        if (currentFileData != null && currentFilePath != null)
        {
            ReloadCurrentFile();
        }
    }

    private void OnAbout(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "CSV Reader v1.0\n\n" +
            "Đọc file CSV hỗ trợ nhiều encoding:\n" +
            "• UTF-8 (có/không BOM)\n" +
            "• Shift-JIS (CP932) — Tiếng Nhật\n" +
            "• UTF-16 LE/BE\n\n" +
            "Tính năng:\n" +
            "• Tự động phát hiện encoding\n" +
            "• Tự động phát hiện delimiter\n" +
            "• Chuyển đổi encoding thủ công\n" +
            "• Xuất ra UTF-8\n" +
            "• Kéo thả file\n\n" +
            "Hỗ trợ tiếng Nhật (日本語) và tiếng Việt (Tiếng Việt)",
            "Giới thiệu",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            LoadFile(files[0]);
        }
    }

    // ============================
    // === Core Logic ===
    // ============================

    private void LoadFile(string filePath)
    {
        try
        {
            currentFilePath = filePath;
            currentFileData = File.ReadAllBytes(filePath);

            Text = $"CSV Reader — {Path.GetFileName(filePath)}";
            ReloadCurrentFile();

            lblStatus.Text = $"✅ Đã mở: {filePath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi đọc file:\n{ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblStatus.Text = $"❌ Lỗi: {ex.Message}";
        }
    }

    private void ReloadCurrentFile()
    {
        if (currentFileData == null) return;

        Encoding enc;
        int bomLen;

        if (cboEncoding.SelectedIndex == 0) // Tự động
        {
            (enc, bomLen) = EncodingDetector.DetectEncoding(currentFileData);
        }
        else
        {
            enc = GetSelectedEncoding();
            bomLen = DetectBomLength(currentFileData);
        }

        currentEncoding = enc;

        string content = enc.GetString(currentFileData, bomLen, currentFileData.Length - bomLen);

        // Phát hiện delimiter
        currentDelimiter = CsvParser.DetectDelimiter(content);

        // Parse CSV
        var rows = CsvParser.Parse(content, currentDelimiter);

        // Hiển thị trong DataGridView
        DisplayData(rows);

        // Cập nhật status bar
        lblEncoding.Text = $"Encoding: {EncodingDetector.GetDisplayName(enc)}";
        lblRowCount.Text = $"Dòng: {rows.Count}";
        lblDelimiter.Text = $"Delimiter: {GetDelimiterName(currentDelimiter)}";
    }

    private void ReloadWithEncoding(Encoding enc)
    {
        if (currentFileData == null)
        {
            MessageBox.Show("Chưa mở file nào.", "Thông báo",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Cập nhật combo box
        cboEncoding.SelectedIndexChanged -= OnEncodingChanged;
        cboEncoding.SelectedIndex = enc.CodePage switch
        {
            65001 => 1,
            932 => 2,
            1200 => 3,
            1201 => 4,
            _ => 0
        };
        cboEncoding.SelectedIndexChanged += OnEncodingChanged;

        ReloadCurrentFile();
    }

    private void DisplayData(List<string[]> rows)
    {
        dataGridView.SuspendLayout();
        dataGridView.Columns.Clear();
        dataGridView.Rows.Clear();

        if (rows.Count == 0)
        {
            dataGridView.ResumeLayout();
            return;
        }

        // Dòng đầu tiên là header
        int columnCount = rows.Max(r => r.Length);
        string[] headers = rows[0];

        for (int col = 0; col < columnCount; col++)
        {
            string headerText = col < headers.Length ? headers[col] : $"Col_{col + 1}";
            if (string.IsNullOrWhiteSpace(headerText))
                headerText = $"Col_{col + 1}";

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = headerText,
                Name = $"col_{col}",
                MinimumWidth = 60
            });
        }

        // Dữ liệu (bỏ dòng header)
        for (int row = 1; row < rows.Count; row++)
        {
            var rowData = rows[row];

            // Bỏ qua dòng trống
            if (rowData.Length == 0 || (rowData.Length == 1 && string.IsNullOrWhiteSpace(rowData[0])))
                continue;

            var dgvRow = new DataGridViewRow();
            dgvRow.CreateCells(dataGridView);

            for (int col = 0; col < columnCount; col++)
            {
                dgvRow.Cells[col].Value = col < rowData.Length ? rowData[col] : "";
            }

            dataGridView.Rows.Add(dgvRow);
        }

        // Auto-resize nhưng giới hạn chiều rộng tối đa
        dataGridView.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
        foreach (DataGridViewColumn col in dataGridView.Columns)
        {
            if (col.Width > 400) col.Width = 400;
            if (col.Width < 80) col.Width = 80;
        }

        dataGridView.ResumeLayout();
    }

    // ============================
    // === Helpers ===
    // ============================

    private Encoding GetSelectedEncoding()
    {
        return cboEncoding.SelectedIndex switch
        {
            1 => Encoding.UTF8,
            2 => Encoding.GetEncoding(932),
            3 => Encoding.Unicode,
            4 => Encoding.BigEndianUnicode,
            _ => Encoding.UTF8
        };
    }

    private static int DetectBomLength(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) return 3;
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) return 2;
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF) return 2;
        return 0;
    }

    private static string GetDelimiterName(char delimiter)
    {
        return delimiter switch
        {
            ',' => "Comma (,)",
            '\t' => "Tab (\\t)",
            ';' => "Semicolon (;)",
            '|' => "Pipe (|)",
            _ => delimiter.ToString()
        };
    }

    private void ApplyDarkTheme()
    {
        // Đã áp dụng trong InitializeComponent
    }

    // ============================
    // === Custom Renderer (Dark Theme cho ToolStrip/MenuStrip) ===
    // ============================

    private class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                var rect = new Rectangle(Point.Empty, e.Item.Size);
                using var brush = new SolidBrush(Color.FromArgb(50, 80, 140, 255));
                e.Graphics.FillRectangle(brush, rect);
            }
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(BgPanel);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                var rect = new Rectangle(1, 1, e.Item.Width - 3, e.Item.Height - 3);
                using var brush = new SolidBrush(Color.FromArgb(40, 80, 140, 255));
                using var pen = new Pen(AccentBlue, 1);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillRectangle(brush, rect);
                e.Graphics.DrawRectangle(pen, rect);
            }
        }
    }

    private class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => BorderColor;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Color.FromArgb(50, 80, 140, 255);
        public override Color MenuStripGradientBegin => BgPanel;
        public override Color MenuStripGradientEnd => BgPanel;
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(50, 80, 140, 255);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(50, 80, 140, 255);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(70, 80, 140, 255);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(70, 80, 140, 255);
        public override Color ToolStripDropDownBackground => BgPanel;
        public override Color ImageMarginGradientBegin => BgPanel;
        public override Color ImageMarginGradientMiddle => BgPanel;
        public override Color ImageMarginGradientEnd => BgPanel;
        public override Color SeparatorDark => BorderColor;
        public override Color SeparatorLight => BorderColor;
    }
}
