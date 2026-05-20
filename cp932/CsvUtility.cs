using System.Text;

namespace CsvReader;

/// <summary>
/// Class tiện ích tổng hợp cho việc đọc file CSV.
/// Bao gồm: phát hiện encoding, phát hiện delimiter, phân tích CSV.
/// Hỗ trợ: UTF-8 (BOM / no BOM), UTF-16 LE/BE, Shift-JIS (CP932).
/// </summary>
public static class CsvUtility
{
    // ================================================================
    //  1. ENCODING DETECTION — Phát hiện encoding
    // ================================================================

    /// <summary>
    /// Phát hiện encoding của file dựa trên BOM và phân tích byte pattern.
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
    /// Phát hiện số byte BOM ở đầu file (không phân tích encoding).
    /// </summary>
    public static int DetectBomLength(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) return 3;
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) return 2;
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF) return 2;
        return 0;
    }

    /// <summary>
    /// Lấy tên hiển thị thân thiện cho encoding.
    /// </summary>
    public static string GetEncodingDisplayName(Encoding encoding)
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

    // ================================================================
    //  2. CSV PARSING — Phân tích nội dung CSV
    // ================================================================

    /// <summary>
    /// Phân tích chuỗi CSV thành danh sách các dòng, mỗi dòng là mảng các cột.
    /// Hỗ trợ: quoted fields (RFC 4180), multi-line fields, escaped quotes.
    /// </summary>
    public static List<string[]> ParseCsv(string content, char delimiter = ',')
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        int i = 0;

        while (i < content.Length)
        {
            // Bỏ qua BOM nếu có
            if (i == 0 && content[0] == '\uFEFF')
            {
                i++;
                continue;
            }

            var (field, nextIndex, endOfRow) = ParseField(content, i, delimiter);
            fields.Add(field);
            i = nextIndex;

            if (endOfRow)
            {
                rows.Add(fields.ToArray());
                fields.Clear();
            }
        }

        // Thêm dòng cuối nếu còn fields
        if (fields.Count > 0)
        {
            rows.Add(fields.ToArray());
        }

        return rows;
    }

    /// <summary>
    /// Tự động phát hiện dấu phân cách phổ biến nhất trong nội dung CSV.
    /// Kiểm tra: comma, tab, semicolon, pipe.
    /// </summary>
    public static char DetectDelimiter(string content)
    {
        char[] candidates = { ',', '\t', ';', '|' };
        int maxCount = 0;
        char bestDelimiter = ',';

        // Chỉ kiểm tra vài dòng đầu
        var lines = content.Split('\n', 10);

        foreach (var delim in candidates)
        {
            int count = 0;
            foreach (var line in lines)
            {
                count += line.Count(c => c == delim);
            }

            if (count > maxCount)
            {
                maxCount = count;
                bestDelimiter = delim;
            }
        }

        return bestDelimiter;
    }

    /// <summary>
    /// Lấy tên hiển thị thân thiện cho delimiter.
    /// </summary>
    public static string GetDelimiterDisplayName(char delimiter)
    {
        return delimiter switch
        {
            ',' => "Comma (,)",
            '\t' => "Tab (\\t)",
            ';' => "Semicolon (;)",
            '|' => "Pipe (|)",
            _ => delimiter.ToString()
        };
    }

    // ================================================================
    //  3. HIGH-LEVEL API — Đọc file CSV hoàn chỉnh
    // ================================================================

    /// <summary>
    /// Đọc file CSV từ đường dẫn, tự động phát hiện encoding và delimiter.
    /// Trả về (danh sách dòng, encoding, delimiter).
    /// </summary>
    public static (List<string[]> rows, Encoding encoding, char delimiter) ReadFile(string filePath)
    {
        byte[] data = File.ReadAllBytes(filePath);
        return ReadFromBytes(data);
    }

    /// <summary>
    /// Đọc CSV từ byte array, tự động phát hiện encoding và delimiter.
    /// Trả về (danh sách dòng, encoding, delimiter).
    /// </summary>
    public static (List<string[]> rows, Encoding encoding, char delimiter) ReadFromBytes(byte[] data)
    {
        var (encoding, bomLength) = DetectEncoding(data);
        return ReadFromBytes(data, encoding, bomLength);
    }

    /// <summary>
    /// Đọc CSV từ byte array với encoding chỉ định.
    /// Trả về (danh sách dòng, encoding, delimiter).
    /// </summary>
    public static (List<string[]> rows, Encoding encoding, char delimiter) ReadFromBytes(
        byte[] data, Encoding encoding, int bomLength = -1)
    {
        if (bomLength < 0)
            bomLength = DetectBomLength(data);

        string content = encoding.GetString(data, bomLength, data.Length - bomLength);
        char delimiter = DetectDelimiter(content);
        var rows = ParseCsv(content, delimiter);

        return (rows, encoding, delimiter);
    }

    /// <summary>
    /// Chuyển đổi file CSV sang UTF-8 và lưu ra file mới.
    /// </summary>
    public static void ConvertToUtf8(string inputPath, string outputPath)
    {
        byte[] data = File.ReadAllBytes(inputPath);
        var (encoding, bomLength) = DetectEncoding(data);
        string content = encoding.GetString(data, bomLength, data.Length - bomLength);
        File.WriteAllText(outputPath, content, new UTF8Encoding(true));
    }

    /// <summary>
    /// Chuyển đổi byte array sang UTF-8 string, tự động phát hiện encoding.
    /// </summary>
    public static string DecodeToString(byte[] data)
    {
        var (encoding, bomLength) = DetectEncoding(data);
        return encoding.GetString(data, bomLength, data.Length - bomLength);
    }

    /// <summary>
    /// Chuyển đổi byte array sang UTF-8 string với encoding chỉ định.
    /// </summary>
    public static string DecodeToString(byte[] data, Encoding encoding)
    {
        int bomLength = DetectBomLength(data);
        return encoding.GetString(data, bomLength, data.Length - bomLength);
    }

    // ================================================================
    //  PRIVATE HELPERS — Các hàm hỗ trợ nội bộ
    // ================================================================

    /// <summary>
    /// Phân tích một field CSV, trả về (giá trị, vị trí tiếp theo, có phải cuối dòng không).
    /// </summary>
    private static (string value, int nextIndex, bool endOfRow) ParseField(
        string content, int start, char delimiter)
    {
        if (start >= content.Length)
            return (string.Empty, start, true);

        bool quoted = content[start] == '"';
        int i;

        if (quoted)
        {
            // Field bắt đầu bằng dấu ngoặc kép
            i = start + 1;
            var sb = new StringBuilder();

            while (i < content.Length)
            {
                if (content[i] == '"')
                {
                    // Kiểm tra escaped quote ("")
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        sb.Append('"');
                        i += 2;
                    }
                    else
                    {
                        // Kết thúc quoted field
                        i++; // bỏ qua dấu ngoặc kép đóng
                        break;
                    }
                }
                else
                {
                    sb.Append(content[i]);
                    i++;
                }
            }

            // Bỏ qua khoảng trắng sau quoted field
            while (i < content.Length && content[i] != delimiter
                   && content[i] != '\r' && content[i] != '\n')
            {
                i++;
            }

            // Xác định kết thúc
            if (i >= content.Length)
                return (sb.ToString(), i, true);

            if (content[i] == delimiter)
                return (sb.ToString(), i + 1, false);

            // Newline
            if (content[i] == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                return (sb.ToString(), i + 2, true);

            return (sb.ToString(), i + 1, true);
        }
        else
        {
            // Field không có dấu ngoặc kép
            i = start;
            var sb = new StringBuilder();

            while (i < content.Length && content[i] != delimiter
                   && content[i] != '\r' && content[i] != '\n')
            {
                sb.Append(content[i]);
                i++;
            }

            if (i >= content.Length)
                return (sb.ToString(), i, true);

            if (content[i] == delimiter)
                return (sb.ToString(), i + 1, false);

            // Newline
            if (content[i] == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                return (sb.ToString(), i + 2, true);

            return (sb.ToString(), i + 1, true);
        }
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
    /// Kiểm tra xem có byte multi-byte UTF-8 không (tiếng Việt, tiếng Nhật, v.v.)
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
}
