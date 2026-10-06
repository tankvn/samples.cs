using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using PrinterHub.Core;
using PrinterHub.Core.Config;
using PrinterHub.Windows.Native;

namespace PrinterHub.Windows;

/// <summary>
/// Gửi byte thô qua Windows Print Spooler (OpenPrinter → StartDocPrinter(RAW) → WritePrinter).
/// Dùng được với mọi queue (USB, TCP/IP port, LPT, COM, Bluetooth) của driver SATO/ZDesigner/Godex/Seagull,
/// hoặc driver "Generic / Text Only". Một chiều (trừ khi driver hỗ trợ ReadPrinter).
/// Route: <c>spooler://Tên máy in Windows</c>; tuỳ chọn <c>?datatype=RAW</c> (hoặc XPS_PASS, TEXT).
/// ⚠ Driver v4 / IPP class driver có thể từ chối datatype RAW → dùng TCP trực tiếp hoặc queue Generic/Text Only.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SpoolerRawTransport(RouteConfig route, IHubLog log) : IPrinterTransport
{
    private IntPtr _handle;
    private bool _docStarted;

    public string Kind => "spooler";
    public string Description => $"spooler://{route.Queue}";
    public bool IsBidirectional => route.Get("bidi", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

    public Task OpenAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(route.Queue)) throw new ArgumentException("Thiếu tên máy in Windows.");
        if (!WinSpool.OpenPrinter(route.Queue, out _handle, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"OpenPrinter('{route.Queue}') thất bại");
        log.Info($"Spooler: mở '{route.Queue}'");
        return Task.CompletedTask;
    }

    private void EnsureDoc()
    {
        if (_docStarted) return;
        var di = new WinSpool.DOC_INFO_1
        {
            pDocName = route.Get("job", "PrinterHub RAW"),
            pDatatype = route.Get("datatype", "RAW"),
        };
        if (WinSpool.StartDocPrinter(_handle, 1, ref di) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "StartDocPrinter thất bại (driver có thể không nhận RAW)");
        if (!WinSpool.StartPagePrinter(_handle))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "StartPagePrinter thất bại");
        _docStarted = true;
    }

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        EnsureDoc();
        unsafe
        {
            using var pin = data.Pin();
            int offset = 0;
            while (offset < data.Length)
            {
                if (!WinSpool.WritePrinter(_handle, (IntPtr)((byte*)pin.Pointer + offset), data.Length - offset, out int written))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "WritePrinter thất bại");
                if (written <= 0) throw new IOException("WritePrinter không ghi được byte nào.");
                offset += written;
            }
        }
        return Task.CompletedTask;
    }

    public Task<int> ReceiveAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken ct = default)
    {
        if (!IsBidirectional) return Task.FromResult(0);
        unsafe
        {
            using var pin = buffer.Pin();
            return Task.FromResult(WinSpool.ReadPrinter(_handle, (IntPtr)pin.Pointer, buffer.Length, out int read) ? read : 0);
        }
    }

    public Task EndJobAsync(CancellationToken ct = default)
    {
        if (_docStarted)
        {
            WinSpool.EndPagePrinter(_handle);
            WinSpool.EndDocPrinter(_handle);
            _docStarted = false;
            log.Info($"Spooler: đã đưa job vào hàng đợi '{route.Queue}'");
        }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await EndJobAsync();
        if (_handle != IntPtr.Zero)
        {
            WinSpool.ClosePrinter(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
