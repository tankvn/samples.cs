using System.Globalization;
using System.Text;

namespace PrinterHub.Core.Util;

/// <summary>
/// Chuyển đổi giữa ký hiệu dễ đọc và byte điều khiển, dùng cho editor lệnh raw:
/// <c>&lt;STX&gt;&lt;ESC&gt;A&lt;ESC&gt;H0100...&lt;ESC&gt;Z&lt;ETX&gt;</c>, <c>&lt;0x1B&gt;</c>, <c>&lt;CR&gt;&lt;LF&gt;</c>.
/// Ký tự thường được mã hoá bằng encoding chỉ định (vd Shift-JIS cho Kanji).
/// </summary>
public static class ControlChars
{
    public const byte NUL = 0x00, SOH = 0x01, STX = 0x02, ETX = 0x03, EOT = 0x04, ENQ = 0x05, ACK = 0x06,
        BEL = 0x07, LF = 0x0A, CR = 0x0D, DLE = 0x10, DC1 = 0x11, DC2 = 0x12, DC3 = 0x13, DC4 = 0x14,
        NAK = 0x15, CAN = 0x18, ESC = 0x1B, FS = 0x1C, GS = 0x1D, RS = 0x1E, US = 0x1F;

    private static readonly string[] Names =
    [
        "NUL","SOH","STX","ETX","EOT","ENQ","ACK","BEL","BS","HT","LF","VT","FF","CR","SO","SI",
        "DLE","DC1","DC2","DC3","DC4","NAK","SYN","ETB","CAN","EM","SUB","ESC","FS","GS","RS","US"
    ];

    private static readonly Dictionary<string, byte> ByName = Names
        .Select((n, i) => (n, (byte)i))
        .ToDictionary(x => x.n, x => x.Item2, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// "&lt;STX&gt;&lt;ESC&gt;A..." → byte[]. Hỗ trợ &lt;NAME&gt;, &lt;0xHH&gt;, &lt;#HH&gt;, và &lt;&lt; để viết dấu '&lt;'.
    /// Các thẻ không nhận diện được (vd &lt;A&gt; của SBPL) được giữ nguyên dạng chữ.
    /// </summary>
    /// <param name="sbplEscTags">
    /// True: thẻ dạng SATO manual như &lt;A&gt;, &lt;H&gt;, &lt;XM&gt;, &lt;2D30&gt; được hiểu là ESC + thẻ.
    /// </param>
    public static byte[] Parse(string text, Encoding encoding, bool sbplEscTags = false)
    {
        var output = new List<byte>(text.Length);
        var literal = new StringBuilder();

        void FlushLiteral()
        {
            if (literal.Length == 0) return;
            output.AddRange(encoding.GetBytes(literal.ToString()));
            literal.Clear();
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '<')
            {
                if (i + 1 < text.Length && text[i + 1] == '<') { literal.Append('<'); i++; continue; }
                int end = text.IndexOf('>', i + 1);
                if (end > i + 1)
                {
                    string tag = text[(i + 1)..end];
                    if (TryTag(tag, out byte b))
                    {
                        FlushLiteral();
                        output.Add(b);
                        i = end;
                        continue;
                    }
                    if (sbplEscTags && IsSbplTag(tag))
                    {
                        FlushLiteral();
                        output.Add(ESC);
                        output.AddRange(Encoding.ASCII.GetBytes(tag));
                        i = end;
                        continue;
                    }
                }
            }
            literal.Append(c);
        }
        FlushLiteral();
        return output.ToArray();
    }

    private static bool IsSbplTag(string tag)
    {
        if (tag.Length is < 1 or > 4) return false;
        foreach (char ch in tag)
            if (!(ch is >= 'A' and <= 'Z' or >= '0' and <= '9' || "$&/%#@*!~=+().-".Contains(ch))) return false;
        return true;
    }

    private static bool TryTag(string tag, out byte value)
    {
        if (ByName.TryGetValue(tag, out value)) return true;
        if (tag.Equals("DEL", StringComparison.OrdinalIgnoreCase)) { value = 0x7F; return true; }
        string? hex = tag.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? tag[2..]
                    : tag.StartsWith('#') ? tag[1..] : null;
        if (hex is { Length: 1 or 2 } && byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            return true;
        value = 0;
        return false;
    }

    /// <summary>byte[] → chuỗi dễ đọc (byte điều khiển hiển thị dạng &lt;NAME&gt;). CR LF giữ xuống dòng.</summary>
    public static string ToDisplay(ReadOnlySpan<byte> data, Encoding encoding, bool keepNewLines = true)
    {
        var sb = new StringBuilder();
        int start = 0;
        void Flush(int end, ReadOnlySpan<byte> d)
        {
            if (end > start) sb.Append(encoding.GetString(d[start..end]).Replace("<", "<<"));
        }
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            if (b < 0x20 || b == 0x7F)
            {
                Flush(i, data);
                start = i + 1;
                if (keepNewLines && b == LF) { sb.Append("<LF>\n"); continue; }
                sb.Append('<').Append(b == 0x7F ? "DEL" : Names[b]).Append('>');
            }
        }
        Flush(data.Length, data);
        return sb.ToString();
    }
}
