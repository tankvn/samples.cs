namespace PrinterHub.Core.Util;

/// <summary>
/// Bộ mã hoá Code 128 (subset B, tự chuyển subset C cho chuỗi số chẵn dài) → độ rộng module.
/// Dùng để vẽ mã vạch khi in qua driver GDI hoặc xem trước (máy in nhãn tự vẽ khi dùng lệnh gốc).
/// </summary>
public static class Code128
{
    // Mỗi mẫu: 6 độ rộng (vạch, khoảng, vạch, khoảng, vạch, khoảng) — tổng 11 module. Stop (106) có 7 phần tử, tổng 13.
    internal static readonly string[] Patterns =
    [
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
        "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
        "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
        "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
        "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
        "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
        "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
        "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
        "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
        "114131","311141","411131","211412","211214","211232","2331112",
    ];

    private const int StartB = 104, StartC = 105, CodeB = 100, CodeC = 99, Stop = 106;

    /// <summary>Trả về danh sách giá trị mã (gồm start, checksum, stop).</summary>
    public static List<int> EncodeValues(string data)
    {
        foreach (char c in data)
            if (c < 32 || c > 126) throw new ArgumentException($"Code128-B không hỗ trợ ký tự U+{(int)c:X4}");

        var values = new List<int>();
        int i = 0;
        bool useC = IsDigits(data, 0, Math.Min(4, data.Length)) && data.Length >= 4;
        values.Add(useC ? StartC : StartB);
        while (i < data.Length)
        {
            if (useC)
            {
                if (i + 1 < data.Length && char.IsAsciiDigit(data[i]) && char.IsAsciiDigit(data[i + 1]))
                {
                    values.Add((data[i] - '0') * 10 + (data[i + 1] - '0'));
                    i += 2;
                    continue;
                }
                values.Add(CodeB);
                useC = false;
                continue;
            }
            // Chuyển sang C nếu phía trước có ≥ 6 chữ số liên tiếp (số chẵn)
            int run = DigitRun(data, i);
            if (run >= 6)
            {
                if (run % 2 == 1) { values.Add(data[i] - 32); i++; }
                values.Add(CodeC);
                useC = true;
                continue;
            }
            values.Add(data[i] - 32);
            i++;
        }
        int sum = values[0];
        for (int k = 1; k < values.Count; k++) sum += values[k] * k;
        values.Add(sum % 103);
        values.Add(Stop);
        return values;
    }

    /// <summary>Độ rộng các phần tử (bắt đầu bằng vạch đen), đơn vị module.</summary>
    public static List<int> EncodeWidths(string data)
    {
        var widths = new List<int>();
        foreach (int v in EncodeValues(data))
            foreach (char w in Patterns[v]) widths.Add(w - '0');
        return widths;
    }

    /// <summary>Tổng số module (không tính quiet zone).</summary>
    public static int ModuleCount(string data) => EncodeWidths(data).Sum();

    private static bool IsDigits(string s, int start, int count)
    {
        for (int i = start; i < start + count; i++) if (!char.IsAsciiDigit(s[i])) return false;
        return count > 0;
    }

    private static int DigitRun(string s, int start)
    {
        int n = 0;
        while (start + n < s.Length && char.IsAsciiDigit(s[start + n])) n++;
        return n;
    }
}
