using System.Text;

namespace CsvReader;

/// <summary>
/// Phát hiện encoding của file dựa trên BOM và phân tích byte.
/// Hỗ trợ: UTF-8 (BOM / no BOM), UTF-16 LE/BE, Shift-JIS (CP932).
/// </summary>
public static class EncodingDetector
{
    /// <summary>
    /// Phát hiện encoding của file.
    /// Trả về encoding được phát hiện và số byte BOM cần bỏ qua.
    /// </summary>
    public static (Encoding encoding, int bomLength) DetectEncoding(byte[] data)
    {
        // 1. Kiểm tra BOM (Byte Order Mark)
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            return (Encoding.UTF8, 3);

        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return (Encoding.Unicode, 2); // UTF-16 LE

        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            return (Encoding.BigEndianUnicode, 2); // UTF-16 BE

        // 2. Không có BOM → phân tích byte pattern
        //    Thử UTF-8 trước, nếu không hợp lệ thì thử Shift-JIS
        if (IsValidUtf8(data))
        {
            // Kiểm tra xem có chứa multi-byte UTF-8 sequences không
            // Nếu toàn bộ là ASCII thuần, có thể là Shift-JIS với nội dung ASCII
            bool hasMultiByteUtf8 = HasMultiByteUtf8(data);
            bool hasShiftJisPatterns = HasShiftJisPatterns(data);

            if (hasMultiByteUtf8 && !hasShiftJisPatterns)
                return (Encoding.UTF8, 0);

            if (!hasMultiByteUtf8 && hasShiftJisPatterns)
                return (Encoding.GetEncoding(932), 0);

            // Nếu cả hai đều có hoặc không có → mặc định UTF-8
            if (hasMultiByteUtf8)
                return (Encoding.UTF8, 0);
        }

        // 3. Thử Shift-JIS
        if (IsValidShiftJis(data))
            return (Encoding.GetEncoding(932), 0);

        // 4. Mặc định UTF-8
        return (Encoding.UTF8, 0);
    }

    /// <summary>
    /// Kiểm tra xem dữ liệu có phải UTF-8 hợp lệ không.
    /// </summary>
    private static bool IsValidUtf8(byte[] data)
    {
        int i = 0;
        while (i < data.Length)
        {
            if (data[i] <= 0x7F)
            {
                i++;
                continue;
            }

            int expectedBytes;
            if ((data[i] & 0xE0) == 0xC0) expectedBytes = 2;
            else if ((data[i] & 0xF0) == 0xE0) expectedBytes = 3;
            else if ((data[i] & 0xF8) == 0xF0) expectedBytes = 4;
            else return false;

            if (i + expectedBytes > data.Length)
                return false;

            for (int j = 1; j < expectedBytes; j++)
            {
                if ((data[i + j] & 0xC0) != 0x80)
                    return false;
            }

            i += expectedBytes;
        }
        return true;
    }

    /// <summary>
    /// Kiểm tra xem có byte sequence multi-byte UTF-8 không (tiếng Việt, tiếng Nhật, v.v.)
    /// </summary>
    private static bool HasMultiByteUtf8(byte[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] > 0x7F)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Kiểm tra xem dữ liệu có chứa pattern đặc trưng của Shift-JIS không.
    /// Shift-JIS double-byte: lead byte 0x81-0x9F hoặc 0xE0-0xEF,
    ///                        trail byte 0x40-0x7E hoặc 0x80-0xFC.
    /// </summary>
    private static bool HasShiftJisPatterns(byte[] data)
    {
        int sjisDoubleByteCount = 0;
        int i = 0;

        while (i < data.Length - 1)
        {
            byte b = data[i];

            if ((b >= 0x81 && b <= 0x9F) || (b >= 0xE0 && b <= 0xEF))
            {
                byte next = data[i + 1];
                if ((next >= 0x40 && next <= 0x7E) || (next >= 0x80 && next <= 0xFC))
                {
                    sjisDoubleByteCount++;
                    i += 2;
                    continue;
                }
            }

            // Half-width katakana: 0xA1-0xDF
            if (b >= 0xA1 && b <= 0xDF)
            {
                sjisDoubleByteCount++;
            }

            i++;
        }

        // Nếu có >= 3 Shift-JIS double-byte sequences → có khả năng cao là Shift-JIS
        return sjisDoubleByteCount >= 3;
    }

    /// <summary>
    /// Kiểm tra xem dữ liệu có phải Shift-JIS hợp lệ không.
    /// </summary>
    private static bool IsValidShiftJis(byte[] data)
    {
        int i = 0;
        int validDoubleBytes = 0;
        int invalidBytes = 0;

        while (i < data.Length)
        {
            byte b = data[i];

            // ASCII
            if (b <= 0x7F)
            {
                i++;
                continue;
            }

            // Half-width katakana
            if (b >= 0xA1 && b <= 0xDF)
            {
                validDoubleBytes++;
                i++;
                continue;
            }

            // Shift-JIS double byte
            if ((b >= 0x81 && b <= 0x9F) || (b >= 0xE0 && b <= 0xEF))
            {
                if (i + 1 >= data.Length)
                {
                    invalidBytes++;
                    break;
                }

                byte next = data[i + 1];
                if ((next >= 0x40 && next <= 0x7E) || (next >= 0x80 && next <= 0xFC))
                {
                    validDoubleBytes++;
                    i += 2;
                    continue;
                }
                else
                {
                    invalidBytes++;
                    i++;
                    continue;
                }
            }

            // Byte không hợp lệ cho Shift-JIS
            invalidBytes++;
            i++;
        }

        return validDoubleBytes > 0 && invalidBytes == 0;
    }

    /// <summary>
    /// Lấy tên hiển thị thân thiện cho encoding.
    /// </summary>
    public static string GetDisplayName(Encoding encoding)
    {
        return encoding.CodePage switch
        {
            932 => "Shift-JIS (CP932)",
            65001 => "UTF-8",
            1200 => "UTF-16 LE",
            1201 => "UTF-16 BE",
            _ => encoding.EncodingName
        };
    }
}
