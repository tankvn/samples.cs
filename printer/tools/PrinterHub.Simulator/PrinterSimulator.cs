using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using PrinterHub.Languages.Sato;
using PrinterHub.Languages.Zebra;

namespace PrinterHub.Simulator;

public sealed class SimulatorOptions
{
    public IPAddress Address { get; set; } = IPAddress.Loopback;
    /// <summary>Cổng raw đa năng (SBPL/ZPL/EZPL/PJL). 0 = tắt.</summary>
    public int RawPort { get; set; } = 9100;
    /// <summary>SATO Status4: cổng data. 0 = tắt.</summary>
    public int SatoDataPort { get; set; } = 1024;
    /// <summary>SATO Status4: cổng status. 0 = tắt.</summary>
    public int SatoStatusPort { get; set; } = 1025;
    /// <summary>Cổng LPD (515 cần quyền admin trên Linux; mặc định 5515). 0 = tắt.</summary>
    public int LpdPort { get; set; } = 5515;
    /// <summary>Thư mục lưu job nhận được (null = không lưu).</summary>
    public string? JobsDirectory { get; set; } = "jobs";
    /// <summary>Thời gian in 1 nhãn (ms) để mô phỏng số nhãn còn lại.</summary>
    public int LabelMs { get; set; } = 200;
}

/// <summary>
/// Máy in giả lập cho phát triển/test không cần máy thật:
/// trả lời ENQ (SATO Status4), ~HS (Zebra), ~S,CHECK (Godex), @PJL INFO STATUS; lưu job; nhận LPR.
/// </summary>
public sealed class PrinterSimulator : IAsyncDisposable
{
    private readonly SimulatorOptions _o;
    private readonly List<TcpListener> _listeners = [];
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _tasks = [];
    private int _remaining;
    private int _jobSeq;

    public PrinterSimulator(SimulatorOptions options) => _o = options;

    /// <summary>Job nhận được (đã bỏ lệnh hỏi trạng thái).</summary>
    public ConcurrentQueue<ReceivedJob> Jobs { get; } = new();

    /// <summary>Giả lập lỗi SATO (vd 'c' hết giấy, 'b' mở đầu in). null = bình thường.</summary>
    public char? SatoErrorCode { get; set; }
    public bool ZebraPaperOut { get; set; }
    public bool ZebraHeadOpen { get; set; }
    /// <summary>Mã Godex trả về khi không in (mặc định "00").</summary>
    public string GodexIdleCode { get; set; } = "00";

    public int RemainingLabels => Volatile.Read(ref _remaining);

    public event Action<string>? Log;

    public sealed record ReceivedJob(string Channel, DateTime Time, byte[] Data, string? SavedPath);

    public void Start()
    {
        if (_o.RawPort > 0) Listen(_o.RawPort, "RAW", HandleRawAsync);
        if (_o.SatoDataPort > 0) Listen(_o.SatoDataPort, "SATO-DATA", HandleRawAsync);
        if (_o.SatoStatusPort > 0) Listen(_o.SatoStatusPort, "SATO-STATUS", HandleSatoStatusAsync);
        if (_o.LpdPort > 0) Listen(_o.LpdPort, "LPD", HandleLpdAsync);
        _tasks.Add(Task.Run(PrintLoopAsync));
    }

    public IReadOnlyList<int> BoundPorts => _listeners.Select(l => ((IPEndPoint)l.LocalEndpoint).Port).ToList();

