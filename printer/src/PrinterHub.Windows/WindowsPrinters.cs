using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using PrinterHub.Windows.Native;

namespace PrinterHub.Windows;

public sealed record WindowsPrinterInfo(
    string Name, string? PortName, string? DriverName, string? ShareName, string? Location,
    string? Datatype, uint Status, uint Jobs, bool IsDefault)
{
    /// <summary>Đoán hãng theo tên driver.</summary>
    public string Brand =>
        (DriverName ?? Name) switch
        {
            var d when d.Contains("SATO", StringComparison.OrdinalIgnoreCase) => "SATO",
            var d when d.Contains("ZDesigner", StringComparison.OrdinalIgnoreCase) || d.Contains("Zebra", StringComparison.OrdinalIgnoreCase) => "Zebra",
            var d when d.Contains("Godex", StringComparison.OrdinalIgnoreCase) => "Godex",
            var d when d.Contains("FUJIFILM", StringComparison.OrdinalIgnoreCase) || d.Contains("FX ", StringComparison.OrdinalIgnoreCase) || d.Contains("Fuji Xerox", StringComparison.OrdinalIgnoreCase) || d.Contains("Apeos", StringComparison.OrdinalIgnoreCase) => "FUJIFILM",
            var d when d.Contains("Fujitsu", StringComparison.OrdinalIgnoreCase) => "Fujitsu",
            _ => "",
        };

    public string StatusText => Status == 0 ? "Ready" : DecodeStatus(Status);

    private static string DecodeStatus(uint s)
    {
        var parts = new List<string>();
        if ((s & 0x1) != 0) parts.Add("Paused");
        if ((s & 0x2) != 0) parts.Add("Error");
        if ((s & 0x8) != 0) parts.Add("PaperJam");
        if ((s & 0x10) != 0) parts.Add("PaperOut");
        if ((s & 0x80) != 0) parts.Add("Offline");
        if ((s & 0x200) != 0) parts.Add("Busy");
        if ((s & 0x400) != 0) parts.Add("Printing");
        if ((s & 0x40000) != 0) parts.Add("NotAvailable");
        if ((s & 0x400000) != 0) parts.Add("DoorOpen");
        if ((s & 0x100000) != 0) parts.Add("UserIntervention");
        return parts.Count == 0 ? $"0x{s:X}" : string.Join(",", parts);
    }
}

public sealed record WindowsPrintJobInfo(uint JobId, string? Document, string? User, string? StatusText, uint Status, uint TotalPages, uint PagesPrinted);

/// <summary>Liệt kê máy in và job trong Windows Spooler (EnumPrinters / EnumJobs).</summary>
[SupportedOSPlatform("windows")]
public static class WindowsPrinters
{
    public static string? GetDefaultPrinterName()
    {
        int size = 0;
        WinSpool.GetDefaultPrinter(null, ref size);
        if (size == 0) return null;
        var buf = new char[size];
        return WinSpool.GetDefaultPrinter(buf, ref size) ? new string(buf, 0, size - 1) : null;
    }

    public static List<WindowsPrinterInfo> List()
    {
        const uint flags = WinSpool.PRINTER_ENUM_LOCAL | WinSpool.PRINTER_ENUM_CONNECTIONS;
        WinSpool.EnumPrinters(flags, null, 2, IntPtr.Zero, 0, out uint needed, out _);
        if (needed == 0) return [];
        IntPtr buf = Marshal.AllocHGlobal((int)needed);
        try
        {
            if (!WinSpool.EnumPrinters(flags, null, 2, buf, needed, out _, out uint count))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "EnumPrinters thất bại");
            string? def = GetDefaultPrinterName();
            int size = Marshal.SizeOf<WinSpool.PRINTER_INFO_2>();
            var list = new List<WindowsPrinterInfo>((int)count);
            for (int i = 0; i < count; i++)
            {
                var pi = Marshal.PtrToStructure<WinSpool.PRINTER_INFO_2>(buf + i * size);
                string name = Marshal.PtrToStringUni(pi.pPrinterName) ?? "";
                list.Add(new WindowsPrinterInfo(
                    name,
                    Marshal.PtrToStringUni(pi.pPortName),
                    Marshal.PtrToStringUni(pi.pDriverName),
                    Marshal.PtrToStringUni(pi.pShareName),
                    Marshal.PtrToStringUni(pi.pLocation),
                    Marshal.PtrToStringUni(pi.pDatatype),
                    pi.Status, pi.cJobs,
                    string.Equals(name, def, StringComparison.OrdinalIgnoreCase)));
            }
            return list;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    public static List<WindowsPrintJobInfo> ListJobs(string printerName, int max = 100)
    {
        if (!WinSpool.OpenPrinter(printerName, out IntPtr h, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"OpenPrinter('{printerName}') thất bại");
        try
        {
            WinSpool.EnumJobs(h, 0, (uint)max, 1, IntPtr.Zero, 0, out uint needed, out _);
            if (needed == 0) return [];
            IntPtr buf = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (!WinSpool.EnumJobs(h, 0, (uint)max, 1, buf, needed, out _, out uint count))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "EnumJobs thất bại");
                int size = Marshal.SizeOf<WinSpool.JOB_INFO_1>();
                var list = new List<WindowsPrintJobInfo>();
                for (int i = 0; i < count; i++)
                {
                    var ji = Marshal.PtrToStructure<WinSpool.JOB_INFO_1>(buf + i * size);
                    list.Add(new WindowsPrintJobInfo(ji.JobId, Marshal.PtrToStringUni(ji.pDocument), Marshal.PtrToStringUni(ji.pUserName),
                        Marshal.PtrToStringUni(ji.pStatus), ji.Status, ji.TotalPages, ji.PagesPrinted));
                }
                return list;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        finally { WinSpool.ClosePrinter(h); }
    }
}
