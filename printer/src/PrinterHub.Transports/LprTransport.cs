using System.Net;
using System.Net.Sockets;
using System.Text;
using PrinterHub.Core;
using PrinterHub.Core.Config;

namespace PrinterHub.Transports;

/// <summary>
/// LPR/LPD (RFC 1179) qua TCP 515. Hỗ trợ bởi SATO, Zebra, FUJIFILM, hầu hết máy in mạng.
/// Dữ liệu được gom lại và gửi khi EndJobAsync (vì LPR cần biết độ dài file trước).
/// Route: <c>lpr://host[:515]/queue</c>; tuỳ chọn <c>?type=l</c> (l = raw, f = text, o = PostScript).
/// </summary>
public sealed class LprTransport(RouteConfig route, IHubLog log) : IPrinterTransport
{
    private static int _jobCounter = Random.Shared.Next(0, 1000);
    private readonly MemoryStream _buffer = new();

    public string Kind => "lpr";
    public string Description => $"lpr://{route.Host}:{route.Port}/{route.Queue}";
    public bool IsBidirectional => false;

    public Task OpenAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        _buffer.Write(data.Span);
        return Task.CompletedTask;
    }

    public Task<int> ReceiveAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken ct = default) => Task.FromResult(0);

    public async Task EndJobAsync(CancellationToken ct = default)
    {
        if (_buffer.Length == 0) return;
        byte[] data = _buffer.ToArray();
        _buffer.SetLength(0);

        string localHost = Sanitize(Dns.GetHostName(), 31);
        string user = Sanitize(Environment.UserName, 31);
        int jobNo = Interlocked.Increment(ref _jobCounter) % 1000;
        string dfName = $"dfA{jobNo:D3}{localHost}";
        string cfName = $"cfA{jobNo:D3}{localHost}";
        string type = route.Get("type", "l");
        string jobName = route.Get("job", "PrinterHub");

        string control =
            $"H{localHost}\n" +
            $"P{user}\n" +
            $"J{jobName}\n" +
            $"N{jobName}\n" +
            $"{type}{dfName}\n" +
            $"U{dfName}\n";
        byte[] controlBytes = Encoding.ASCII.GetBytes(control);

        using var client = new TcpClient { NoDelay = true };
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            cts.CancelAfter(route.ConnectTimeoutMs);
            await client.ConnectAsync(route.Host!, route.Port, cts.Token);
        }
        await using var s = client.GetStream();

        // 02 queue LF : nhận job
        await Command(s, $"\x02{route.Queue}\n", "receive job", ct);
        // 02 count SP cfName LF : control file
        await Command(s, $"\x02{controlBytes.Length} {cfName}\n", "control file header", ct);
        await s.WriteAsync(controlBytes, ct);
        await WriteZeroAndAck(s, "control file", ct);
        // 03 count SP dfName LF : data file
        await Command(s, $"\x03{data.Length} {dfName}\n", "data file header", ct);
        await s.WriteAsync(data, ct);
        await WriteZeroAndAck(s, "data file", ct);

        log.Info($"LPR: đã gửi {data.Length} bytes tới queue '{route.Queue}'");
    }

    private async Task Command(NetworkStream s, string cmd, string what, CancellationToken ct)
    {
        await s.WriteAsync(Encoding.ASCII.GetBytes(cmd), ct);
        await ReadAck(s, what, ct);
    }

    private async Task WriteZeroAndAck(NetworkStream s, string what, CancellationToken ct)
    {
        await s.WriteAsync(new byte[] { 0 }, ct);
        await ReadAck(s, what, ct);
    }

    private async Task ReadAck(NetworkStream s, string what, CancellationToken ct)
    {
        var buf = new byte[1];
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(route.ReadTimeoutMs * 5);
        int n = await s.ReadAsync(buf, cts.Token);
        if (n != 1 || buf[0] != 0)
            throw new IOException($"LPD từ chối ({what}): ack={(n == 1 ? buf[0] : -1)}");
    }

    private static string Sanitize(string s, int max)
    {
        var chars = s.Where(c => c is > ' ' and < (char)0x7F).Take(max).ToArray();
        return chars.Length == 0 ? "host" : new string(chars);
    }

    public ValueTask DisposeAsync()
    {
        _buffer.Dispose();
        return ValueTask.CompletedTask;
    }
}
