using PrinterHub.Core;
using PrinterHub.Core.Config;

namespace PrinterHub.Transports;

/// <summary>
/// Ghi byte vào một đường dẫn:
/// <list type="bullet">
/// <item>Máy in chia sẻ Windows: <c>file://\\PC01\SATO_CL4NX</c> (tương đương <c>copy /b job.prn \\PC01\SATO_CL4NX</c>).</item>
/// <item>Cổng thiết bị: <c>file://\\.\LPT1</c>, <c>file://\\.\COM1</c> (không cấu hình baud).</item>
/// <item>File để kiểm tra: <c>file://C:\out\job.prn</c> — thêm <c>?append=true</c> để nối thêm.</item>
/// </list>
/// Với máy in chia sẻ, queue phải chấp nhận RAW (driver Generic/Text Only hoặc driver hãng).
/// </summary>
public sealed class FileTransport(RouteConfig route, IHubLog log) : IPrinterTransport
{
    private FileStream? _fs;

    public string Kind => "file";
    public string Description => $"file://{route.Path}";
    public bool IsBidirectional => false;

    public Task OpenAsync(CancellationToken ct = default)
    {
        string path = route.Path ?? throw new ArgumentException("Thiếu đường dẫn.");
        bool append = route.Get("append", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        bool isDevice = path.StartsWith(@"\\.\", StringComparison.Ordinal);
        if (!isDevice && !path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }
        _fs = new FileStream(path, append ? FileMode.Append
            : isDevice ? FileMode.Open
            : path.StartsWith(@"\\", StringComparison.Ordinal) ? FileMode.OpenOrCreate
            : FileMode.Create,
            FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.None);
        log.Info($"Mở {path}");
        return Task.CompletedTask;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var fs = _fs ?? throw new InvalidOperationException("Chưa OpenAsync.");
        await fs.WriteAsync(data, ct);
        await fs.FlushAsync(ct);
    }

    public Task<int> ReceiveAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken ct = default) => Task.FromResult(0);

    public async ValueTask DisposeAsync()
    {
        if (_fs is not null) await _fs.DisposeAsync();
    }
}
