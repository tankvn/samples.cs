namespace PrinterHub.App;

/// <summary>Bảng màu dark theme (đồng bộ với dự án cp932).</summary>
internal static class Theme
{
    public static readonly Color BgDark = Color.FromArgb(24, 24, 32);
    public static readonly Color BgPanel = Color.FromArgb(32, 34, 48);
    public static readonly Color BgInput = Color.FromArgb(28, 30, 42);
    public static readonly Color BgButton = Color.FromArgb(48, 52, 72);
    public static readonly Color BgButtonHover = Color.FromArgb(64, 70, 96);
    public static readonly Color Accent = Color.FromArgb(80, 140, 255);
    public static readonly Color AccentCyan = Color.FromArgb(0, 210, 210);
    public static readonly Color Ok = Color.FromArgb(80, 200, 120);
    public static readonly Color Warn = Color.FromArgb(240, 190, 60);
    public static readonly Color Error = Color.FromArgb(240, 90, 90);
    public static readonly Color Text = Color.FromArgb(230, 235, 245);
    public static readonly Color TextDim = Color.FromArgb(150, 160, 180);
    public static readonly Color Border = Color.FromArgb(55, 60, 80);

    public static readonly Font UiFont = new("Segoe UI", 9.5f);
    public static readonly Font MonoFont = new("Consolas", 10f);
    public static readonly Font TitleFont = new("Segoe UI", 14f, FontStyle.Bold);

    /// <summary>Áp dụng màu cho control và toàn bộ con (đệ quy).</summary>
    public static void Apply(Control root)
    {
        foreach (Control c in root.Controls)
        {
            switch (c)
            {
                case Button b:
                    b.FlatStyle = FlatStyle.Flat;
                    b.BackColor = BgButton;
                    b.ForeColor = Text;
                    b.FlatAppearance.BorderColor = Border;
                    b.FlatAppearance.MouseOverBackColor = BgButtonHover;
                    b.Cursor = Cursors.Hand;
                    break;
                case TextBoxBase t:
                    t.BackColor = BgInput;
                    t.ForeColor = Text;
                    t.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox cb:
                    cb.BackColor = BgInput;
                    cb.ForeColor = Text;
                    cb.FlatStyle = FlatStyle.Flat;
                    break;
                case ListView lv:
                    lv.BackColor = BgInput;
                    lv.ForeColor = Text;
                    lv.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case CheckBox ck:
                    ck.ForeColor = Text;
                    break;
                case NumericUpDown n:
                    n.BackColor = BgInput;
                    n.ForeColor = Text;
                    break;
                case Label l:
                    if (l.ForeColor == SystemColors.ControlText) l.ForeColor = TextDim;
                    break;
                case TabPage tp:
                    tp.BackColor = BgDark;
                    tp.ForeColor = Text;
                    break;
                case Panel or SplitContainer or GroupBox:
                    c.BackColor = c.BackColor == SystemColors.Control ? BgDark : c.BackColor;
                    c.ForeColor = Text;
                    break;
            }
            if (c.HasChildren) Apply(c);
        }
    }

    public static Button MakeButton(string text, EventHandler onClick, int width = 0)
    {
        var b = new Button { Text = text, AutoSize = width == 0, Height = 30, Margin = new Padding(3), Padding = new Padding(6, 0, 6, 0) };
        if (width > 0) b.Width = width;
        b.Click += onClick;
        return b;
    }

    public static Label MakeLabel(string text) =>
        new() { Text = text, AutoSize = true, Margin = new Padding(3, 9, 3, 3), ForeColor = TextDim };
}

/// <summary>Vẽ TabControl theo dark theme (owner-draw).</summary>
internal sealed class DarkTabControl : TabControl
{
    public DarkTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(150, 30);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.BgPanel);
        for (int i = 0; i < TabCount; i++)
        {
            var r = GetTabRect(i);
            bool sel = i == SelectedIndex;
            using var bg = new SolidBrush(sel ? Theme.BgDark : Theme.BgPanel);
            e.Graphics.FillRectangle(bg, r);
            if (sel)
            {
                using var accent = new SolidBrush(Theme.Accent);
                e.Graphics.FillRectangle(accent, r.X, r.Bottom - 3, r.Width, 3);
            }
            TextRenderer.DrawText(e.Graphics, TabPages[i].Text, Theme.UiFont, r, sel ? Theme.Text : Theme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
