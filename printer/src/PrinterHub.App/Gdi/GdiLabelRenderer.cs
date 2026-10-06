using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using PrinterHub.Core.Model;
using PrinterHub.Core.Util;

namespace PrinterHub.App.Gdi;

/// <summary>
/// Vẽ LabelDocument bằng GDI+ — dùng cho (1) xem trước trên màn hình, (2) in qua driver Windows (PrintDocument).
/// Toạ độ đầu vào tính bằng dot; Graphics phải được scale sao cho 1 đơn vị world = 1 dot.
/// Code128 được vẽ thật; các loại mã vạch khác và QR vẽ dạng khung giữ chỗ (máy in nhãn tự vẽ khi dùng lệnh gốc).
/// </summary>
public static class GdiLabelRenderer
{
    public static string FontFamily { get; set; } = "Arial";
    public static string CjkFontFamily { get; set; } = "MS Gothic";

    /// <summary>Vẽ nhãn lên Graphics đã được scale theo dot.</summary>
    public static void Render(Graphics g, LabelDocument doc)
    {
        g.SmoothingMode = SmoothingMode.None;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        foreach (var e in doc.Elements)
        {
            var state = g.Save();
            g.TranslateTransform(e.X, e.Y);
            if (e.Rotation != 0) g.RotateTransform(e.Rotation);
            try
            {
                switch (e)
                {
                    case TextElement t: DrawText(g, t); break;
                    case BarcodeElement b: DrawBarcode(g, b); break;
                    case QrElement q: DrawQrPlaceholder(g, q); break;
                    case BoxElement box:
                        int th = Math.Max(1, box.Thickness);
                        g.FillRectangle(Brushes.Black, 0, 0, box.Width, th);
                        g.FillRectangle(Brushes.Black, 0, box.Height - th, box.Width, th);
                        g.FillRectangle(Brushes.Black, 0, 0, th, box.Height);
                        g.FillRectangle(Brushes.Black, box.Width - th, 0, th, box.Height);
                        break;
                    case LineElement l:
                        if (l.Horizontal) g.FillRectangle(Brushes.Black, 0, 0, l.Length, Math.Max(1, l.Thickness));
                        else g.FillRectangle(Brushes.Black, 0, 0, Math.Max(1, l.Thickness), l.Length);
                        break;
                    case ImageElement img when img.Image.Width > 0:
                        using (var bmp = ToBitmap(img.Image)) g.DrawImage(bmp, 0, 0, img.Image.Width, img.Image.Height);
                        break;
                }
            }
            finally { g.Restore(state); }
        }
    }

    private static void DrawText(Graphics g, TextElement t)
    {
        string family = t.FontName is { } fn && !fn.Contains(':') && fn.Length > 2 ? fn
            : TextEncodings.IsAscii(t.Text) ? FontFamily : CjkFontFamily;
        // Kích thước font tính theo world unit (= dot); em-height ≈ 1.15 × chiều cao chữ hoa
        using var font = new Font(family, Math.Max(1, t.FontHeight), FontStyle.Regular, GraphicsUnit.World);
        using var fmt = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap };
        if (t.FontWidth > 0 && t.FontHeight > 0 && t.FontWidth != t.FontHeight)
            g.ScaleTransform((float)t.FontWidth / t.FontHeight, 1f);
        g.DrawString(t.Text, font, Brushes.Black, 0, 0, fmt);
    }

    private static void DrawBarcode(Graphics g, BarcodeElement b)
    {
        int m = Math.Max(1, b.ModuleWidth);
        if (b.Type == BarcodeType.Code128)
        {
            try
            {
                int x = 0;
                bool bar = true;
                foreach (int w in Code128.EncodeWidths(b.Data))
                {
                    if (bar) g.FillRectangle(Brushes.Black, x, 0, w * m, b.Height);
                    x += w * m;
                    bar = !bar;
                }
                if (b.HumanReadable) DrawCaption(g, b.Data, x, b.Height);
                return;
            }
            catch (ArgumentException) { /* ký tự ngoài Code128-B → vẽ giữ chỗ */ }
        }
        int width = Math.Max(60, b.Data.Length * 11 * m);
        using var pen = new Pen(Color.Black, 2) { DashStyle = DashStyle.Dash };
        g.DrawRectangle(pen, 0, 0, width, b.Height);
        DrawCaption(g, $"[{b.Type}] {b.Data}", width, b.Height / 2 - 10);
    }

    private static void DrawCaption(Graphics g, string text, int width, int y)
    {
        using var font = new Font(FontFamily, 18, FontStyle.Regular, GraphicsUnit.World);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawString(text, font, Brushes.Black, new RectangleF(0, y + 2, Math.Max(width, 1), 24), fmt);
    }

    private static void DrawQrPlaceholder(Graphics g, QrElement q)
    {
        int size = 25 * Math.Max(1, q.CellSize); // QR version 2 ≈ 25 module
        g.FillRectangle(Brushes.White, 0, 0, size, size);
        using var pen = new Pen(Color.Black, Math.Max(1, q.CellSize));
        g.DrawRectangle(pen, 0, 0, size, size);
        // 3 ô định vị giống QR
        int f = 7 * q.CellSize;
        foreach (var (x, y) in new[] { (0, 0), (size - f, 0), (0, size - f) })
        {
            g.FillRectangle(Brushes.Black, x, y, f, f);
            g.FillRectangle(Brushes.White, x + q.CellSize, y + q.CellSize, f - 2 * q.CellSize, f - 2 * q.CellSize);
            g.FillRectangle(Brushes.Black, x + 2 * q.CellSize, y + 2 * q.CellSize, f - 4 * q.CellSize, f - 4 * q.CellSize);
        }
        using var font = new Font(FontFamily, Math.Max(8, size / 8f), FontStyle.Bold, GraphicsUnit.World);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("QR", font, Brushes.Black, new RectangleF(0, 0, size, size), fmt);
    }

    public static Bitmap ToBitmap(MonoImage img)
    {
        var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
                bmp.SetPixel(x, y, img[x, y] ? Color.Black : Color.White);
        return bmp;
    }

    /// <summary>Chuyển ảnh bất kỳ (logo PNG/BMP) thành MonoImage theo ngưỡng độ sáng.</summary>
    public static MonoImage FromImageFile(string path, int maxWidthDots = 0, int threshold = 128)
    {
        using var src = Image.FromFile(path);
        int w = src.Width, h = src.Height;
        if (maxWidthDots > 0 && w > maxWidthDots) { h = h * maxWidthDots / w; w = maxWidthDots; }
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, w, h);
        }
        return MonoImage.FromLuminance(w, h, (x, y) =>
        {
            var c = bmp.GetPixel(x, y);
            return c.A < 128 ? 255 : (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
        }, threshold);
    }

    /// <summary>Tạo bitmap xem trước (nền trắng, viền xám) với tỉ lệ <paramref name="scale"/> pixel/dot.</summary>
    public static Bitmap Preview(LabelDocument doc, float scale)
    {
        int w = Math.Max(1, (int)(doc.Width * scale)), h = Math.Max(1, (int)(doc.Height * scale));
        var bmp = new Bitmap(w + 2, h + 2);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(60, 60, 70));
        g.FillRectangle(Brushes.White, 1, 1, w, h);
        g.TranslateTransform(1, 1);
        g.ScaleTransform(scale, scale);
        Render(g, doc);
        return bmp;
    }
}
