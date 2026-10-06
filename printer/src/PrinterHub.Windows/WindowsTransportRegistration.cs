using PrinterHub.Core;

namespace PrinterHub.Windows;

public static class WindowsTransportRegistration
{
    /// <summary>Đăng ký transport chỉ chạy trên Windows (Spooler RAW, cổng COM/Bluetooth SPP).</summary>
    public static TransportRegistry AddWindowsTransports(this TransportRegistry registry)
    {
        if (!OperatingSystem.IsWindows()) return registry;
        return registry
            .Register("spooler", (r, l) => new SpoolerRawTransport(r, l), "Windows Spooler RAW (WritePrinter) – mọi driver")
            .Register("serial", (r, l) => new SerialTransport(r, l), "COM / RS-232C / Bluetooth SPP");
    }
}
