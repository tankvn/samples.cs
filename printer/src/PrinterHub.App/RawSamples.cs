namespace PrinterHub.App;

/// <summary>Lệnh mẫu cho tab "Lệnh raw" (ký hiệu &lt;ESC&gt;, &lt;STX&gt;... được chuyển thành byte khi gửi).</summary>
internal static class RawSamples
{
    public sealed record Sample(string Brand, string Title, string Language, string Encoding, string Text, bool SbplTags = false, bool WaitReply = false);

    public static readonly Sample[] All =
    [
        new("SATO", "SBPL – nhãn test", "SBPL", "shift_jis",
            "<STX><ESC>A<ESC>A104000800\n<ESC>V0050<ESC>H0050<ESC>L0202<ESC>XMSATO SBPL TEST\n<ESC>V0130<ESC>H0050<ESC>BG02080>H12345678\n<ESC>V0250<ESC>H0050<ESC>XSPrinterHub\n<ESC>Q1<ESC>Z<ETX>"),
        new("SATO", "SBPL – ký hiệu manual <A>", "SBPL", "shift_jis",
            "<STX><A><V>0100<H>0100<L>0202<XM>SATO <<A>> NOTATION<Q>1<Z><ETX>", SbplTags: true),
        new("SATO", "SBPL – Kanji (Shift-JIS)", "SBPL", "shift_jis",
            "<STX><ESC>A<ESC>V0100<ESC>H0100<ESC>L0202<ESC>K9B部品テスト<ESC>Q1<ESC>Z<ETX>"),
        new("SATO", "SBPL – QR code", "SBPL", "shift_jis",
            "<STX><ESC>A<ESC>V0050<ESC>H0050<ESC>2D30,M,06,1,0<ESC>DN0014,PRINTERHUB-QR1<ESC>Q1<ESC>Z<ETX>"),
        new("SATO", "Status4 – ENQ", "SBPL", "shift_jis", "<ENQ>", WaitReply: true),
        new("SATO", "Huỷ job – CAN", "SBPL", "shift_jis", "<CAN>"),
        new("SATO", "DC2 PG – trạng thái mở rộng", "SBPL", "shift_jis", "<DC2>PG", WaitReply: true),

        new("Zebra", "ZPL – nhãn test (UTF-8)", "ZPL", "utf-8",
            "^XA\n^CI28\n^FO50,50^A0N,40,40^FDZEBRA ZPL TEST^FS\n^FO50,110^BY2^BCN,80,Y,N,N^FD12345678^FS\n^FO50,240^BQN,2,5^FDMA,PRINTERHUB^FS\n^XZ"),
        new("Zebra", "~HS – host status", "ZPL", "utf-8", "~HS", WaitReply: true),
        new("Zebra", "SGD – getvar ngôn ngữ", "ZPL", "utf-8", "! U1 getvar \"device.languages\"<CR><LF>", WaitReply: true),
        new("Zebra", "SGD – thông tin máy", "ZPL", "utf-8", "! U1 getvar \"device.product_name\"<CR><LF>", WaitReply: true),
        new("Zebra", "JSON SGD (port 9200)", "ZPL", "utf-8", "{}{\"device.friendly_name\":null,\"device.unique_id\":null}", WaitReply: true),
        new("Zebra", "Calibrate ~JC", "ZPL", "utf-8", "~JC"),

        new("Godex", "EZPL – nhãn test", "EZPL", "utf-8",
            "^Q30,3\n^W50\n^H10\n^P1\n^L\nAD,30,30,1,1,0,0,GODEX EZPL TEST\nBQ,30,80,2,6,60,0,1,12345678\nE\n"),
        new("Godex", "~S,CHECK – trạng thái", "EZPL", "utf-8", "^XSET,IMMEDIATE,1<CR><LF>~S,CHECK<CR><LF>", WaitReply: true),
        new("Godex", "~V – in self-test", "EZPL", "utf-8", "~V<CR><LF>"),

        new("FUJIFILM", "PJL INFO STATUS", "PJL", "utf-8", "<ESC>%-12345X@PJL INFO STATUS<CR><LF><ESC>%-12345X", WaitReply: true),
        new("FUJIFILM", "PJL INFO ID", "PJL", "utf-8", "<ESC>%-12345X@PJL INFO ID<CR><LF><ESC>%-12345X", WaitReply: true),
        new("FUJIFILM", "PCL – trang text", "RAW", "utf-8", "<ESC>E Hello PCL from PrinterHub<CR><LF><FF><ESC>E"),

        new("Fujitsu/POS", "ESC/P – dòng chữ", "RAW", "shift_jis", "<ESC>@Hello ESC/P<CR><LF><FF>"),
        new("Fujitsu/POS", "ESC/POS – in + cắt", "RAW", "utf-8", "<ESC>@Hello ESC/POS<LF><LF><LF><GS>V<0x00>"),
    ];
}
