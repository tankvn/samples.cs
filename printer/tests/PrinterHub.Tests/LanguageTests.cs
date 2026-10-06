using System.Text;
using PrinterHub.Core.Model;
using PrinterHub.Core.Util;
using PrinterHub.Languages.Generic;
using PrinterHub.Languages.Godex;
using PrinterHub.Languages.Sato;
using PrinterHub.Languages.Zebra;

namespace PrinterHub.Tests;

public static class LanguageTests
{
    private static string Display(byte[] b, Encoding e) => ControlChars.ToDisplay(b, e, keepNewLines: false);

    // ======================= SBPL =======================

    [Test]
    public static void Sbpl_Frame()
    {
        var lang = new SbplLanguage();
        var doc = new LabelDocument { Width = 800, Height = 400, Copies = 3 }.Text(100, 50, "HELLO", 24);
        byte[] b = lang.Render(doc);
        Assert.Equal((byte)0x02, b[0]);
        Assert.Equal((byte)0x1B, b[1]);
        Assert.Equal((byte)'A', b[2]);
        Assert.Equal((byte)0x03, b[^1]);
        string s = Display(b, lang.TextEncoding);
        Assert.Contains("<ESC>A104000800", s);
        Assert.Contains("<ESC>V0050<ESC>H0100<ESC>XMHELLO", s);
        Assert.Contains("<ESC>Q3<ESC>Z<ETX>", s);
    }

    [Test]
    public static void Sbpl_Kanji_ShiftJis()
    {
        var lang = new SbplLanguage();
        byte[] b = lang.Render(new LabelDocument().Text(10, 10, "部品", 48));
        var kanji = Encoding.ASCII.GetBytes("\x1BK9B");
        int idx = b.AsSpan().IndexOf(kanji);
        Assert.True(idx > 0, "Thiếu ESC K9B");
        Assert.SequenceEqual(new byte[] { 0x95, 0x94, 0x95, 0x69 }, b.AsSpan(idx + 4, 4));
        Assert.Contains("<ESC>L0202", Display(b, lang.TextEncoding));
    }

    [Test]
    public static void Sbpl_Barcodes_Qr_Box()
    {
        var lang = new SbplLanguage(new Dictionary<string, string> { ["jobId"] = "07", ["kanjiCode"] = "KC1" });
        var doc = new LabelDocument()
            .Barcode(10, 20, BarcodeType.Code128, "ABC123", 80, 2, humanReadable: false)
            .Barcode(10, 120, BarcodeType.Code39, "X1", 60, 3, humanReadable: false)
            .Qr(400, 20, "QR-DATA", 5, 'H')
            .Box(0, 0, 500, 300, 4)
            .Line(0, 150, 500);
        string s = Display(lang.Render(doc), lang.TextEncoding);
        Assert.Contains("<ESC>KC1", s);
        Assert.Contains("<ESC>ID07", s);
        Assert.Contains("<ESC>BG02080>HABC123", s);
        Assert.Contains("<ESC>B103060*X1*", s);
        Assert.Contains("<ESC>2D30,H,05,1,0<ESC>DN0007,QR-DATA", s);
        Assert.Contains("<ESC>FW0404V0300H0500", s);
        Assert.Contains("<ESC>FW02H0500", s);
    }

    [Test]
    public static void Sbpl_NonStandardCodes()
    {
        var lang = new SbplLanguage(new Dictionary<string, string> { ["nonStandardCodes"] = "true", ["labelSize"] = "false" });
        string s = Encoding.ASCII.GetString(lang.Render(new LabelDocument().Text(1, 2, "A")));
        Assert.True(s.StartsWith("{^A"), s);
        Assert.True(s.EndsWith("^Z}"), s);
        Assert.False(s.Contains('\x1B'));
    }

