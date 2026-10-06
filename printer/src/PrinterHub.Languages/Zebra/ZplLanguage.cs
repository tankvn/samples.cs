using System.Text;
using PrinterHub.Core.Model;
using PrinterHub.Core.Util;

namespace PrinterHub.Languages.Zebra;

/// <summary>
/// Zebra Programming Language II (ZPL) — Zebra ZT/ZD/ZQ, và emulation SZPL (SATO), GZPL (Godex).
///
/// Tuỳ chọn:
/// <list type="bullet">
/// <item><c>unicodeFont</c>: font TTF trong máy in cho văn bản không phải ASCII, vd "E:ANMDJ.TTF" (^A@).</item>
/// <item><c>font</c>: font mặc định cho ASCII (mặc định "0" = ^A0 scalable).</item>
/// <item><c>statusCommand</c>: "~HS" (mặc định).</item>
/// </list>
/// Encoding: UTF-8 → ^CI28, Shift-JIS → ^CI15.
/// </summary>
public sealed class ZplLanguage : LanguageBase
{
    public ZplLanguage(IReadOnlyDictionary<string, string>? options = null) : base(options)
    {
        TextEncoding = TextEncodings.Get(Opt("encoding", "utf-8"));
    }

    public override string Name => "ZPL";

    private string CharsetCommand => TextEncoding.CodePage switch
    {
        65001 => "^CI28",
        932 => "^CI15",
        _ => "^CI28",
    };

    private static char Orient(int rotation) => ((rotation % 360 + 360) % 360) switch
    {
        90 => 'R', 180 => 'I', 270 => 'B', _ => 'N',
    };

    public override byte[] Render(LabelDocument doc)
    {
        var w = new CommandWriter(TextEncoding);
        if (doc.Darkness is { } dk) w.Line($"~SD{D(Math.Clamp(dk, 0, 30), 2)}");
        w.Line("^XA");
        w.Line(CharsetCommand);
        w.Line($"^PW{I(doc.Width)}");
        w.Line($"^LL{I(doc.Height)}");
        w.Line("^LH0,0");
        if (!string.IsNullOrEmpty(doc.JobName)) w.Ascii("^FX ").Text(Escape(doc.JobName, out _)).Line();

        foreach (var e in doc.Elements)
        {
            char o = Orient(e.Rotation);
            switch (e)
            {
                case TextElement t:
                {
                    int h = Math.Max(1, t.FontHeight);
                    int wd = t.FontWidth > 0 ? t.FontWidth : h;
                    string fontCmd;
                    if (t.FontName is { } fn) fontCmd = fn.Contains(':') ? $"^A@{o},{h},{wd},{fn}" : $"^A{fn}{o},{h},{wd}";
                    else if (!TextEncodings.IsAscii(t.Text) && OptOrNull("unicodeFont") is { } uf) fontCmd = $"^A@{o},{h},{wd},{uf}";
                    else fontCmd = $"^A{Opt("font", "0")}{o},{h},{wd}";
                    w.Ascii($"^FO{I(t.X)},{I(t.Y)}{fontCmd}");
                    WriteField(w, t.Text);
                    break;
                }
                case BarcodeElement b:
                {
                    char hr = b.HumanReadable ? 'Y' : 'N';
                    w.Ascii($"^FO{I(b.X)},{I(b.Y)}^BY{I(Math.Clamp(b.ModuleWidth, 1, 10))},3,{I(b.Height)}");
                    w.Ascii(b.Type switch
                    {
                        BarcodeType.Code128 => $"^BC{o},{I(b.Height)},{hr},N,N",
                        BarcodeType.Code39 => $"^B3{o},N,{I(b.Height)},{hr},N",
                        BarcodeType.Ean13 => $"^BE{o},{I(b.Height)},{hr},N",
                        BarcodeType.Itf => $"^B2{o},{I(b.Height)},{hr},N,N",
                        BarcodeType.Codabar => $"^BK{o},N,{I(b.Height)},{hr},N,A,A",
                        _ => throw new NotSupportedException(b.Type.ToString()),
                    });
                    WriteField(w, b.Data);
                    break;
                }
                case QrElement q:
                {
                    char ec = "LMQH".Contains(char.ToUpperInvariant(q.ErrorCorrection)) ? char.ToUpperInvariant(q.ErrorCorrection) : 'M';
                    w.Ascii($"^FO{I(q.X)},{I(q.Y)}^BQ{o},2,{I(Math.Clamp(q.CellSize, 1, 10))}");
                    // ^FD<EC><mode>,data — A = tự động chọn chế độ
                    WriteField(w, q.Data, $"{ec}A,");
                    break;
                }
                case BoxElement box:
                    w.Line($"^FO{I(box.X)},{I(box.Y)}^GB{I(box.Width)},{I(box.Height)},{I(box.Thickness)}^FS");
                    break;
                case LineElement l:
                    w.Line(l.Horizontal
                        ? $"^FO{I(l.X)},{I(l.Y)}^GB{I(l.Length)},{I(l.Thickness)},{I(l.Thickness)}^FS"
                        : $"^FO{I(l.X)},{I(l.Y)}^GB{I(l.Thickness)},{I(l.Length)},{I(l.Thickness)}^FS");
                    break;
                case ImageElement img when img.Image.Width > 0:
                {
                    int total = img.Image.Data.Length;
                    w.Line($"^FO{I(img.X)},{I(img.Y)}^GFA,{I(total)},{I(total)},{I(img.Image.BytesPerRow)},{img.Image.ToHex()}^FS");
                    break;
                }
            }
        }
        w.Line($"^PQ{I(Math.Max(1, doc.Copies))}");
        w.Line("^XZ");
        return w.ToArray();
    }

