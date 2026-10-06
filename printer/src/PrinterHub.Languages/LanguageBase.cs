using System.Globalization;
using System.Text;
using PrinterHub.Core;
using PrinterHub.Core.Model;
using PrinterHub.Core.Util;

namespace PrinterHub.Languages;

/// <summary>Lớp cơ sở cho các ngôn ngữ lệnh: quản lý encoding và tuỳ chọn.</summary>
public abstract class LanguageBase(IReadOnlyDictionary<string, string>? options) : ICommandLanguage
{
    protected IReadOnlyDictionary<string, string> Options { get; } =
        options ?? new Dictionary<string, string>();

    public abstract string Name { get; }
    public virtual Encoding TextEncoding { get; set; } = TextEncodings.Utf8;
    public virtual bool CanRenderLabel => true;

    public abstract byte[] Render(LabelDocument document);
    public abstract byte[]? BuildStatusRequest();
    public abstract bool TryParseStatus(ReadOnlySpan<byte> response, out PrinterStatus status);
    public virtual byte[] WrapRaw(byte[] data, PrintJob job) => data;

    protected string Opt(string key, string fallback) =>
        Options.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;

    protected string? OptOrNull(string key) =>
        Options.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : null;

    protected int OptInt(string key, int fallback) =>
        Options.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : fallback;

    protected bool OptBool(string key, bool fallback) =>
        Options.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;

    protected static string D(int value, int digits) =>
        Math.Max(0, value).ToString(new string('0', digits), CultureInfo.InvariantCulture);

    protected static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Bộ ghi byte lệnh: phân biệt byte điều khiển, ASCII lệnh, và văn bản theo encoding.</summary>
public sealed class CommandWriter(Encoding textEncoding)
{
    private readonly MemoryStream _ms = new();

    public Encoding TextEncoding { get; } = textEncoding;

    public CommandWriter Byte(byte b) { _ms.WriteByte(b); return this; }
    public CommandWriter Bytes(ReadOnlySpan<byte> b) { _ms.Write(b); return this; }
    /// <summary>Lệnh ASCII (tên lệnh, tham số số).</summary>
    public CommandWriter Ascii(string s) { _ms.Write(Encoding.ASCII.GetBytes(s)); return this; }
    /// <summary>Dữ liệu văn bản theo encoding đã chọn (UTF-8, Shift-JIS...).</summary>
    public CommandWriter Text(string s) { _ms.Write(TextEncoding.GetBytes(s)); return this; }
    public CommandWriter Text(string s, Encoding enc) { _ms.Write(enc.GetBytes(s)); return this; }
    public CommandWriter Line(string s = "") => Ascii(s).Ascii("\r\n");

    public int Length => (int)_ms.Length;
    public byte[] ToArray() => _ms.ToArray();
}
