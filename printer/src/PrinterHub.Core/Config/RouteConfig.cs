using System.Globalization;
using System.Text.Json.Serialization;

namespace PrinterHub.Core.Config;

/// <summary>
/// Cấu hình một đường in. Có thể viết gọn dạng URI:
/// <code>
/// tcp://192.168.1.50:9100
/// sato4://192.168.1.50?data=1024&amp;status=1025
/// lpr://192.168.1.60/queue
/// spooler://SATO CL4NX Plus 305dpi
/// serial://COM3?baud=9600&amp;parity=none&amp;databits=8&amp;stopbits=1&amp;handshake=rtscts
/// file://\\server\SharedPrinter     (hoặc file://C:\out\job.prn)
/// </code>
/// </summary>
public sealed class RouteConfig
{
    public string Kind { get; set; } = "tcp";
    public string? Host { get; set; }
    public int Port { get; set; }
    /// <summary>Cổng trạng thái (SATO Status4 2 cổng).</summary>
    public int StatusPort { get; set; }
    /// <summary>Tên queue (LPR) hoặc tên máy in Windows (spooler).</summary>
    public string? Queue { get; set; }
    /// <summary>Đường dẫn (file/UNC) hoặc tên cổng COM.</summary>
    public string? Path { get; set; }
    public int ConnectTimeoutMs { get; set; } = 3000;
    public int ReadTimeoutMs { get; set; } = 2000;
    /// <summary>Tham số bổ sung (baud, parity, datatype...).</summary>
    public Dictionary<string, string> Options { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public string Display => ToString();

    public string Get(string key, string fallback) => Options.TryGetValue(key, out var v) ? v : fallback;
    public int GetInt(string key, int fallback) =>
        Options.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : fallback;

    public static RouteConfig Parse(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        int sep = uri.IndexOf("://", StringComparison.Ordinal);
        if (sep <= 0) throw new FormatException($"Route không hợp lệ (thiếu 'kind://'): {uri}");

        var r = new RouteConfig { Kind = uri[..sep].Trim().ToLowerInvariant() };
        string rest = uri[(sep + 3)..];

        // Tách query ?a=b&c=d (không áp dụng cho file:// vì đường dẫn có thể chứa '?' hiếm khi; vẫn hỗ trợ)
        int q = rest.IndexOf('?');
        if (q >= 0)
        {
            foreach (var pair in rest[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0) r.Options[Uri.UnescapeDataString(pair[..eq])] = Uri.UnescapeDataString(pair[(eq + 1)..]);
                else r.Options[Uri.UnescapeDataString(pair)] = "true";
            }
            rest = rest[..q];
        }
        rest = Uri.UnescapeDataString(rest);

        switch (r.Kind)
        {
            case "tcp":
            case "raw":
            case "sato4":
            case "ipp":
            case "ftp":
            case "lpr":
            case "lpd":
            {
                if (r.Kind == "raw") r.Kind = "tcp";
                if (r.Kind == "lpd") r.Kind = "lpr";
                string hostPort = rest;
                int slash = rest.IndexOf('/');
                if (slash >= 0)
                {
                    hostPort = rest[..slash];
                    r.Queue = rest[(slash + 1)..];
                }
                (r.Host, int port) = SplitHostPort(hostPort);
                r.Port = port > 0 ? port : DefaultPort(r.Kind);
                if (r.Kind == "sato4")
                {
                    r.Port = r.GetInt("data", r.Port);
                    r.StatusPort = r.GetInt("status", r.Port + 1);
                }
                if (r.Kind == "lpr" && string.IsNullOrEmpty(r.Queue)) r.Queue = "lp";
                break;
            }
            case "spooler":
            case "winprint":
            case "gdi":
            case "passthrough":
                r.Queue = rest;
                break;
            case "serial":
            case "com":
            case "bt":
                r.Kind = "serial";
                r.Path = rest;
                break;
            case "file":
            case "unc":
                r.Kind = "file";
                r.Path = rest;
                break;
            default:
                r.Path = rest;
                break;
        }
        r.ConnectTimeoutMs = r.GetInt("connectTimeout", r.ConnectTimeoutMs);
        r.ReadTimeoutMs = r.GetInt("readTimeout", r.ReadTimeoutMs);
        return r;
    }

    public static int DefaultPort(string kind) => kind switch
    {
        "tcp" => 9100,
        "sato4" => 1024,
        "lpr" => 515,
        "ipp" => 631,
        "ftp" => 21,
        _ => 0,
    };

    private static (string host, int port) SplitHostPort(string s)
    {
        // IPv6 dạng [::1]:9100
        if (s.StartsWith('['))
        {
            int end = s.IndexOf(']');
            string h = s[1..end];
            int p = end + 1 < s.Length && s[end + 1] == ':' ? int.Parse(s[(end + 2)..], CultureInfo.InvariantCulture) : 0;
            return (h, p);
        }
        int colon = s.LastIndexOf(':');
        if (colon > 0 && int.TryParse(s[(colon + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
            return (s[..colon], port);
        return (s, 0);
    }

    public override string ToString() => Kind switch
    {
        "sato4" => $"sato4://{Host}?data={Port}&status={StatusPort}",
        "lpr" => $"lpr://{Host}:{Port}/{Queue}",
        "tcp" or "ipp" or "ftp" => $"{Kind}://{Host}:{Port}{(Queue is null ? "" : "/" + Queue)}",
        "spooler" or "gdi" or "passthrough" or "winprint" => $"{Kind}://{Queue}",
        _ => $"{Kind}://{Path}",
    };
}
