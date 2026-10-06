using System.Net.Sockets;
using PrinterHub.Core;
using PrinterHub.Core.Config;

namespace PrinterHub.Transports;

/// <summary>
/// Gửi raw qua TCP (JetDirect / port 9100, SATO port 1024/9100, Zebra 9100/6101, Godex 9100...).
/// Hai chiều: đọc phản hồi trên cùng socket (~HS, ENQ, ~S,CHECK, @PJL INFO).
/// </summary>
public class TcpRawTransport(RouteConfig route, IHubLog log) : IPrinterTransport
{
    protected readonly RouteConfig Route = route;
    protected readonly IHubLog Log = log;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public virtual string Kind => "tcp";
    public virtual string Description => $"tcp://{Route.Host}:{Route.Port}";
    public virtual bool IsBidirectional => true;

    public virtual async Task OpenAsync(CancellationToken ct = default)
    {
        (_client, _stream) = await ConnectAsync(Route.Host!, Route.Port, ct);
    }

    protected async Task<(TcpClient, NetworkStream)> ConnectAsync(string host, int port, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Thiếu host.");
        var client = new TcpClient { NoDelay = true };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Route.ConnectTimeoutMs);
        try
        {
            await client.ConnectAsync(host, port, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            client.Dispose();
            throw new TimeoutException($"Không kết nối được {host}:{port} sau {Route.ConnectTimeoutMs} ms");
        }
        catch
        {
            client.Dispose();
            throw;
        }
        Log.Info($"Đã kết nối {host}:{port}");
        return (client, client.GetStream());
    }

    public virtual async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var s = _stream ?? throw new InvalidOperationException("Chưa OpenAsync.");
        await s.WriteAsync(data, ct);
        await s.FlushAsync(ct);
    }

    public virtual Task SendStatusRequestAsync(ReadOnlyMemory<byte> request, CancellationToken ct = default)
        => SendAsync(request, ct);

    public virtual Task EndJobAsync(CancellationToken ct = default) => Task.CompletedTask;

    public virtual Task<int> ReceiveAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken ct = default)
        => ReadWithTimeoutAsync(_stream ?? throw new InvalidOperationException("Chưa OpenAsync."), buffer, timeout, ct);

    protected static async Task<int> ReadWithTimeoutAsync(NetworkStream stream, Memory<byte> buffer, TimeSpan timeout, CancellationToken ct)
    {
        if (timeout <= TimeSpan.Zero) return 0;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            return await stream.ReadAsync(buffer, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return 0; // hết thời gian chờ
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_stream is not null)
        {
            try
            {
                // Chờ ngắn để máy in nhận hết dữ liệu trước khi đóng (một số máy cắt job nếu RST quá sớm)
                _client?.Client.Shutdown(SocketShutdown.Send);
                await Task.Delay(Route.GetInt("closeDelay", 50));
            }
            catch { /* bỏ qua */ }
            await _stream.DisposeAsync();
        }
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// SATO Status4 hai cổng: Port1 (data, mặc định 1024) nhận lệnh in, Port2 (status, mặc định 1025)
/// nhận ENQ và trả trạng thái. Cho phép theo dõi trạng thái trong lúc máy in đang nhận/in dữ liệu.
/// Route: <c>sato4://192.168.1.50?data=1024&amp;status=1025</c>.
/// Với Status4 1 cổng (ENQ) hãy dùng <c>tcp://host:9100</c> + ngôn ngữ SBPL.
/// </summary>
public sealed class SatoStatus4Transport(RouteConfig route, IHubLog log) : TcpRawTransport(route, log)
{
    private TcpClient? _statusClient;
    private NetworkStream? _statusStream;

    public override string Kind => "sato4";
    public override string Description => $"sato4://{Route.Host}?data={Route.Port}&status={Route.StatusPort}";

    public override async Task OpenAsync(CancellationToken ct = default)
    {
        // Mở cổng status trước để đọc được trạng thái kể cả khi cổng data bận
        (_statusClient, _statusStream) = await ConnectAsync(Route.Host!, Route.StatusPort, ct);
        await base.OpenAsync(ct);
    }

    public override async Task SendStatusRequestAsync(ReadOnlyMemory<byte> request, CancellationToken ct = default)
    {
        var s = _statusStream ?? throw new InvalidOperationException("Chưa OpenAsync.");
        await s.WriteAsync(request, ct);
        await s.FlushAsync(ct);
    }

    /// <summary>Đọc từ cổng status.</summary>
    public override Task<int> ReceiveAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken ct = default)
        => ReadWithTimeoutAsync(_statusStream ?? throw new InvalidOperationException("Chưa OpenAsync."), buffer, timeout, ct);

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_statusStream is not null) await _statusStream.DisposeAsync();
        _statusClient?.Dispose();
    }
}
