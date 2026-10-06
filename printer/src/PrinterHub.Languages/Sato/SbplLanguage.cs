using System.Text;
using PrinterHub.Core.Model;
using PrinterHub.Core.Util;

namespace PrinterHub.Languages.Sato;

/// <summary>
/// SATO Barcode Printer Language (SBPL) — ngôn ngữ gốc của máy in SATO
/// (CL4NX Plus, CL6NX Plus, CT4-LX, FX3-LX, WS, CG, M-84Pro...).
///
/// Khung job: <c>STX ESC A … ESC Z ETX</c>. Trong tài liệu SATO, <c>&lt;A&gt;</c> nghĩa là <c>ESC A</c>.
///
/// Tuỳ chọn (PrinterProfile.LanguageOptions):
/// <list type="bullet">
/// <item><c>textFont</c>: font bitmap cho ASCII (mặc định "XM" 24x24). Khác: XS, XL, XU, XB, OA, OB.</item>
/// <item><c>textMode</c>: "bitmap" (mặc định) hoặc "scalable" (ESC $ … ESC $=).</item>
/// <item><c>kanjiFont</c>: lệnh Kanji (mặc định "K9B" = Kanji 24x24, dữ liệu 2 byte). Cần kiểm tra theo model.</item>
/// <item><c>kanjiCode</c>: lệnh chọn mã Kanji phát ra sau ESC A (vd "KC1"). Mặc định không phát — dùng cài đặt máy in.</item>
/// <item><c>labelSize</c>: true (mặc định) → phát ESC A1 VVVVHHHH.</item>
/// <item><c>jobId</c>: 2 chữ số → phát ESC ID (máy in trả lại trong Status4).</item>
/// <item><c>nonStandardCodes</c>: true → thay STX/ETX/ESC bằng { } ^ (khi máy in đặt "Non-standard protocol").</item>
/// </list>
/// ⚠ Các lệnh barcode/QR/Kanji dựa theo CL4NX Plus Programming Reference — cần thử trên máy thật.
/// </summary>
public sealed class SbplLanguage : LanguageBase
{
    public SbplLanguage(IReadOnlyDictionary<string, string>? options = null) : base(options)
    {
        // Máy SATO thị trường Nhật thường dùng Shift-JIS cho Kanji
        TextEncoding = TextEncodings.Get(Opt("encoding", "shift_jis"));
    }

    public override string Name => "SBPL";

    private bool NonStandard => OptBool("nonStandardCodes", false);

    private byte Map(byte control) => !NonStandard ? control : control switch
    {
        ControlChars.STX => (byte)'{',
        ControlChars.ETX => (byte)'}',
        ControlChars.ESC => (byte)'^',
        _ => control,
    };

    /// <summary>Ghi ESC + lệnh (vd Cmd(w, "H0100")).</summary>
    private void Cmd(CommandWriter w, string command) => w.Byte(Map(ControlChars.ESC)).Ascii(command);