    /// <summary>Ghi ^FD…^FS; nếu dữ liệu có ^ ~ \ thì dùng ^FH\ và mã hex.</summary>
    private void WriteField(CommandWriter w, string data, string prefix = "")
    {
        string escaped = Escape(data, out bool needFh);
        if (needFh) w.Ascii("^FH\\");
        w.Ascii("^FD").Ascii(prefix).Text(escaped).Line("^FS");
    }

    private static string Escape(string data, out bool needFh)
    {
        needFh = data.IndexOfAny(['^', '~', '\\']) >= 0;
        if (!needFh) return data;
        var sb = new StringBuilder(data.Length + 8);
        foreach (char c in data)
            sb.Append(c switch { '\\' => "\\5C", '^' => "\\5E", '~' => "\\7E", _ => c.ToString() });
        return sb.ToString();
    }

    // =================== Trạng thái ~HS ===================

    public override byte[]? BuildStatusRequest() => Encoding.ASCII.GetBytes(Opt("statusCommand", "~HS") + "\r\n");

    public override bool TryParseStatus(ReadOnlySpan<byte> response, out PrinterStatus status)
        => ZebraHostStatus.TryParse(response, out status);
}

/// <summary>
/// Phân tích phản hồi ~HS: 3 chuỗi STX…ETX.
/// String1: aaa,b(paper out),c(pause),dddd,eee(formats in buffer),f(buffer full),g,h,iii,j,k(under temp),l(over temp)
/// String2: mmm,n,o(head up),p(ribbon out),q,r,s,t(label waiting),uuuuuuuu(labels remaining),v,www
/// </summary>
public static class ZebraHostStatus
{
    public static bool TryParse(ReadOnlySpan<byte> response, out PrinterStatus status)
    {
        status = PrinterStatus.Unknown();
        var strings = new List<string[]>();
        int pos = 0;
        while (strings.Count < 3)
        {
            int stx = response[pos..].IndexOf(ControlChars_STX);
            if (stx < 0) break;
            stx += pos;
            int etx = response[(stx + 1)..].IndexOf(ControlChars_ETX);
            if (etx < 0) break;
            etx += stx + 1;
            strings.Add(Encoding.ASCII.GetString(response[(stx + 1)..etx]).Split(','));
            pos = etx + 1;
        }
        if (strings.Count < 2) return false;

        string[] s1 = strings[0], s2 = strings[1];
        static bool Flag(string[] a, int i) => i < a.Length && a[i].Trim() == "1";
        static int Num(string[] a, int i) => i < a.Length && int.TryParse(a[i].Trim(), out int v) ? v : 0;

        var errors = PrinterErrors.None;
        if (Flag(s1, 1)) errors |= PrinterErrors.PaperOut;
        if (Flag(s1, 5)) errors |= PrinterErrors.BufferOverflow;
        if (Flag(s1, 10) || Flag(s1, 11)) errors |= PrinterErrors.Temperature;
        if (Flag(s2, 2)) errors |= PrinterErrors.HeadOpen;
        if (Flag(s2, 3)) errors |= PrinterErrors.RibbonOut;
        bool paused = Flag(s1, 2);
        int remaining = Num(s2, 8);
        int formats = Num(s1, 4);
        bool labelWaiting = Flag(s2, 7);

        var state = errors != PrinterErrors.None ? PrinterState.Error
            : paused ? PrinterState.Offline
            : labelWaiting ? PrinterState.WaitingForTakeOut
            : remaining > 0 ? PrinterState.Printing
            : formats > 0 ? PrinterState.Busy
            : PrinterState.Ready;

        status = new PrinterStatus
        {
            State = state,
            Errors = errors,
            Warnings = paused ? PrinterWarnings.Paused : PrinterWarnings.None,
            RemainingLabels = remaining,
            RawCode = string.Join(",", s1) + "|" + string.Join(",", s2),
            RawResponse = response.ToArray(),
        };
        return true;
    }

    private const byte ControlChars_STX = 0x02, ControlChars_ETX = 0x03;

    /// <summary>Tạo phản hồi ~HS mẫu (Simulator/test).</summary>
    public static byte[] BuildResponse(bool paperOut = false, bool paused = false, bool headUp = false, bool ribbonOut = false, int remaining = 0)
    {
        string s1 = $"030,{(paperOut ? 1 : 0)},{(paused ? 1 : 0)},1245,000,0,0,0,000,0,0,0";
        string s2 = $"000,0,{(headUp ? 1 : 0)},{(ribbonOut ? 1 : 0)},1,2,6,0,{remaining:D8},1,000";
        string s3 = "1234,0";
        var sb = $"\x02{s1}\x03\r\n\x02{s2}\x03\r\n\x02{s3}\x03\r\n";
        return Encoding.ASCII.GetBytes(sb);
    }
}