    [Test]
    public static void Sbpl_Image()
    {
        var lang = new SbplLanguage(new Dictionary<string, string> { ["labelSize"] = "false" });
        var img = MonoImage.Checker(16, 10);
        string s = Display(lang.Render(new LabelDocument().Image(0, 0, img)), Encoding.ASCII);
        // 16 dot = 2 byte; 10 dot pad → 16 dòng = 2 khối 8
        Assert.Contains("<ESC>GH002002", s);
        int hexStart = s.IndexOf("GH002002", StringComparison.Ordinal) + 8;
        int hexEnd = s.IndexOf("<ESC>", hexStart, StringComparison.Ordinal);
        Assert.Equal(2 * 16 * 2, hexEnd - hexStart); // 32 byte = 64 ký tự hex
    }

    [Test]
    public static void Sato_Status4_Parse()
    {
        var resp = SatoStatus.BuildResponse("01", 'G', 12, "JOB-A");
        Assert.True(SatoStatus.TryParse(resp, out var st));
        Assert.Equal(PrinterState.Printing, st.State);
        Assert.Equal(12, st.RemainingLabels);
        Assert.Equal("01", st.JobId);
        Assert.Equal("JOB-A", st.JobName);

        // LAN có thêm header phía trước phần nội dung
        byte[] lan = [0x02, .. Encoding.ASCII.GetBytes("HDRHDRHDR"), .. resp[1..]];
        Assert.True(SatoStatus.TryParse(lan, out var st2));
        Assert.Equal(12, st2.RemainingLabels);
    }

    [Test]
    public static void Sato_Status_Errors_And_Warnings()
    {
        Assert.True(SatoStatus.TryParse(SatoStatus.BuildResponse("00", 'c', 0), out var st));
        Assert.Equal(PrinterState.Error, st.State);
        Assert.Equal(PrinterErrors.PaperOut, st.Errors);
        Assert.False(st.CanPrint);

        Assert.True(SatoStatus.TryParse(SatoStatus.BuildResponse("00", 'B', 0), out var w));
        Assert.Equal(PrinterState.Ready, w.State);
        Assert.Equal(PrinterWarnings.RibbonNearEnd, w.Warnings);

        // Status3 rút gọn: STX ID(2) ST(1) REM(6) ETX
        Assert.True(SatoStatus.TryParse(Encoding.ASCII.GetBytes("\u000201A000005\u0003"), out var s3));
        Assert.Equal(5, s3.RemainingLabels);

        Assert.False(SatoStatus.TryParse(Encoding.ASCII.GetBytes("\u000201A0000"), out _)); // chưa đủ
    }

    // ======================= ZPL =======================

    [Test]
    public static void Zpl_Structure_Utf8()
    {
        var lang = new ZplLanguage(new Dictionary<string, string> { ["unicodeFont"] = "E:ANMDJ.TTF" });
        var doc = new LabelDocument { Width = 812, Height = 406, Copies = 2 }
            .Text(10, 20, "Xin chào 部品", 30)
            .Barcode(10, 100, BarcodeType.Code128, "ABC", 60)
            .Qr(300, 20, "Q", 4, 'L');
        byte[] b = lang.Render(doc);
        string s = Encoding.UTF8.GetString(b);
        Assert.True(s.StartsWith("^XA\r\n^CI28"), s);
        Assert.Contains("^PW812", s);
        Assert.Contains("^FO10,20^A@N,30,30,E:ANMDJ.TTF^FDXin chào 部品^FS", s);
        Assert.Contains("^BY2,3,60^BCN,60,Y,N,N^FDABC^FS", s);
        Assert.Contains("^BQN,2,4^FDLA,Q^FS", s);
        Assert.Contains("^PQ2", s);
        Assert.True(s.TrimEnd().EndsWith("^XZ"));
    }

    [Test]
    public static void Zpl_EscapeCaret()
    {
        string s = Encoding.UTF8.GetString(new ZplLanguage().Render(new LabelDocument().Text(0, 0, "A^B~C")));
        Assert.Contains("^FH\\^FDA\\5EB\\7EC^FS", s);
    }

    [Test]
    public static void Zpl_ShiftJis_Charset()
    {
        var lang = new ZplLanguage { TextEncoding = TextEncodings.ShiftJis };
        Assert.Contains("^CI15", Encoding.ASCII.GetString(lang.Render(new LabelDocument())));
    }