    public override byte[] Render(LabelDocument doc)
    {
        var w = new CommandWriter(TextEncoding);
        w.Byte(Map(ControlChars.STX));
        Cmd(w, "A");

        if (OptOrNull("kanjiCode") is { } kc) Cmd(w, kc);
        if (OptBool("labelSize", true)) Cmd(w, $"A1{D(doc.Height, 4)}{D(doc.Width, 4)}");
        if (OptOrNull("jobId") is { } id) Cmd(w, $"ID{id}");
        if (!string.IsNullOrEmpty(doc.JobName)) { Cmd(w, "WK"); w.Text(Truncate(doc.JobName, 16)); }
        if (doc.Darkness is { } dk) Cmd(w, $"#E{Math.Clamp(dk, 1, 5)}");

        foreach (var e in doc.Elements)
        {
            Cmd(w, $"%{RotationCode(e.Rotation)}");
            switch (e)
            {
                case TextElement t: WriteText(w, t); break;
                case BarcodeElement b: WriteBarcode(w, b); break;
                case QrElement q: WriteQr(w, q); break;
                case BoxElement box: WriteBox(w, box); break;
                case LineElement l: WriteLine(w, l); break;
                case ImageElement img: WriteImage(w, img); break;
            }
        }
        Cmd(w, "%0");
        Cmd(w, $"Q{Math.Clamp(doc.Copies, 1, 999999)}");
        Cmd(w, "Z");
        w.Byte(Map(ControlChars.ETX));
        return w.ToArray();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static int RotationCode(int deg) => ((deg % 360 + 360) % 360) switch
    {
        90 => 1, 180 => 2, 270 => 3, _ => 0,
    };

    private void Position(CommandWriter w, int x, int y)
    {
        Cmd(w, $"V{D(y, 4)}");
        Cmd(w, $"H{D(x, 4)}");
    }

    // Kích thước cơ bản (dot) của font bitmap SBPL để tính hệ số phóng to ESC L
    private static int FontBaseHeight(string font) => font switch
    {
        "XU" => 9, "XS" => 17, "XM" => 24, "XB" => 30, "XL" => 48, "OA" => 22, "OB" => 30, _ => 24,
    };

    private void WriteText(CommandWriter w, TextElement t)
    {
        var style = t.Style;
        if (style == TextStyle.Auto) style = TextEncodings.IsAscii(t.Text) ? TextStyle.Builtin : TextStyle.Kanji;

        Position(w, t.X, t.Y);
        switch (style)
        {
            case TextStyle.Kanji:
            {
                // ESC K9B + dữ liệu 2 byte (Shift-JIS/JIS tuỳ cài đặt ESC KC). Font Kanji 24x24.
                int baseH = 24;
                int my = Math.Clamp((int)Math.Round((double)t.FontHeight / baseH), 1, 36);
                int mx = t.FontWidth > 0 ? Math.Clamp((int)Math.Round((double)t.FontWidth / baseH), 1, 36) : my;
                Cmd(w, $"L{D(mx, 2)}{D(my, 2)}");
                Cmd(w, t.FontName ?? Opt("kanjiFont", "K9B"));
                w.Text(t.Text);
                break;
            }
            case TextStyle.Unicode:
            {
                // Font scalable/CG: ESC $ A,width,height,style  ESC $= data
                int h = Math.Clamp(t.FontHeight, 4, 999);
                int wd = Math.Clamp(t.FontWidth > 0 ? t.FontWidth : t.FontHeight, 4, 999);
                Cmd(w, $"${t.FontName ?? "A"},{D(wd, 3)},{D(h, 3)},0");
                Cmd(w, "$=");
                w.Text(t.Text);
                break;
            }
            default:
            {
                if (Opt("textMode", "bitmap") == "scalable")
                {
                    int h = Math.Clamp(t.FontHeight, 4, 999);
                    int wd = Math.Clamp(t.FontWidth > 0 ? t.FontWidth : t.FontHeight, 4, 999);
                    Cmd(w, $"$A,{D(wd, 3)},{D(h, 3)},0");
                    Cmd(w, "$=");
                    w.Ascii(t.Text);
                    break;
                }
                string font = t.FontName ?? Opt("textFont", "XM");
                int baseH = FontBaseHeight(font);
                int my = Math.Clamp((int)Math.Round((double)t.FontHeight / baseH), 1, 36);
                int mx = t.FontWidth > 0 ? Math.Clamp((int)Math.Round((double)t.FontWidth / baseH), 1, 36) : my;
                if (mx != 1 || my != 1) Cmd(w, $"L{D(mx, 2)}{D(my, 2)}");
                Cmd(w, font);
                w.Text(t.Text);
                break;
            }
        }
    }

    private void WriteBarcode(CommandWriter w, BarcodeElement b)
    {
        Position(w, b.X, b.Y);
        string nw = D(Math.Clamp(b.ModuleWidth, 1, 12), 2);
        string h = D(Math.Clamp(b.Height, 1, 999), 3);
        switch (b.Type)
        {
            case BarcodeType.Code128:
                // ESC BG aa bbb + mã start (>H = START B)
                Cmd(w, $"BG{nw}{h}>H");
                w.Ascii(b.Data);
                break;
            case BarcodeType.Code39:
                Cmd(w, $"B1{nw}{h}");
                w.Ascii("*" + b.Data.Trim('*') + "*");
                break;
            case BarcodeType.Ean13:
                Cmd(w, $"B3{nw}{h}");
                w.Ascii(b.Data);
                break;
            case BarcodeType.Itf:
                Cmd(w, $"B2{nw}{h}");
                w.Ascii(b.Data);
                break;
            case BarcodeType.Codabar:
                Cmd(w, $"B0{nw}{h}");
                w.Ascii(b.Data);
                break;
        }
        if (b.HumanReadable)
        {
            // Dòng chữ dưới mã vạch bằng font XS
            Position(w, b.X, b.Y + b.Height + 4);
            Cmd(w, "XS");
            w.Ascii(b.Data);
        }
    }

    private void WriteQr(CommandWriter w, QrElement q)
    {
        // ESC 2D30,ec,cell,mode(1=auto),0  ESC DN nnnn,data
        Position(w, q.X, q.Y);
        char ec = "LMQH".Contains(char.ToUpperInvariant(q.ErrorCorrection)) ? char.ToUpperInvariant(q.ErrorCorrection) : 'M';
        Cmd(w, $"2D30,{ec},{D(Math.Clamp(q.CellSize, 1, 32), 2)},1,0");
        byte[] data = TextEncoding.GetBytes(q.Data);
        Cmd(w, $"DN{D(data.Length, 4)},");
        w.Bytes(data);
    }

    private void WriteBox(CommandWriter w, BoxElement b)
    {
        // ESC FW aa bb V cccc H dddd: aa=độ dày cạnh ngang, bb=độ dày cạnh dọc
        Position(w, b.X, b.Y);
        string t = D(Math.Clamp(b.Thickness, 1, 99), 2);
        Cmd(w, $"FW{t}{t}V{D(b.Height, 4)}H{D(b.Width, 4)}");
    }

    private void WriteLine(CommandWriter w, LineElement l)
    {
        Position(w, l.X, l.Y);
        Cmd(w, $"FW{D(Math.Clamp(l.Thickness, 1, 99), 2)}{(l.Horizontal ? 'H' : 'V')}{D(l.Length, 4)}");
    }

    private void WriteImage(CommandWriter w, ImageElement e)
    {
        // ESC GH aaa bbb + hex; aaa = số khối 8 dot ngang, bbb = số khối 8 dot dọc
        var img = e.Image.PadHeight(8);
        if (img.Width == 0) return;
        // Chiều ngang cũng phải là bội số 8 → BytesPerRow đã pad
        Position(w, e.X, e.Y);
        Cmd(w, $"GH{D(img.BytesPerRow, 3)}{D(img.Height / 8, 3)}");
        w.Ascii(img.ToHex());
    }

    // =================== Trạng thái (Status3/4/5) ===================

    public override byte[]? BuildStatusRequest() => [ControlChars.ENQ];

    /// <summary>
    /// Phân tích phản hồi STX … ETX. Phần nội dung (25 byte cuối) = ID(2) + trạng thái(1) + số nhãn còn lại(6) + tên job(16).
    /// Status3 / phản hồi ngắn: 9 byte cuối = ID(2) + trạng thái(1) + còn lại(6).
    /// </summary>
    public override bool TryParseStatus(ReadOnlySpan<byte> response, out PrinterStatus status)
        => SatoStatus.TryParse(response, out status);
}

/// <summary>Bảng mã trạng thái SATO (Status4). ⚠ Cần đối chiếu "Return Status" trong Programming Reference.</summary>
public static class SatoStatus
{
    public static bool TryParse(ReadOnlySpan<byte> response, out PrinterStatus status)
    {
        status = PrinterStatus.Unknown();
        int etx = response.LastIndexOf(ControlChars.ETX);
        if (etx < 0) return false;
        int stx = response[..etx].LastIndexOf(ControlChars.STX);
        if (stx < 0) return false;
        var body = response[(stx + 1)..etx];

        string? jobName = null;
        ReadOnlySpan<byte> core;
        if (body.Length >= 25)
        {
            var last = body[^25..];
            core = last[..9];
            jobName = Encoding.ASCII.GetString(last[9..]).TrimEnd(' ', '\0');
        }
        else if (body.Length >= 9) core = body[^9..];
        else if (body.Length >= 3) core = body[^3..]; // ID + status (không có số nhãn)
        else return false;

        string id = Encoding.ASCII.GetString(core[..2]);
        char code = (char)core[2];
        int? remaining = null;
        if (core.Length >= 9 && int.TryParse(Encoding.ASCII.GetString(core[3..9]), out int r)) remaining = r;

        status = Decode(code) with
        {
            JobId = id,
            JobName = string.IsNullOrEmpty(jobName) ? null : jobName,
            RemainingLabels = remaining,
            RawCode = code.ToString(),
            RawResponse = response.ToArray(),
        };
        return true;
    }

