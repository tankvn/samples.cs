using System.Text;
using PrinterHub.Core.Config;
using PrinterHub.Core.Model;
using PrinterHub.Core.Util;

namespace PrinterHub.Tests;

public static class CoreTests
{
    [Test]
    public static void Route_Tcp_DefaultPort()
    {
        var r = RouteConfig.Parse("tcp://192.168.1.50");
        Assert.Equal("tcp", r.Kind);
        Assert.Equal("192.168.1.50", r.Host);
        Assert.Equal(9100, r.Port);
    }

    [Test]
    public static void Route_Sato4_Ports()
    {
        var r = RouteConfig.Parse("sato4://10.0.0.5?data=2024&status=2025&readTimeout=500");
        Assert.Equal("sato4", r.Kind);
        Assert.Equal(2024, r.Port);
        Assert.Equal(2025, r.StatusPort);
        Assert.Equal(500, r.ReadTimeoutMs);
        var d = RouteConfig.Parse("sato4://10.0.0.5");
        Assert.Equal(1024, d.Port);
        Assert.Equal(1025, d.StatusPort);
    }

    [Test]
    public static void Route_Lpr_Spooler_Serial_File()
    {
        var l = RouteConfig.Parse("lpr://printer01/queue1");
        Assert.Equal(515, l.Port);
        Assert.Equal("queue1", l.Queue);

        var s = RouteConfig.Parse("spooler://SATO CL4NX Plus 305dpi");
        Assert.Equal("SATO CL4NX Plus 305dpi", s.Queue);

        var c = RouteConfig.Parse("com://COM3?baud=19200&handshake=rtscts");
        Assert.Equal("serial", c.Kind);
        Assert.Equal("COM3", c.Path);
        Assert.Equal(19200, c.GetInt("baud", 0));

        var f = RouteConfig.Parse(@"file://\\PC01\SATO");
        Assert.Equal("file", f.Kind);
        Assert.Equal(@"\\PC01\SATO", f.Path);

        var v6 = RouteConfig.Parse("tcp://[::1]:9101");
        Assert.Equal("::1", v6.Host);
        Assert.Equal(9101, v6.Port);
    }

    [Test]
    public static void ControlChars_ParseAndDisplay()
    {
        var bytes = ControlChars.Parse("<STX><ESC>A<0x1B>Z<ETX><<x", Encoding.ASCII);
        Assert.SequenceEqual(new byte[] { 0x02, 0x1B, (byte)'A', 0x1B, (byte)'Z', 0x03, (byte)'<', (byte)'x' }, bytes);
        Assert.Equal("<STX><ESC>A<ESC>Z<ETX><<x", ControlChars.ToDisplay(bytes, Encoding.ASCII));
    }

    [Test]
    public static void ControlChars_SbplNotation()
    {
        var bytes = ControlChars.Parse("<STX><A><H>0100<V>0100<XM>ABC<Q>1<Z><ETX>", Encoding.ASCII, sbplEscTags: true);
        Assert.Equal("<STX><ESC>A<ESC>H0100<ESC>V0100<ESC>XMABC<ESC>Q1<ESC>Z<ETX>", ControlChars.ToDisplay(bytes, Encoding.ASCII));
    }

    [Test]
    public static void ControlChars_ShiftJis()
    {
        var bytes = ControlChars.Parse("<ESC>K9B部品", TextEncodings.ShiftJis);
        // 部 = 0x95 0x94, 品 = 0x95 0x69 trong Shift-JIS
        Assert.SequenceEqual(new byte[] { 0x1B, (byte)'K', (byte)'9', (byte)'B', 0x95, 0x94, 0x95, 0x69 }, bytes);
    }

