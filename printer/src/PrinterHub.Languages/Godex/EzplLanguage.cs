using System.Globalization;
using System.Text;
using PrinterHub.Core.Model;
using PrinterHub.Core.Util;

namespace PrinterHub.Languages.Godex;

/// <summary>
/// Godex EZPL — ngôn ngữ gốc của Godex (G500, RT700i, EZ2250i, ZX1200i, HD830i, MX...).
/// Mỗi lệnh kết thúc bằng CR LF. Cấu trúc: thiết lập (^Q ^W ^H ^P…) → ^L → phần tử → E.
///
/// Tuỳ chọn:
/// <list type="bullet">
/// <item><c>font</c>: font nội A–H (mặc định tự chọn theo chiều cao).</item>
/// <item><c>ttfCommand</c>: tiền tố lệnh cho văn bản Unicode (mặc định "AT"). ⚠ Kiểm tra theo firmware.</item>
/// <item><c>speed</c>: tốc độ in ^S (ips).</item>
/// <item><c>enableImmediateStatus</c>: true → phát ^XSET,IMMEDIATE,1 để ~S,CHECK hoạt động.</item>
/// </list>
/// ⚠ Cú pháp QR (W) và ảnh (GW) dựa theo EZPL manual — cần kiểm tra trên máy thật.
/// </summary>
public sealed class EzplLanguage : LanguageBase
{
    public EzplLanguage(IReadOnlyDictionary<string, string>? options = null) : base(options)
    {
        TextEncoding = TextEncodings.Get(Opt("encoding", "utf-8"));
    }

    public override string Name => "EZPL";

    private static string Mm(LabelDocument doc, int dots) =>
        Math.Max(1, (int)Math.Round(doc.ToMm(dots))).ToString(CultureInfo.InvariantCulture);

    private static int Rot(int deg) => ((deg % 360 + 360) % 360) switch { 90 => 1, 180 => 2, 270 => 3, _ => 0 };

    // Chiều cao xấp xỉ (dot @203dpi) của font nội EZPL A..H
    private static readonly (char font, int height)[] Fonts =
        [('A', 9), ('B', 11), ('C', 15), ('D', 20), ('E', 26), ('F', 34), ('G', 48), ('H', 64)];

    public override byte[] Render(LabelDocument doc)
    {
        var w = new CommandWriter(TextEncoding);
        if (OptBool("enableImmediateStatus", false)) w.Line("^XSET,IMMEDIATE,1");
        w.Line($"^Q{Mm(doc, doc.Height)},{Mm(doc, doc.Gap)}");
        w.Line($"^W{Mm(doc, doc.Width)}");
        w.Line($"^H{I(Math.Clamp(doc.Darkness ?? 10, 0, 19))}");
        w.Line($"^P{I(Math.Max(1, doc.Copies))}");
        if (OptOrNull("speed") is { } sp) w.Line($"^S{sp}");
        w.Line("^AD");
        w.Line("^C1");
        w.Line("^R0");
        w.Line("~Q+0");
        w.Line("^O0");
        w.Line("^D0");
        w.Line("^L");

        foreach (var e in doc.Elements)
        {
            int r = Rot(e.Rotation);
            switch (e)
            {
                case TextElement t:
                {
                    bool ascii = TextEncodings.IsAscii(t.Text) && t.Style != TextStyle.Unicode && t.Style != TextStyle.Kanji;
                    if (ascii)
                    {
                        char font = t.FontName is { Length: 1 } f ? f[0] : PickFont(t.FontHeight, doc.Dpi, out _);
                        int baseH = Fonts.First(x => x.font == font).height * doc.Dpi / 203;
                        int my = Math.Clamp((int)Math.Round((double)t.FontHeight / Math.Max(1, baseH)), 1, 8);
                        int mx = t.FontWidth > 0 ? Math.Clamp((int)Math.Round((double)t.FontWidth / Math.Max(1, baseH)), 1, 8) : my;
                        w.Ascii($"A{font},{I(t.X)},{I(t.Y)},{I(mx)},{I(my)},0,{I(r)},").Text(t.Text).Line();
                    }
                    else
                    {
                        // Văn bản Unicode dùng font TrueType: AT,x,y,height,width,gap,dark,rotation,0,text
                        int h = t.FontHeight, wd = t.FontWidth > 0 ? t.FontWidth : t.FontHeight;
                        w.Ascii($"{Opt("ttfCommand", "AT")},{I(t.X)},{I(t.Y)},{I(h)},{I(wd)},0,0,{I(r)},0,").Text(t.Text).Line();
                    }
                    break;
                }
                case BarcodeElement b:
                {
                    string cmd = b.Type switch
                    {
                        BarcodeType.Code128 => "BQ",
                        BarcodeType.Code39 => "BA",
                        BarcodeType.Ean13 => "BE",
                        _ => throw new NotSupportedException($"EZPL: chưa hỗ trợ barcode {b.Type}"),
                    };
                    int narrow = Math.Clamp(b.ModuleWidth, 1, 10);
                    w.Ascii($"{cmd},{I(b.X)},{I(b.Y)},{I(narrow)},{I(narrow * 3)},{I(b.Height)},{I(r)},{(b.HumanReadable ? 1 : 0)},")
                     .Ascii(b.Data).Line();
                    break;
                }
                case QrElement q:
                {
                    byte[] data = TextEncoding.GetBytes(q.Data);
                    char ec = "LMQH".Contains(char.ToUpperInvariant(q.ErrorCorrection)) ? char.ToUpperInvariant(q.ErrorCorrection) : 'M';
                    // W x,y,mode,type,ec,mask,mul,len,rotate  → dòng tiếp theo là dữ liệu
                    w.Line($"W{I(q.X)},{I(q.Y)},2,1,{ec},8,{I(Math.Clamp(q.CellSize, 1, 40))},{I(data.Length)},{I(r)}");
                    w.Bytes(data).Line();
                    break;
                }
                case BoxElement box:
                    w.Line($"R{I(box.X)},{I(box.Y)},{I(box.X + box.Width)},{I(box.Y + box.Height)},{I(box.Thickness)},{I(box.Thickness)}");
                    break;
                case LineElement l:
                    w.Line(l.Horizontal
                        ? $"Lo,{I(l.X)},{I(l.Y)},{I(l.X + l.Length)},{I(l.Y + l.Thickness)}"
                        : $"Lo,{I(l.X)},{I(l.Y)},{I(l.X + l.Thickness)},{I(l.Y + l.Length)}");
                    break;
                case ImageElement img when img.Image.Width > 0:
                    // GW x,y,bytesPerRow,height + dữ liệu nhị phân (EZPL: bit 0 = đen → đảo bit)
                    w.Ascii($"GW{I(img.X)},{I(img.Y)},{I(img.Image.BytesPerRow)},{I(img.Image.Height)},");
                    w.Bytes(OptBool("imageInvert", true) ? img.Image.Data.Select(x => (byte)~x).ToArray() : img.Image.Data);
                    w.Line();
                    break;
            }
        }
        w.Line("E");
        return w.ToArray();
    }

