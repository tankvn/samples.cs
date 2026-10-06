using PrinterHub.Core.Util;
using System.Text;
using PrinterHub.Core.Model;

namespace PrinterHub.Core;

/// <summary>
/// Đường truyền vật lý/logic tới máy in: TCP raw, SATO Status4, LPR, Spooler RAW, Serial, UNC...
/// Một instance = một phiên kết nối (mở → gửi → đóng).
/// </summary>
public interface IPrinterTransport : IAsyncDisposable
{
    /// <summary>Loại route ("tcp", "sato4", "lpr", "spooler"...).</summary>
    string Kind { get; }

    /// <summary>Mô tả ngắn để log (vd "tcp://192.168.1.50:9100").</summary>
    string Description { get; }

    /// <summary>True nếu có thể đọc phản hồi từ máy in.</summary>
    bool IsBidirectional { get; }

    Task OpenAsync(CancellationToken ct = default);

    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

    /// <summary>
    /// Đọc phản hồi. Trả về số byte đọc được, 0 nếu hết thời gian chờ hoặc transport một chiều.
    /// </summary>
    Task<int> ReceiveAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken ct = default);

    /// <summary>Kết thúc job (vd EndDocPrinter với spooler, gửi control file với LPR).</summary>
    Task EndJobAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Gửi lệnh hỏi trạng thái. Mặc định gửi trên cùng kênh; SATO Status4 2 cổng sẽ gửi sang cổng status.
    /// </summary>
    Task SendStatusRequestAsync(ReadOnlyMemory<byte> request, CancellationToken ct = default) => SendAsync(request, ct);
}

/// <summary>
/// Ngôn ngữ lệnh của máy in: SBPL, ZPL, EZPL, PCL/PJL, Raw...
/// </summary>
public interface ICommandLanguage
{
    /// <summary>Tên ngôn ngữ ("SBPL", "ZPL", "EZPL", "RAW"...).</summary>
    string Name { get; }

    /// <summary>Encoding dùng cho văn bản trong lệnh.</summary>
    Encoding TextEncoding { get; set; }

    /// <summary>Có hỗ trợ render LabelDocument không (Raw/PJL thì không).</summary>
    bool CanRenderLabel { get; }

    /// <summary>Chuyển nhãn trung lập thành byte lệnh.</summary>
    byte[] Render(LabelDocument document);

    /// <summary>Lệnh hỏi trạng thái (null nếu ngôn ngữ không hỗ trợ).</summary>
    byte[]? BuildStatusRequest();

    /// <summary>
    /// Thử phân tích phản hồi trạng thái. Trả về false nếu dữ liệu chưa đủ (cần đọc tiếp).
    /// </summary>
    bool TryParseStatus(ReadOnlySpan<byte> response, out PrinterStatus status);

    /// <summary>Đóng gói dữ liệu raw (vd thêm header PJL). Mặc định trả nguyên.</summary>
    byte[] WrapRaw(byte[] data, PrintJob job) => data;
}

/// <summary>Logger đơn giản (không phụ thuộc NuGet).</summary>
public interface IHubLog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
    void Data(string direction, ReadOnlySpan<byte> bytes);
}

public sealed class ConsoleLog(bool showData = false) : IHubLog
{
    public bool ShowData { get; set; } = showData;
    public void Info(string message) => Write(ConsoleColor.Gray, "INF", message);
    public void Warn(string message) => Write(ConsoleColor.Yellow, "WRN", message);
    public void Error(string message, Exception? ex = null) =>
        Write(ConsoleColor.Red, "ERR", ex is null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");
    public void Data(string direction, ReadOnlySpan<byte> bytes)
    {
        if (!ShowData) return;
        Write(ConsoleColor.DarkCyan, direction, $"{bytes.Length} bytes\n{HexDump.Format(bytes, 512)}");
    }
    private static readonly Lock Gate = new();
    private static void Write(ConsoleColor color, string tag, string message)
    {
        lock (Gate)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{tag}] {message}");
            Console.ForegroundColor = old;
        }
    }
}

public sealed class NullLog : IHubLog
{
    public static readonly NullLog Instance = new();
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message, Exception? ex = null) { }
    public void Data(string direction, ReadOnlySpan<byte> bytes) { }
}

/// <summary>Logger chuyển tiếp sang delegate (dùng cho GUI).</summary>
public sealed class DelegateLog(Action<string, string> sink, bool showData = true) : IHubLog
{
    public void Info(string message) => sink("INF", message);
    public void Warn(string message) => sink("WRN", message);
    public void Error(string message, Exception? ex = null) =>
        sink("ERR", ex is null ? message : $"{message}: {ex.Message}");
    public void Data(string direction, ReadOnlySpan<byte> bytes)
    {
        if (showData) sink(direction, $"{bytes.Length} bytes\r\n{HexDump.Format(bytes, 1024).Replace("\n", "\r\n")}");
    }
}
