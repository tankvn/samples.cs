namespace CsvReader;

/// <summary>
/// Phân tích nội dung CSV, hỗ trợ:
/// - Dấu phân cách tùy chọn (comma, tab, semicolon)
/// - Quoted fields (RFC 4180)
/// - Multi-line fields trong dấu ngoặc kép
/// </summary>
public static class CsvParser
{
    /// <summary>
    /// Phân tích chuỗi CSV thành danh sách các dòng, mỗi dòng là danh sách các cột.
    /// </summary>
    public static List<string[]> Parse(string content, char delimiter = ',')
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
    /// Phân tích một field, trả về (giá trị, vị trí tiếp theo, có phải cuối dòng không).
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
            var sb = new System.Text.StringBuilder();

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
            var sb = new System.Text.StringBuilder();

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
    /// Tự động phát hiện dấu phân cách phổ biến nhất.
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
}