    private static char PickFont(int height, int dpi, out int baseHeight)
    {
        var best = Fonts.OrderBy(f => Math.Abs(f.height * dpi / 203 - height)).First();
        baseHeight = best.height;
        return best.font;
    }

    // =================== Trạng thái ~S,CHECK ===================

    public override byte[]? BuildStatusRequest() => Encoding.ASCII.GetBytes("~S,CHECK\r\n");

    public override bool TryParseStatus(ReadOnlySpan<byte> response, out PrinterStatus status)
        => GodexStatus.TryParse(response, out status);
}

public static class GodexStatus
{
    public static bool TryParse(ReadOnlySpan<byte> response, out PrinterStatus status)
    {
        status = PrinterStatus.Unknown();
        string s = Encoding.ASCII.GetString(response);
        int nl = s.IndexOfAny(['\r', '\n']);
        if (nl < 0) return false;
        string code = s[..nl].Trim();
        if (code.Length < 2 || !int.TryParse(code, out _)) return false;

        status = Decode(code) with { RawCode = code, RawResponse = response.ToArray() };
        return true;
    }

    public static PrinterStatus Decode(string code) => code switch
    {
        "00" => new PrinterStatus { State = PrinterState.Ready, Message = "Ready" },
        "01" => Err(PrinterErrors.PaperOut, "Hết giấy / không tìm thấy giấy"),
        "02" => Err(PrinterErrors.PaperJam, "Kẹt giấy"),
        "03" => Err(PrinterErrors.RibbonOut, "Hết ribbon"),
        "04" => Err(PrinterErrors.HeadOpen, "Mở đầu in"),
        "05" => Err(PrinterErrors.Other, "Bộ cuộn đầy"),
        "06" => Err(PrinterErrors.MemoryFull, "Bộ nhớ đầy"),
        "07" => Err(PrinterErrors.Other, "Lỗi lưu file"),
        "08" => Err(PrinterErrors.Other, "Trùng tên file"),
        "09" => Err(PrinterErrors.SyntaxError, "Lỗi cú pháp lệnh"),
        "10" => Err(PrinterErrors.CutterError, "Kẹt dao cắt"),
        "11" => Err(PrinterErrors.Other, "Thiếu bộ nhớ mở rộng"),
        "20" => new PrinterStatus { State = PrinterState.Offline, Warnings = PrinterWarnings.Paused, Message = "Pause" },
        "21" => new PrinterStatus { State = PrinterState.Busy, Message = "Đang cài đặt" },
        "22" => new PrinterStatus { State = PrinterState.Busy, Message = "Chế độ bàn phím" },
        "50" => new PrinterStatus { State = PrinterState.Printing, Message = "Đang in" },
        "60" => new PrinterStatus { State = PrinterState.Busy, Message = "Đang xử lý dữ liệu" },
        _ => new PrinterStatus { State = PrinterState.Unknown, Message = $"Mã {code}" },
    };

    private static PrinterStatus Err(PrinterErrors e, string m) => new() { State = PrinterState.Error, Errors = e, Message = m };
}
