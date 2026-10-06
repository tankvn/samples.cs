using System.Diagnostics;
using PrinterHub.Core.Config;
using PrinterHub.Core.Model;

namespace PrinterHub.Core;

public sealed class PrinterNotReadyException(PrinterStatus status)
    : Exception($"Máy in không sẵn sàng: {status}")
{
    public PrinterStatus Status { get; } = status;
}

/// <summary>
/// Dịch vụ in: render job bằng ngôn ngữ lệnh, gửi qua route, kiểm tra trạng thái, retry và failover sang route dự phòng.
/// </summary>
public sealed class PrinterService(HubContext context)
{
    private IHubLog Log => context.Log;

    /// <summary>In theo hồ sơ máy in: thử lần lượt từng route (failover), mỗi route retry theo cấu hình.</summary>
    public async Task<PrintResult> PrintAsync(PrinterProfile profile, PrintJob job, CancellationToken ct = default)
    {
        var language = context.Languages.Create(profile);
        var routes = profile.ParseRoutes().ToList();
        if (routes.Count == 0) throw new InvalidOperationException($"Profile '{profile.Name}' chưa có route.");

        PrintResult? last = null;
        int totalAttempts = 0;
        foreach (var route in routes)
        {
            last = await PrintAsync(route, language, job, profile.RetryCount, profile.RetryDelayMs, ct);
            totalAttempts += last.Attempts;
            if (last.Success) return last with { Attempts = totalAttempts };
            Log.Warn($"Route {route} thất bại → chuyển route tiếp theo (nếu có).");
        }
        return last! with { Attempts = totalAttempts };
    }

    /// <summary>In qua một route cụ thể với retry.</summary>
    public async Task<PrintResult> PrintAsync(RouteConfig route, ICommandLanguage language, PrintJob job,
        int retryCount = 0, int retryDelayMs = 1000, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        byte[] payload = Render(language, job);
        string? lastError = null;

        for (int attempt = 1; attempt <= retryCount + 1; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var transport = context.Transports.Create(route, Log);
                Log.Info($"[{job.Name}] Lần {attempt}: mở {transport.Description} ({language.Name}, {payload.Length} bytes)");
                await transport.OpenAsync(ct);

                PrinterStatus? before = null;
                if (job.CheckStatusBeforePrint && transport.IsBidirectional && language.BuildStatusRequest() is not null)
                {
                    before = await ReadStatusAsync(transport, language, TimeSpan.FromMilliseconds(route.ReadTimeoutMs), ct);
                    Log.Info($"[{job.Name}] Trạng thái trước in: {before}");
                    if (before.IsError) throw new PrinterNotReadyException(before);
                }

                Log.Data("TX", payload);
                await transport.SendAsync(payload, ct);
                await transport.EndJobAsync(ct);

                Log.Info($"[{job.Name}] Đã gửi {payload.Length} bytes qua {transport.Description}");
                return new PrintResult
                {
                    Success = true, Route = transport.Description, BytesSent = payload.Length,
                    Attempts = attempt, StatusBefore = before, Elapsed = sw.Elapsed,
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                lastError = ex is PrinterNotReadyException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
                Log.Error($"[{job.Name}] Lần {attempt} lỗi qua {route}", ex);
                if (attempt <= retryCount) await Task.Delay(retryDelayMs * attempt, ct);
            }
        }
        return new PrintResult { Success = false, Route = route.ToString(), Attempts = retryCount + 1, Error = lastError, Elapsed = sw.Elapsed };
    }

    /// <summary>Render job thành byte (nhãn → ngôn ngữ lệnh, raw → WrapRaw).</summary>
    public static byte[] Render(ICommandLanguage language, PrintJob job)
    {
        if (job.Label is { } label)
        {
            if (!language.CanRenderLabel)
                throw new NotSupportedException($"Ngôn ngữ {language.Name} không render được nhãn, hãy gửi raw.");
            return language.Render(label);
        }
        if (job.RawData is { } raw) return language.WrapRaw(raw, job);
        throw new ArgumentException("PrintJob phải có Label hoặc RawData.");
    }

    /// <summary>Mở route, hỏi trạng thái rồi đóng.</summary>
    public async Task<PrinterStatus> GetStatusAsync(RouteConfig route, ICommandLanguage language, CancellationToken ct = default)
    {
        await using var transport = context.Transports.Create(route, Log);
        if (!transport.IsBidirectional) return PrinterStatus.Unknown($"Route {route.Kind} là một chiều, không đọc được trạng thái.");
        await transport.OpenAsync(ct);
        return await ReadStatusAsync(transport, language, TimeSpan.FromMilliseconds(route.ReadTimeoutMs), ct);
    }

    /// <summary>Gửi lệnh hỏi trạng thái trên transport đã mở và đọc đến khi phân tích được hoặc hết giờ.</summary>
    public async Task<PrinterStatus> ReadStatusAsync(IPrinterTransport transport, ICommandLanguage language, TimeSpan timeout, CancellationToken ct = default)
    {
        byte[]? request = language.BuildStatusRequest();
        if (request is null) return PrinterStatus.Unknown($"{language.Name} không có lệnh hỏi trạng thái.");

        Log.Data("TX", request);
        await transport.SendStatusRequestAsync(request, ct);

        var collected = new List<byte>();
        var buffer = new byte[1024];
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var remain = deadline - DateTime.UtcNow;
            int n = await transport.ReceiveAsync(buffer, remain > TimeSpan.Zero ? remain : TimeSpan.FromMilliseconds(1), ct);
            if (n <= 0) break;
            collected.AddRange(buffer.AsSpan(0, n));
            if (language.TryParseStatus(collected.ToArray(), out var status))
            {
                Log.Data("RX", collected.ToArray());
                return status;
            }
        }
        if (collected.Count > 0) Log.Data("RX", collected.ToArray());
        return PrinterStatus.Unknown(collected.Count == 0 ? "Không có phản hồi (timeout)." : $"Phản hồi không hiểu ({collected.Count} bytes).");
    }

    /// <summary>Gửi byte tuỳ ý và thu toàn bộ phản hồi trong khoảng thời gian chờ (dùng cho console lệnh raw).</summary>
    public async Task<byte[]> SendAndReceiveAsync(RouteConfig route, byte[] data, TimeSpan wait, CancellationToken ct = default)
    {
        await using var transport = context.Transports.Create(route, Log);
        await transport.OpenAsync(ct);
        Log.Data("TX", data);
        await transport.SendAsync(data, ct);
        await transport.EndJobAsync(ct);
        if (!transport.IsBidirectional || wait <= TimeSpan.Zero) return [];

        var collected = new List<byte>();
        var buffer = new byte[4096];
        var deadline = DateTime.UtcNow + wait;
        while (DateTime.UtcNow < deadline)
        {
            int n = await transport.ReceiveAsync(buffer, deadline - DateTime.UtcNow, ct);
            if (n <= 0) break;
            collected.AddRange(buffer.AsSpan(0, n));
        }
        if (collected.Count > 0) Log.Data("RX", collected.ToArray());
        return collected.ToArray();
    }
}
