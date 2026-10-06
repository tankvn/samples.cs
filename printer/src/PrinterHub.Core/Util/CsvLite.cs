using System.Text;

namespace PrinterHub.Core.Util;

/// <summary>
/// Đọc CSV đơn giản (RFC 4180: dấu ngoặc kép, xuống dòng trong ô). Tự phát hiện encoding UTF-8/Shift-JIS
/// và dấu phân cách (, ; tab). Dùng để in hàng loạt: mỗi dòng → một bộ biến {Cột}.
/// </summary>
public static class CsvLite
{
    public static List<Dictionary<string, string>> ReadFile(string path)
    {
        var (text, _) = TextEncodings.ReadAllTextAuto(path);
        return ReadRecords(text);
    }

    public static List<Dictionary<string, string>> ReadRecords(string text)
    {
        char delimiter = DetectDelimiter(text);
        var rows = Parse(text, delimiter);
        if (rows.Count == 0) return [];
        var header = rows[0].Select(h => h.Trim()).ToArray();
        var result = new List<Dictionary<string, string>>();
        foreach (var row in rows.Skip(1))
        {
            if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])) continue;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++) dict[header[i]] = i < row.Count ? row[i] : "";
            result.Add(dict);
        }
        return result;
    }

    public static char DetectDelimiter(string text)
    {
        string firstLine = text.Split('\n', 2)[0];
        char[] candidates = [',', ';', '\t', '|'];
        return candidates.OrderByDescending(c => firstLine.Count(ch => ch == c)).First();
    }

    public static List<List<string>> Parse(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }
            if (c == '"') inQuotes = true;
            else if (c == delimiter) { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\r') { }
            else if (c == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row); row = []; }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
