using System.Net;
using PrinterHub.Simulator;

// PrinterHub.Simulator — máy in giả lập để phát triển/test không cần máy thật.
// Dùng: PrinterHub.Simulator [--any] [--raw 9100] [--sato 1024,1025] [--lpd 5515] [--jobs ./jobs] [--label-ms 200]
//       Tham số "--sato-error c" giả lập hết giấy (b=mở đầu in, d=hết ribbon...).

var o = new SimulatorOptions();
string? satoError = null;
for (int i = 0; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Thiếu giá trị cho {args[i]}");
    switch (args[i])
    {
        case "--any": o.Address = IPAddress.Any; break;
        case "--raw": o.RawPort = int.Parse(Next()); break;
        case "--sato":
            var p = Next().Split(',');
            o.SatoDataPort = int.Parse(p[0]);
            o.SatoStatusPort = p.Length > 1 ? int.Parse(p[1]) : o.SatoDataPort + 1;
            break;
        case "--lpd": o.LpdPort = int.Parse(Next()); break;
        case "--jobs": o.JobsDirectory = Next(); break;
        case "--label-ms": o.LabelMs = int.Parse(Next()); break;
        case "--sato-error": satoError = Next(); break;
        case "-h" or "--help":
            Console.WriteLine("PrinterHub.Simulator [--any] [--raw 9100] [--sato 1024,1025] [--lpd 5515] [--jobs dir] [--label-ms 200] [--sato-error c]");
            return;
    }
}

await using var sim = new PrinterSimulator(o);
if (satoError is { Length: > 0 }) sim.SatoErrorCode = satoError[0];
sim.Log += m => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {m}");
sim.Start();
Console.WriteLine("Simulator đang chạy. Phím: [p] hết giấy SATO, [h] mở đầu in, [r] trở lại bình thường, [q] thoát.");

while (true)
{
    if (Console.IsInputRedirected) { await Task.Delay(1000); continue; }
    var key = Console.ReadKey(true).KeyChar;
    switch (key)
    {
        case 'p': sim.SatoErrorCode = 'c'; sim.ZebraPaperOut = true; sim.GodexIdleCode = "01"; Console.WriteLine("→ Hết giấy"); break;
        case 'h': sim.SatoErrorCode = 'b'; sim.ZebraHeadOpen = true; sim.GodexIdleCode = "04"; Console.WriteLine("→ Mở đầu in"); break;
        case 'r': sim.SatoErrorCode = null; sim.ZebraPaperOut = sim.ZebraHeadOpen = false; sim.GodexIdleCode = "00"; Console.WriteLine("→ Bình thường"); break;
        case 'q': return;
    }
}
