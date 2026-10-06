using System.Text;

namespace PrinterHub.Core.Util;

public static class HexDump
{
    /// <summary>Định dạng hex + ASCII, 16 byte mỗi dòng.</summary>
    public static string Format(ReadOnlySpan<byte> data, int maxBytes = int.MaxValue)
    {
        var sb = new StringBuilder();
        int len = Math.Min(data.Length, maxBytes);
        for (int off = 0; off < len; off += 16)
        {
            sb.Append(off.ToString("X6")).Append("  ");
            for (int i = 0; i < 16; i++)
            {
                if (off + i < len) sb.Append(data[off + i].ToString("X2")).Append(' ');
                else sb.Append("   ");
                if (i == 7) sb.Append(' ');
            }
            sb.Append(' ');
            for (int i = 0; i < 16 && off + i < len; i++)
            {
                byte b = data[off + i];
                sb.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
            }
            sb.Append('\n');
        }
        if (data.Length > len) sb.Append($"... ({data.Length - len} bytes more)\n");
        return sb.ToString();
    }
}

