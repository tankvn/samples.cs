using System.Drawing.Printing;
using System.Text;
using PrinterHub.Core;
using PrinterHub.Core.Config;
using PrinterHub.Core.Model;

namespace PrinterHub.App.Gdi;

/// <summary>
/// In nhãn qua driver Windows bằng GDI (PrintDocument). Driver SATO / ZDesigner / Godex / Seagull sẽ tự
/// chuyển trang GDI thành lệnh máy in (raster). Chậm hơn lệnh gốc nhưng in được mọi font/ảnh.
/// </summary>
public static class GdiLabelPrinter
{
    public static void Print(string printerName, LabelDocument doc, IHubLog? log = null)
    {
        using var pd = new PrintDocument();
        pd.PrinterSettings.PrinterName = printerName;
        if (!pd.PrinterSettings.IsValid) throw new InvalidOperationException($"Không tìm thấy máy in '{printerName}'.");
        pd.DocumentName = doc.JobName ?? "PrinterHub Label";
        pd.PrinterSettings.Copies = (short)Math.Clamp(doc.Copies, 1, short.MaxValue);
        pd.OriginAtMargins = false;
        pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        // Khổ giấy tính bằng 1/100 inch
        int wHundredth = (int)Math.Round(doc.Width * 100.0 / doc.Dpi);
        int hHundredth = (int)Math.Round(doc.Height * 100.0 / doc.Dpi);
        pd.DefaultPageSettings.PaperSize = new PaperSize("PrinterHub", wHundredth, hHundredth);

        pd.PrintPage += (_, e) =>
        {
            var g = e.Graphics!;
            g.PageUnit = GraphicsUnit.Inch;
            // Bù lề vật lý không in được của máy in
            g.TranslateTransform(-e.PageSettings.HardMarginX / 100f, -e.PageSettings.HardMarginY / 100f);
            g.ScaleTransform(1f / doc.Dpi, 1f / doc.Dpi);
            GdiLabelRenderer.Render(g, doc);
            e.HasMorePages = false;
        };
        pd.Print();
        log?.Info($"GDI: đã gửi '{pd.DocumentName}' tới driver '{printerName}' ({doc.Copies} bản)");
    }
}

/// <summary>
/// Driver passthrough: gom byte lệnh rồi in thành một dòng chữ GDI được bao bởi chuỗi bắt đầu/kết thúc
/// (ZDesigner mặc định <c>${ … }$</c>). Driver nhận ra chuỗi này và gửi nguyên lệnh tới máy in.
/// Với driver SATO dùng tính năng "Command Font" tương tự — cấu hình trong Printing Preferences.
/// Route: <c>passthrough://ZDesigner ZT411-203dpi ZPL?prefix=${&amp;suffix=}$&amp;font=Arial</c>
/// </summary>
public sealed class PassthroughTransport(RouteConfig route, IHubLog log) : IPrinterTransport
{
    private readonly MemoryStream _buffer = new();

    public string Kind => "passthrough";
    public string Description => $"passthrough://{route.Queue}";
    public bool IsBidirectional => false;

    public Task OpenAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        _buffer.Write(data.Span);
        return Task.CompletedTask;
    }

    public Task<int> ReceiveAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken ct = default) => Task.FromResult(0);

    public Task EndJobAsync(CancellationToken ct = default)
    {
        if (_buffer.Length == 0) return Task.CompletedTask;
        string commands = Encoding.UTF8.GetString(_buffer.ToArray()).Replace("\r", "").Replace("\n", "");
        _buffer.SetLength(0);
        string text = route.Get("prefix", "${") + commands + route.Get("suffix", "}$");

        using var pd = new PrintDocument();
        pd.PrinterSettings.PrinterName = route.Queue;
        if (!pd.PrinterSettings.IsValid) throw new InvalidOperationException($"Không tìm thấy máy in '{route.Queue}'.");
        pd.DocumentName = route.Get("job", "PrinterHub passthrough");
        pd.PrintPage += (_, e) =>
        {
            using var font = new Font(route.Get("font", "Arial"), 6);
            e.Graphics!.DrawString(text, font, Brushes.Black, 0, 0);
            e.HasMorePages = false;
        };
        pd.Print();
        log.Info($"Passthrough: {commands.Length} ký tự lệnh → '{route.Queue}'");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _buffer.Dispose();
        return ValueTask.CompletedTask;
    }
}
