using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using PrinterHub.Core;
using PrinterHub.Core.Config;

namespace PrinterHub.Windows;

/// <summary>
/// Cổng COM (RS-232C, USB-Serial, Bluetooth SPP – cổng COM ảo) dùng Win32 trực tiếp (không cần NuGet System.IO.Ports).
/// Route: <c>serial://COM3?baud=9600&amp;parity=none&amp;databits=8&amp;stopbits=1&amp;handshake=rtscts</c>.
/// handshake: none | rtscts | xonxoff | dsrdtr (SATO READY/BUSY ≈ rtscts hoặc dsrdtr tuỳ cáp).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SerialTransport(RouteConfig route, IHubLog log) : IPrinterTransport
{
    private SafeFileHandle? _handle;
    private FileStream? _stream;

    public string Kind => "serial";
    public string Description => $"serial://{route.Path}?baud={route.GetInt("baud", 9600)}";
    public bool IsBidirectional => true;

    public Task OpenAsync(CancellationToken ct = default)
    {
        string port = route.Path ?? throw new ArgumentException("Thiếu tên cổng COM.");
        string path = port.StartsWith(@"\\.\", StringComparison.Ordinal) ? port : @"\\.\" + port;

        _handle = Kernel32.CreateFile(path, Kernel32.GENERIC_READ | Kernel32.GENERIC_WRITE, 0, IntPtr.Zero,
            Kernel32.OPEN_EXISTING, 0, IntPtr.Zero);
        if (_handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Không mở được {port}");

        var dcb = new Kernel32.DCB { DCBlength = (uint)Marshal.SizeOf<Kernel32.DCB>() };
        if (!Kernel32.GetCommState(_handle, ref dcb))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetCommState thất bại");

        dcb.BaudRate = (uint)route.GetInt("baud", 9600);
        dcb.ByteSize = (byte)route.GetInt("databits", 8);
        dcb.Parity = route.Get("parity", "none").ToLowerInvariant() switch
        {
            "odd" => 1, "even" => 2, "mark" => 3, "space" => 4, _ => (byte)0,
        };
        dcb.StopBits = route.Get("stopbits", "1") switch { "1.5" => 1, "2" => 2, _ => (byte)0 };

        // Flags: bit0 fBinary, bit2 fOutxCtsFlow, bit3 fOutxDsrFlow, bits4-5 fDtrControl, bit8 fOutX, bit9 fInX, bits12-13 fRtsControl
        uint flags = 1; // fBinary
        string hs = route.Get("handshake", "none").ToLowerInvariant();
        uint dtrControl = 1, rtsControl = 1; // ENABLE
        if (hs == "rtscts") { flags |= 1u << 2; rtsControl = 2; }       // RTS_CONTROL_HANDSHAKE
        if (hs == "dsrdtr") { flags |= 1u << 3; dtrControl = 2; }       // DTR_CONTROL_HANDSHAKE
        if (hs == "xonxoff") { flags |= (1u << 8) | (1u << 9); dcb.XonChar = 0x11; dcb.XoffChar = 0x13; dcb.XonLim = 2048; dcb.XoffLim = 512; }
        flags |= dtrControl << 4;
        flags |= rtsControl << 12;
        dcb.Flags = flags;

        if (!Kernel32.SetCommState(_handle, ref dcb))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetCommState thất bại (kiểm tra baud/parity)");

        var timeouts = new Kernel32.COMMTIMEOUTS
        {
            ReadIntervalTimeout = 50,
            ReadTotalTimeoutConstant = (uint)route.ReadTimeoutMs,
            ReadTotalTimeoutMultiplier = 0,
            WriteTotalTimeoutConstant = (uint)route.GetInt("writeTimeout", 10000),
            WriteTotalTimeoutMultiplier = 1,
        };
        Kernel32.SetCommTimeouts(_handle, ref timeouts);

        _stream = new FileStream(_handle, FileAccess.ReadWrite, 1, false);
        log.Info($"Mở {port} @ {dcb.BaudRate} bps, handshake={hs}");
        return Task.CompletedTask;
    }

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var s = _stream ?? throw new InvalidOperationException("Chưa OpenAsync.");
        return Task.Run(() => { s.Write(data.Span); s.Flush(); }, ct);
    }

    public Task<int> ReceiveAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken ct = default)
    {
        var s = _stream ?? throw new InvalidOperationException("Chưa OpenAsync.");
        // Timeout đọc được cấu hình bằng COMMTIMEOUTS; ReadFile trả 0 khi hết giờ
        return Task.Run(() => s.Read(buffer.Span), ct);
    }

    public ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _handle?.Dispose();
        return ValueTask.CompletedTask;
    }
}

[SupportedOSPlatform("windows")]
internal static class Kernel32
{
    public const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, OPEN_EXISTING = 3;

    [StructLayout(LayoutKind.Sequential)]
    public struct DCB
    {
        public uint DCBlength;
        public uint BaudRate;
        public uint Flags;
        public ushort wReserved;
        public ushort XonLim;
        public ushort XoffLim;
        public byte ByteSize;
        public byte Parity;
        public byte StopBits;
        public byte XonChar;
        public byte XoffChar;
        public byte ErrorChar;
        public byte EofChar;
        public byte EvtChar;
        public ushort wReserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct COMMTIMEOUTS
    {
        public uint ReadIntervalTimeout;
        public uint ReadTotalTimeoutMultiplier;
        public uint ReadTotalTimeoutConstant;
        public uint WriteTotalTimeoutMultiplier;
        public uint WriteTotalTimeoutConstant;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetCommState(SafeFileHandle h, ref DCB dcb);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetCommState(SafeFileHandle h, ref DCB dcb);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetCommTimeouts(SafeFileHandle h, ref COMMTIMEOUTS t);
}
