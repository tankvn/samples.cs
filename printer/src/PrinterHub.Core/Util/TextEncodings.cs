using System.Text;

namespace PrinterHub.Core.Util;

/// <summary>
/// Quản lý encoding. Giống dự án cp932: phải đăng ký CodePagesEncodingProvider để dùng Shift-JIS (932).
/// </summary>
public static class TextEncodings
{
    private static int _registered;

    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>UTF-8 không BOM.</summary>
    public static readonly Encoding Utf8 = new UTF8Encoding(false);

    public static Encoding ShiftJis { get { EnsureRegistered(); return Encoding.GetEncoding(932); } }

    /// <summary>Lấy encoding theo tên: utf-8, shift_jis / sjis / cp932 / 932, windows-1258, ascii, utf-16...</summary>
    public static Encoding Get(string? name)
    {
        EnsureRegistered();
        if (string.IsNullOrWhiteSpace(name)) return Utf8;
        return name.Trim().ToLowerInvariant() switch
        {
            "utf8" or "utf-8" => Utf8,
            "sjis" or "shift_jis" or "shift-jis" or "cp932" or "932" or "ms932" => Encoding.GetEncoding(932),
            "utf16" or "utf-16" or "unicode" => new UnicodeEncoding(false, false),
            "ascii" => Encoding.ASCII,
            "latin1" or "iso-8859-1" => Encoding.Latin1,
            var n when int.TryParse(n, out int cp) => Encoding.GetEncoding(cp),
            var n => Encoding.GetEncoding(n),
        };
    }

    public static bool IsAscii(string s)
    {
        foreach (char c in s) if (c > 0x7E || (c < 0x20 && c != '\t')) return false;
        return true;
    }

    /// <summary>
    /// Đọc file văn bản tự phát hiện encoding: BOM → UTF-8 hợp lệ → Shift-JIS (rút gọn từ CsvUtility của cp932).
    /// </summary>
    public static (string text, Encoding encoding) ReadAllTextAuto(string path)
    {
        EnsureRegistered();
        byte[] data = File.ReadAllBytes(path);
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            return (Utf8.GetString(data, 3, data.Length - 3), Utf8);
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return (Encoding.Unicode.GetString(data, 2, data.Length - 2), Encoding.Unicode);
        try
        {
            var strict = new UTF8Encoding(false, true);
            return (strict.GetString(data), Utf8);
        }
        catch (DecoderFallbackException)
        {
            var sjis = Encoding.GetEncoding(932);
            return (sjis.GetString(data), sjis);
        }
    }
}