    private void Listen(int port, string name, Func<TcpClient, string, CancellationToken, Task> handler)
    {
        var l = new TcpListener(_o.Address, port);
        l.Start();
        _listeners.Add(l);
        Log?.Invoke($"[{name}] lắng nghe {_o.Address}:{((IPEndPoint)l.LocalEndpoint).Port}");
        _tasks.Add(Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient c;
                try { c = await l.AcceptTcpClientAsync(_cts.Token); }
                catch { break; }
                _ = Task.Run(async () =>
                {
                    using (c)
                    {
                        try { await handler(c, name, _cts.Token); }
                        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException) { }
                        catch (Exception ex) { Log?.Invoke($"[{name}] lỗi: {ex.Message}"); }
                    }
                });
            }
        }));
    }

    // ---------------- Trạng thái ----------------

    private char SatoCode()
    {
        if (SatoErrorCode is { } e) return e;
        return RemainingLabels > 0 ? 'G' : 'A';
    }

    private byte[] SatoStatus4() => SatoStatus.BuildResponse("01", SatoCode(), RemainingLabels, "SIMULATOR");

    private byte[] ZebraHs() => ZebraHostStatus.BuildResponse(ZebraPaperOut, false, ZebraHeadOpen, false, RemainingLabels);

    private byte[] GodexStatus() => Encoding.ASCII.GetBytes((RemainingLabels > 0 ? "50" : GodexIdleCode) + "\r\n");

    private byte[] PjlStatus()
    {
        bool printing = RemainingLabels > 0;
        return Encoding.ASCII.GetBytes(
            $"@PJL INFO STATUS\r\nCODE={(printing ? "10023" : "10001")}\r\nDISPLAY=\"{(printing ? "PRINTING" : "READY")}\"\r\nONLINE=TRUE\r\n\f");
    }

    // ---------------- Kênh raw ----------------

    private async Task HandleRawAsync(TcpClient client, string name, CancellationToken ct)
    {
        var s = client.GetStream();
        var job = new MemoryStream();
        var buf = new byte[8192];
        var pending = new List<byte>(); // phát hiện lệnh hỏi trạng thái bị cắt giữa 2 lần đọc
        int n;
        bool inSbplFrame = false;
        int counted = 0;
        while ((n = await s.ReadAsync(buf, ct)) > 0)
        {
            for (int i = 0; i < n; i++)
            {
                byte b = buf[i];
                if (b == 0x02) inSbplFrame = true;
                else if (b == 0x03) inSbplFrame = false;

                // SATO ENQ ngoài khung STX…ETX
                if (b == 0x05 && !inSbplFrame)
                {
                    await s.WriteAsync(SatoStatus4(), ct);
                    Log?.Invoke($"[{name}] ENQ → '{SatoCode()}' còn {RemainingLabels}");
                    continue;
                }
                if (b == 0x18 && !inSbplFrame) // CAN = huỷ
                {
                    Interlocked.Exchange(ref _remaining, 0);
                    Log?.Invoke($"[{name}] CAN → huỷ job");
                    continue;
                }
                job.WriteByte(b);
                pending.Add(b);
                if (b == '\n' || b == 0x0C)
                {
                    string line = Encoding.ASCII.GetString(pending.ToArray());
                    pending.Clear();
                    byte[]? reply = null;
                    if (line.Contains("~HS")) reply = ZebraHs();
                    else if (line.Contains("~S,CHECK")) reply = GodexStatus();
                    else if (line.Contains("@PJL INFO STATUS")) reply = PjlStatus();
                    if (reply is not null)
                    {
                        // Loại lệnh hỏi trạng thái khỏi dữ liệu job
                        job.SetLength(Math.Max(0, job.Length - Encoding.ASCII.GetByteCount(line)));
                        await s.WriteAsync(reply, ct);
                        Log?.Invoke($"[{name}] {line.Trim()} → trả trạng thái");
                    }
                }
            }
            // Job SBPL/ZPL/EZPL đã đủ → bắt đầu "in"
            counted = CheckJobBoundary(job.ToArray(), counted);
        }
        SaveJob(name, job.ToArray());
    }

    /// <summary>Cộng số nhãn mới phát hiện vào bộ đếm "còn lại". Trả về tổng đã đếm của kết nối.</summary>
    private int CheckJobBoundary(byte[] data, int alreadyCounted)
    {
        int labels = CountLabels(data);
        if (labels > alreadyCounted) Interlocked.Add(ref _remaining, labels - alreadyCounted);
        return Math.Max(labels, alreadyCounted);
    }

    /// <summary>Đếm số nhãn: SBPL ESC Q n, ZPL ^PQn, EZPL ^Pn.</summary>
    public static int CountLabels(byte[] data)
    {
        string s = Encoding.Latin1.GetString(data);
        int total = 0;
        foreach (Match m in Regex.Matches(s, "\x1BQ(\\d{1,6})")) total += int.Parse(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(s, "\\^PQ(\\d+)")) total += int.Parse(m.Groups[1].Value);
        if (total == 0 && s.Contains("^XZ")) total = Regex.Matches(s, "\\^XZ").Count;
        if (total == 0 && Regex.IsMatch(s, "\r\nE\r\n"))
        {
            var m = Regex.Match(s, "\\^P(\\d+)\r\n");
            total = m.Success ? int.Parse(m.Groups[1].Value) : 1;
        }
        return total;
    }

    private void SaveJob(string channel, byte[] data)
    {
        if (data.Length == 0) return;
        string? path = null;
        if (_o.JobsDirectory is { } dir)
        {
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Interlocked.Increment(ref _jobSeq):D4}-{channel}.prn");
            File.WriteAllBytes(path, data);
        }
        Jobs.Enqueue(new ReceivedJob(channel, DateTime.Now, data, path));
        Log?.Invoke($"[{channel}] nhận job {data.Length} bytes{(path is null ? "" : " → " + path)}");
    }

    // ---------------- SATO cổng status ----------------

    private async Task HandleSatoStatusAsync(TcpClient client, string name, CancellationToken ct)
    {
        var s = client.GetStream();
        var buf = new byte[256];
        int n;
        while ((n = await s.ReadAsync(buf, ct)) > 0)
        {
            for (int i = 0; i < n; i++)
            {
                if (buf[i] == 0x05)
                {
                    await s.WriteAsync(SatoStatus4(), ct);
                    Log?.Invoke($"[{name}] ENQ → '{SatoCode()}' còn {RemainingLabels}");
                }
                else if (buf[i] == 0x18)
                {
                    Interlocked.Exchange(ref _remaining, 0);
                }
            }
        }
    }

    // ---------------- LPD (RFC 1179) ----------------

    private async Task HandleLpdAsync(TcpClient client, string name, CancellationToken ct)
    {
        var s = client.GetStream();
        string first = await ReadLineAsync(s, ct);
        if (first.Length == 0 || first[0] != '\x02') { await s.WriteAsync(new byte[] { 1 }, ct); return; }
        string queue = first[1..];
        await s.WriteAsync(new byte[] { 0 }, ct);

        byte[]? dataFile = null;
        while (true)
        {
            string sub = await ReadLineAsync(s, ct);
            if (sub.Length == 0) break;
            char cmd = sub[0];
            var parts = sub[1..].Split(' ', 2);
            int count = int.Parse(parts[0]);
            await s.WriteAsync(new byte[] { 0 }, ct);
            byte[] content = await ReadExactAsync(s, count + 1, ct); // + byte 0 kết thúc
            await s.WriteAsync(new byte[] { 0 }, ct);
            if (cmd == '\x03') dataFile = content[..count];
        }
        if (dataFile is not null)
        {
            CheckJobBoundary(dataFile, 0);
            SaveJob($"LPD-{queue}", dataFile);
        }
    }

    private static async Task<string> ReadLineAsync(NetworkStream s, CancellationToken ct)
    {
        var sb = new List<byte>();
        var one = new byte[1];
        while (await s.ReadAsync(one, ct) == 1)
        {
            if (one[0] == '\n') return Encoding.ASCII.GetString(sb.ToArray());
            sb.Add(one[0]);
        }
        return Encoding.ASCII.GetString(sb.ToArray());
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream s, int count, CancellationToken ct)
    {
        var data = new byte[count];
        await s.ReadExactlyAsync(data, ct);
        return data;
    }

    // ---------------- Mô phỏng in ----------------

    private async Task PrintLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try { await Task.Delay(_o.LabelMs, _cts.Token); } catch { break; }
            if (SatoErrorCode is null && !ZebraPaperOut && !ZebraHeadOpen)
            {
                int cur = Volatile.Read(ref _remaining);
                if (cur > 0) Interlocked.CompareExchange(ref _remaining, cur - 1, cur);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        foreach (var l in _listeners) l.Stop();
        try { await Task.WhenAll(_tasks).WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
    }
}