    public static PrinterStatus Decode(char code)
    {
        // Nhóm 4 ký tự: +0 bình thường, +1 ribbon sắp hết, +2 buffer sắp đầy, +3 cả hai
        static PrinterWarnings W(int offset) => offset switch
        {
            1 => PrinterWarnings.RibbonNearEnd,
            2 => PrinterWarnings.BufferNearFull,
            3 => PrinterWarnings.RibbonNearEnd | PrinterWarnings.BufferNearFull,
            _ => PrinterWarnings.None,
        };

        return code switch
        {
            >= '0' and <= '3' => new PrinterStatus { State = PrinterState.Offline, Warnings = W(code - '0'), Message = "Offline" },
            >= 'A' and <= 'D' => new PrinterStatus { State = PrinterState.Ready, Warnings = W(code - 'A'), Message = "Online - chờ dữ liệu" },
            >= 'G' and <= 'J' => new PrinterStatus { State = PrinterState.Printing, Warnings = W(code - 'G'), Message = "Đang in" },
            >= 'M' and <= 'P' => new PrinterStatus { State = PrinterState.WaitingForTakeOut, Warnings = W(code - 'M'), Message = "Chờ lấy nhãn" },
            >= 'S' and <= 'V' => new PrinterStatus { State = PrinterState.Busy, Warnings = W(code - 'S'), Message = "Đang phân tích dữ liệu" },
            'a' => Err(PrinterErrors.BufferOverflow, "Tràn bộ đệm nhận"),
            'b' => Err(PrinterErrors.HeadOpen, "Mở đầu in"),
            'c' => Err(PrinterErrors.PaperOut, "Hết giấy"),
            'd' => Err(PrinterErrors.RibbonOut, "Hết ribbon"),
            'e' => Err(PrinterErrors.MediaError, "Lỗi giấy"),
            'f' => Err(PrinterErrors.SensorError, "Lỗi cảm biến"),
            'g' => Err(PrinterErrors.HeadError, "Lỗi đầu in"),
            'h' => Err(PrinterErrors.CoverOpen, "Mở nắp"),
            'i' => Err(PrinterErrors.Other, "Lỗi thẻ nhớ/thẻ"),
            'j' => Err(PrinterErrors.CutterError, "Lỗi dao cắt"),
            'k' => Err(PrinterErrors.Other, "Lỗi khác"),
            _ => new PrinterStatus { State = PrinterState.Unknown, Message = $"Mã không xác định '{code}'" },
        };

        static PrinterStatus Err(PrinterErrors e, string msg) =>
            new() { State = PrinterState.Error, Errors = e, Message = msg };
    }

    /// <summary>Tạo phản hồi Status4 (dùng cho Simulator/test).</summary>
    public static byte[] BuildResponse(string jobId, char code, int remaining, string jobName = "")
    {
        var s = $"{jobId.PadLeft(2, '0')[..2]}{code}{Math.Clamp(remaining, 0, 999999):D6}{jobName.PadRight(16)[..16]}";
        return [ControlChars.STX, .. Encoding.ASCII.GetBytes(s), ControlChars.ETX];
    }
}