    [Test]
    public static void Label_Json_RoundTrip_And_Bind()
    {
        var t = DemoLabels.Template(300);
        string json = DemoLabels.ToJson(t);
        Assert.Contains("\"element\": \"barcode\"", json);
        var back = DemoLabels.FromJson(json);
        Assert.Equal(t.Elements.Count, back.Elements.Count);
        var bound = back.Bind(new Dictionary<string, string> { ["PartNo"] = "X1", ["Qty"] = "5" });
        var bc = bound.Elements.OfType<BarcodeElement>().Single();
        Assert.Equal("X1", bc.Data);
        Assert.Contains("QTY : 5", string.Join("|", bound.Elements.OfType<TextElement>().Select(e => e.Text)));
        Assert.Contains("{Lot}", bound.Elements.OfType<QrElement>().Single().Data); // biến chưa có giữ nguyên
        Assert.Equal(300, back.Dpi);
    }

    [Test]
    public static void Label_MmConversion()
    {
        var d = new LabelDocument { Dpi = 203 };
        Assert.Equal(799, d.Mm(100));
        d.Dpi = 305;
        Assert.Equal(1201, d.Mm(100));
    }

    [Test]
    public static void Csv_QuotesAndDelimiter()
    {
        var rows = CsvLite.ReadRecords("A;B;C\n1;\"x;y\";\"he said \"\"hi\"\"\"\n2;部品;\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal("x;y", rows[0]["B"]);
        Assert.Equal("he said \"hi\"", rows[0]["C"]);
        Assert.Equal("部品", rows[1]["B"]);
        Assert.Equal("", rows[1]["C"]);
    }

    [Test]
    public static void Csv_ShiftJisFileAutoDetect()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ph_{Guid.NewGuid():N}.csv");
        File.WriteAllBytes(path, TextEncodings.ShiftJis.GetBytes("Name,Qty\r\nネジ,10\r\n"));
        try
        {
            var rows = CsvLite.ReadFile(path);
            Assert.Equal("ネジ", rows[0]["Name"]);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public static void MonoImage_PadAndPixels()
    {
        var img = new MonoImage(10, 5);
        img[0, 0] = true;
        img[9, 4] = true;
        Assert.Equal(2, img.BytesPerRow);
        Assert.Equal(0x80, img.Data[0]);
        Assert.Equal(0x40, img.Data[4 * 2 + 1]);
        var p = img.PadHeight(8);
        Assert.Equal(8, p.Height);
        Assert.True(p[9, 4]);
    }

    [Test]
    public static void Profiles_SaveLoad()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ph_{Guid.NewGuid():N}.json");
        var store = new ProfileStore
        {
            Printers = [new PrinterProfile { Name = "A", Language = "SBPL", Routes = ["tcp://1.2.3.4"], LanguageOptions = { ["kanjiFont"] = "K9B" } }],
        };
        store.Save(path);
        try
        {
            var loaded = ProfileStore.Load(path);
            Assert.Equal("K9B", loaded.Find("a")!.LanguageOptions["kanjiFont"]);
            Assert.Equal(9100, loaded.Printers[0].ParseRoutes().Single().Port);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public static void Code128_Table_And_Checksum()
    {
        Assert.Equal(107, Code128.Patterns.Length);
        for (int i = 0; i < 106; i++) Assert.Equal(11, Code128.Patterns[i].Sum(c => c - '0'));
        Assert.Equal(13, Code128.Patterns[106].Sum(c => c - '0'));
        Assert.Equal(107, Code128.Patterns.Distinct().Count());

        // "PJJ123C" (ví dụ Wikipedia): Start B, P J J 1 2 3 C, checksum = (104+48·1+42·2+42·3+17·4+18·5+19·6+35·7) mod 103 = 55
        var v = Code128.EncodeValues("PJJ123C");
        Assert.Equal(104, v[0]);
        Assert.Equal(55, v[^2]);
        Assert.Equal(106, v[^1]);

        // Chuỗi số dài → subset C
        var c = Code128.EncodeValues("12345678");
        Assert.Equal(105, c[0]);
        Assert.Equal(1 + 4 + 2, c.Count);
        Assert.Equal(11 * 6 + 13, Code128.ModuleCount("12345678"));
    }
}
