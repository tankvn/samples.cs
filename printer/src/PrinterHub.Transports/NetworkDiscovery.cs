using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using PrinterHub.Core;

namespace PrinterHub.Transports;

public sealed record DiscoveredPrinter(string Host, IReadOnlyList<int> OpenPorts, string? Banner)
{
    /// <summary>Đoán hãng/route theo cổng mở.</summary>
    public string Guess
    {
        get
        {
            var hints = new List<string>();
            if (OpenPorts.Contains(1024) && OpenPorts.Contains(1025)) hints.Add("SATO Status4 (sato4://)");
            if (OpenPorts.Contains(9200)) hints.Add("Zebra Link-OS (JSON 9200)");
            if (OpenPorts.Contains(6101)) hints.Add("Zebra (6101)");
            if (OpenPorts.Contains(9100)) hints.Add("RAW 9100");
            if (OpenPorts.Contains(631)) hints.Add("IPP");
            if (OpenPorts.Contains(515)) hints.Add("LPD");
            if (Banner is not null) hints.Add(Banner);
            return string.Join(", ", hints);
        }
    }
}

/// <summary>
/// Quét mạng LAN tìm máy in bằng cách thử kết nối các cổng in phổ biến.
/// (mDNS / SNMP / WS-Discovery sẽ bổ sung ở giai đoạn sau.)
/// </summary>
public static class NetworkDiscovery
{
    public static readonly int[] DefaultPorts = [9100, 1024, 1025, 515, 631, 6101, 9200];

    /// <summary>Quét subnet dạng "192.168.1" (1..254) hoặc danh sách host.</summary>
    public static async Task<List<DiscoveredPrinter>> ScanAsync(
        IEnumerable<string> hosts, IReadOnlyList<int>? ports = null, int timeoutMs = 300,
        int parallelism = 64, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        ports ??= DefaultPorts;
        var results = new System.Collections.Concurrent.ConcurrentBag<DiscoveredPrinter>();
        await Parallel.ForEachAsync(hosts, new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
            async (host, token) =>
            {
                var open = new List<int>();
                foreach (int port in ports)
                    if (await IsOpenAsync(host, port, timeoutMs, token)) open.Add(port);
                if (open.Count > 0)
                {
                    string? banner = await TryHttpTitleAsync(host, timeoutMs * 3, token);
                    var p = new DiscoveredPrinter(host, open, banner);
                    results.Add(p);
                    progress?.Report($"{host}: {string.Join(",", open)} {p.Guess}");
                }
            });
        return results.OrderBy(r => IPAddress.TryParse(r.Host, out var ip) ? ip.GetAddressBytes()[^1] : 0).ToList();
    }

    public static IEnumerable<string> Subnet(string prefix, int from = 1, int to = 254) =>
        Enumerable.Range(from, to - from + 1).Select(i => $"{prefix.TrimEnd('.')}.{i}");

    /// <summary>Lấy các prefix /24 của card mạng đang hoạt động (vd "192.168.1").</summary>
    public static IEnumerable<string> LocalSubnets() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => string.Join('.', a.Address.ToString().Split('.')[..3]))
            .Distinct();

    public static async Task<bool> IsOpenAsync(string host, int port, int timeoutMs, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        try
        {
            await client.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Đọc &lt;title&gt; trang web cấu hình (WebConfig SATO, Zebra, Godex...) để nhận diện model.</summary>
    private static async Task<string?> TryHttpTitleAsync(string host, int timeoutMs, CancellationToken ct)
    {
        try
        {
            if (!await IsOpenAsync(host, 80, timeoutMs, ct)) return null;
            using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(Math.Max(timeoutMs, 1500)) };
            string html = await http.GetStringAsync($"http://{host}/", ct);
            int a = html.IndexOf("<title>", StringComparison.OrdinalIgnoreCase);
            int b = a >= 0 ? html.IndexOf("</title>", a, StringComparison.OrdinalIgnoreCase) : -1;
            return a >= 0 && b > a ? html[(a + 7)..b].Trim() : null;
        }
        catch { return null; }
    }
}