    [Test]
    public static void Zebra_HostStatus_Parse()
    {
        Assert.True(ZebraHostStatus.TryParse(ZebraHostStatus.BuildResponse(), out var ok));
        Assert.Equal(PrinterState.Ready, ok.State);

        Assert.True(ZebraHostStatus.TryParse(ZebraHostStatus.BuildResponse(paperOut: true, headUp: true), out var err));
        Assert.Equal(PrinterState.Error, err.State);
        Assert.True(err.Errors.HasFlag(PrinterErrors.PaperOut));
        Assert.True(err.Errors.HasFlag(PrinterErrors.HeadOpen));

        Assert.True(ZebraHostStatus.TryParse(ZebraHostStatus.BuildResponse(remaining: 7), out var pr));
        Assert.Equal(PrinterState.Printing, pr.State);
        Assert.Equal(7, pr.RemainingLabels);

        Assert.False(ZebraHostStatus.TryParse(Encoding.ASCII.GetBytes("\u0002030,0,0"), out _));
    }

    // ======================= EZPL =======================

    [Test]
    public static void Ezpl_Structure()
    {
        var lang = new EzplLanguage();
        var doc = new LabelDocument { Dpi = 203, Width = 799, Height = 400, Copies = 4 }
            .Text(10, 10, "HELLO", 20)
            .Barcode(10, 50, BarcodeType.Code128, "123", 50)
            .Box(0, 0, 100, 50, 2);
        string s = Encoding.UTF8.GetString(lang.Render(doc));
        Assert.True(s.StartsWith("^Q50,3\r\n^W100\r\n"), s);
        Assert.Contains("^P4\r\n", s);
        Assert.Contains("^L\r\n", s);
        Assert.Contains("AD,10,10,1,1,0,0,HELLO\r\n", s);
        Assert.Contains("BQ,10,50,2,6,50,0,1,123\r\n", s);
        Assert.Contains("R0,0,100,50,2,2\r\n", s);
        Assert.True(s.EndsWith("E\r\n"));
    }

    [Test]
    public static void Godex_Status_Parse()
    {
        Assert.True(GodexStatus.TryParse("00\r\n"u8, out var r));
        Assert.Equal(PrinterState.Ready, r.State);
        Assert.True(GodexStatus.TryParse("04\r\n"u8, out var h));
        Assert.Equal(PrinterErrors.HeadOpen, h.Errors);
        Assert.False(GodexStatus.TryParse("0"u8, out _));
    }

    // ======================= PJL / RAW =======================

    [Test]
    public static void Pjl_WrapAndStatus()
    {
        var lang = new PjlLanguage(new Dictionary<string, string> { ["duplex"] = "true" });
        var job = PrintJob.FromRaw("%PDF-1.7"u8.ToArray(), "Report \"Q3\"", "PDF") with { Copies = 2 };
        string s = Encoding.ASCII.GetString(lang.WrapRaw(job.RawData!, job));
        Assert.True(s.StartsWith("\x1B%-12345X@PJL\r\n"));
        Assert.Contains("@PJL JOB NAME=\"Report 'Q3'\"", s);
        Assert.Contains("@PJL SET COPIES=2", s);
        Assert.Contains("@PJL SET DUPLEX=ON", s);
        Assert.Contains("@PJL ENTER LANGUAGE=PDF\r\n%PDF-1.7", s);
        Assert.True(s.EndsWith("@PJL EOJ\r\n\x1B%-12345X"));

        Assert.True(lang.TryParseStatus("@PJL INFO STATUS\r\nCODE=10001\r\nDISPLAY=\"READY\"\r\nONLINE=TRUE\r\n\f"u8, out var st));
        Assert.Equal(PrinterState.Ready, st.State);
        Assert.Equal("READY", st.Message);
        Assert.True(lang.TryParseStatus("CODE=41000\r\nONLINE=FALSE\r\n\f"u8, out var e));
        Assert.Equal(PrinterErrors.PaperOut, e.Errors);
    }

    [Test]
    public static void Raw_NoLabel()
    {
        var raw = new RawLanguage();
        Assert.False(raw.CanRenderLabel);
        Assert.Throws<NotSupportedException>(() => raw.Render(new LabelDocument()));
        Assert.Equal(null, raw.BuildStatusRequest());
    }
}
