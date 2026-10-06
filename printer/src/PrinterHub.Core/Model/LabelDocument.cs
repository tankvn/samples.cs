using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PrinterHub.Core.Model;

/// <summary>
/// Mô hình nhãn trung lập (không phụ thuộc hãng). Toạ độ và kích thước tính bằng DOT.
/// Mỗi ICommandLanguage (SBPL, ZPL, EZPL...) sẽ chuyển mô hình này thành byte lệnh.
/// </summary>
public sealed class LabelDocument
{
    /// <summary>Độ phân giải máy in (203, 300/305, 600 dpi).</summary>
    public int Dpi { get; set; } = 203;

    /// <summary>Chiều rộng nhãn (dot).</summary>
    public int Width { get; set; } = 800;

    /// <summary>Chiều cao nhãn (dot).</summary>
    public int Height { get; set; } = 400;

    /// <summary>Khoảng hở giữa các nhãn (dot) – dùng cho EZPL.</summary>
    public int Gap { get; set; } = 24;

    /// <summary>Số bản in.</summary>
    public int Copies { get; set; } = 1;

    /// <summary>Độ đậm (0-30 tuỳ hãng, null = mặc định của máy in).</summary>
    public int? Darkness { get; set; }

    /// <summary>Tên job (SBPL ESC WK, PJL JOB NAME...).</summary>
    public string? JobName { get; set; }

    public List<LabelElement> Elements { get; set; } = [];

    // ---------- Fluent helpers ----------
    public LabelDocument Text(int x, int y, string text, int height = 30, int width = 0, TextStyle style = TextStyle.Auto)
    { Elements.Add(new TextElement { X = x, Y = y, Text = text, FontHeight = height, FontWidth = width, Style = style }); return this; }

    public LabelDocument Barcode(int x, int y, BarcodeType type, string data, int height = 80, int moduleWidth = 2, bool humanReadable = true)
    { Elements.Add(new BarcodeElement { X = x, Y = y, Type = type, Data = data, Height = height, ModuleWidth = moduleWidth, HumanReadable = humanReadable }); return this; }

    public LabelDocument Qr(int x, int y, string data, int cellSize = 5, char errorCorrection = 'M')
    { Elements.Add(new QrElement { X = x, Y = y, Data = data, CellSize = cellSize, ErrorCorrection = errorCorrection }); return this; }

    public LabelDocument Box(int x, int y, int width, int height, int thickness = 2)
    { Elements.Add(new BoxElement { X = x, Y = y, Width = width, Height = height, Thickness = thickness }); return this; }

    public LabelDocument Line(int x, int y, int length, bool horizontal = true, int thickness = 2)
    { Elements.Add(new LineElement { X = x, Y = y, Length = length, Horizontal = horizontal, Thickness = thickness }); return this; }

    public LabelDocument Image(int x, int y, MonoImage image)
    { Elements.Add(new ImageElement { X = x, Y = y, Image = image }); return this; }

    /// <summary>Chuyển mm → dot theo DPI của nhãn.</summary>
    public int Mm(double mm) => (int)Math.Round(mm * Dpi / 25.4);

    /// <summary>Chuyển dot → mm.</summary>
    public double ToMm(int dots) => dots * 25.4 / Dpi;

    private static readonly Regex VarRegex = new(@"\{(?<name>[^{}]+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Tạo bản sao với các biến {Tên} được thay bằng giá trị (dùng khi in hàng loạt từ CSV).
    /// Biến không có giá trị được giữ nguyên.
    /// </summary>
    public LabelDocument Bind(IReadOnlyDictionary<string, string> values)
    {
        string Sub(string s) => VarRegex.Replace(s, m =>
            values.TryGetValue(m.Groups["name"].Value, out var v) ? v : m.Value);

        var copy = (LabelDocument)MemberwiseClone();
        copy.JobName = JobName is null ? null : Sub(JobName);
        copy.Elements = Elements.Select(e => e switch
        {
            TextElement t => t with { Text = Sub(t.Text) },
            BarcodeElement b => b with { Data = Sub(b.Data) },
            QrElement q => q with { Data = Sub(q.Data) },
            _ => e
        }).ToList();
        return copy;
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "element")]
[JsonDerivedType(typeof(TextElement), "text")]
[JsonDerivedType(typeof(BarcodeElement), "barcode")]
[JsonDerivedType(typeof(QrElement), "qr")]
[JsonDerivedType(typeof(BoxElement), "box")]
[JsonDerivedType(typeof(LineElement), "line")]
[JsonDerivedType(typeof(ImageElement), "image")]
public abstract record LabelElement
{
    public int X { get; init; }
    public int Y { get; init; }
    /// <summary>Góc xoay: 0, 90, 180, 270.</summary>
    public int Rotation { get; init; }
}

public enum TextStyle
{
    /// <summary>Tự chọn: ASCII → font máy in; có ký tự đa byte → font Kanji/Unicode.</summary>
    Auto,
    /// <summary>Font bitmap/scalable có sẵn của máy in (chỉ ASCII).</summary>
    Builtin,
    /// <summary>Font Kanji (SBPL ESC K…, mã hoá Shift-JIS).</summary>
    Kanji,
    /// <summary>Font Unicode/TTF đã tải vào máy in (UTF-8).</summary>
    Unicode,
}

public sealed record TextElement : LabelElement
{
    public string Text { get; init; } = "";
    /// <summary>Chiều cao chữ (dot).</summary>
    public int FontHeight { get; init; } = 30;
    /// <summary>Chiều rộng chữ (dot), 0 = bằng chiều cao.</summary>
    public int FontWidth { get; init; }
    public TextStyle Style { get; init; } = TextStyle.Auto;
    /// <summary>Tên font tuỳ hãng (vd ZPL "E:ARIALUNI.TTF", SBPL "XM", EZPL "C").</summary>
    public string? FontName { get; init; }
}

public enum BarcodeType { Code128, Code39, Ean13, Itf, Codabar }

public sealed record BarcodeElement : LabelElement
{
    public BarcodeType Type { get; init; } = BarcodeType.Code128;
    public string Data { get; init; } = "";
    public int Height { get; init; } = 80;
    /// <summary>Độ rộng vạch hẹp (dot).</summary>
    public int ModuleWidth { get; init; } = 2;
    public bool HumanReadable { get; init; } = true;
}

public sealed record QrElement : LabelElement
{
    public string Data { get; init; } = "";
    /// <summary>Kích thước 1 ô (dot), 1-32.</summary>
    public int CellSize { get; init; } = 5;
    /// <summary>L, M, Q, H.</summary>
    public char ErrorCorrection { get; init; } = 'M';
}

public sealed record BoxElement : LabelElement
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int Thickness { get; init; } = 2;
}

public sealed record LineElement : LabelElement
{
    public int Length { get; init; }
    public bool Horizontal { get; init; } = true;
    public int Thickness { get; init; } = 2;
}

public sealed record ImageElement : LabelElement
{
    public MonoImage Image { get; init; } = MonoImage.Empty;
}
