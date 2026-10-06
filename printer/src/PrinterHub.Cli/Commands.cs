using PrinterHub.Core;
using PrinterHub.Core.Config;
using PrinterHub.Core.Model;
using PrinterHub.Core.Util;
using PrinterHub.Transports;
using PrinterHub.Windows;

namespace PrinterHub.Cli;

internal static class Commands
{
    public static int Help()
    {
        Console.WriteLine("""
        PrinterHub CLI — gửi lệnh tới máy in SATO / Zebra / Godex / FUJIFILM / Fujitsu...

        ROUTE (--route):
          tcp://192.168.1.50:9100            Raw TCP (hai chiều)
          sato4://192.168.1.50?data=1024&status=1025   SATO Status4 hai cổng
          lpr://192.168.1.60/queue           LPR/LPD
          spooler://SATO CL4NX Plus 305dpi   Windows Spooler RAW (driver bất kỳ)
          serial://COM3?baud=9600&handshake=rtscts     COM / Bluetooth SPP
          file://\\PC01\SharedPrinter        Máy in chia sẻ / file

        NGÔN NGỮ (--lang): SBPL | ZPL | SZPL | GZPL | EZPL | PJL | RAW

        LỆNH:
          send     --route R --file job.prn [--lang RAW|PJL] [--format PDF] [--wait 1000]
          raw      --route R --text "<STX><ESC>A...<ESC>Z<ETX>" [--sbpl] [--encoding shift_jis] [--wait 1000]
                   --route R --hex "1B41..."
          label    --route R | --out file  [--lang SBPL] [--json label.json] [--dpi 203] [--copies 1]
                   [--set PartNo=ABC] [--opt kanjiFont=K9B] [--show]
          batch    --json template.json --csv data.csv (--route R --lang L | --profile P) [--out dir]
          status   --route R --lang SBPL|ZPL|EZPL|PJL [--repeat 1000]
          print    --profile P [--profiles profiles.json] (--json label.json | --file job.prn)
          watch    --folder in --profile P [--template label.json]   (hot folder)
          discover [--subnet 192.168.1] [--ports 9100,1024,1025,515,631] [--timeout 300]
          printers                          Liệt kê máy in Windows
          jobs     --printer "Tên máy in"   Liệt kê job trong hàng đợi
          routes                            Liệt kê loại route đã đăng ký
          init                              Tạo file mẫu profiles.json, label.json, data.csv

        Thêm --verbose để xem hex-dump dữ liệu gửi/nhận.
        """);
        return 0;
    }

    // ------------------------------------------------------------------

    private static ICommandLanguage Language(Args a, HubContext hub, string fallback)
    {
        var opts = a.Pairs("opt");
        return hub.Languages.Create(a.Get("lang", fallback), a.Get("encoding"), opts);
    }

    private static LabelDocument LoadLabel(Args a)
    {
        int dpi = a.GetInt("dpi", 203);
        LabelDocument doc = a.Get("json") is { } json
            ? DemoLabels.FromJson(File.ReadAllText(json))
            : DemoLabels.Product(dpi);
        if (a.Get("dpi") is not null && a.Get("json") is not null) doc.Dpi = dpi;
        doc.Copies = a.GetInt("copies", doc.Copies);
        var vars = a.Pairs("set");
        return vars.Count > 0 ? doc.Bind(vars) : doc;
    }

    private static async Task<int> Deliver(Args a, HubContext hub, PrinterService service, ICommandLanguage lang, PrintJob job, CancellationToken ct)
    {
        if (a.Get("out") is { } outPath)
        {
            byte[] bytes = PrinterService.Render(lang, job);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
            await File.WriteAllBytesAsync(outPath, bytes, ct);
            hub.Log.Info($"Đã ghi {bytes.Length} bytes ({lang.Name}) → {outPath}");
            if (a.Flag("show")) Console.WriteLine(ControlChars.ToDisplay(bytes, lang.TextEncoding));
            return 0;
        }
        var route = RouteConfig.Parse(a.Require("route"));
        if (a.Flag("show")) Console.WriteLine(ControlChars.ToDisplay(PrinterService.Render(lang, job), lang.TextEncoding));
        var result = await service.PrintAsync(route, lang, job, a.GetInt("retry", 0), 1000, ct);
        Console.WriteLine(result);
        return result.Success ? 0 : 2;
    }

    // ------------------------------------------------------------------

    public static async Task<int> SendFileAsync(Args a, HubContext hub, PrinterService service, CancellationToken ct)
    {
        string file = a.Require("file");
        var lang = Language(a, hub, "RAW");
        var job = PrintJob.FromRaw(await File.ReadAllBytesAsync(file, ct), Path.GetFileName(file), a.Get("format"))
            with { Copies = a.GetInt("copies", 1), CheckStatusBeforePrint = false };
        int wait = a.GetInt("wait", 0);
        if (wait > 0)
        {
            var route = RouteConfig.Parse(a.Require("route"));
            byte[] reply = await service.SendAndReceiveAsync(route, PrinterService.Render(lang, job), TimeSpan.FromMilliseconds(wait), ct);
            PrintReply(reply, lang);
            return 0;
        }
        return await Deliver(a, hub, service, lang, job, ct);
    }

    public static async Task<int> RawAsync(Args a, HubContext hub, PrinterService service, CancellationToken ct)
    {
        var route = RouteConfig.Parse(a.Require("route"));
        var enc = TextEncodings.Get(a.Get("encoding", "utf-8"));
        byte[] data = a.Get("hex") is { } hex
            ? Convert.FromHexString(hex.Replace(" ", "").Replace("-", ""))
            : ControlChars.Parse(a.Require("text"), enc, a.Flag("sbpl"));
        hub.Log.Info($"Gửi {data.Length} bytes:\n{HexDump.Format(data, 256)}");
        byte[] reply = await service.SendAndReceiveAsync(route, data, TimeSpan.FromMilliseconds(a.GetInt("wait", 1000)), ct);
        PrintReply(reply, null, enc);
        return 0;
    }

    private static void PrintReply(byte[] reply, ICommandLanguage? lang, System.Text.Encoding? enc = null)
    {
        if (reply.Length == 0) { Console.WriteLine("(không có phản hồi)"); return; }
        Console.WriteLine($"Phản hồi {reply.Length} bytes:");
        Console.WriteLine(HexDump.Format(reply));
        Console.WriteLine(ControlChars.ToDisplay(reply, enc ?? lang?.TextEncoding ?? TextEncodings.Utf8));
        if (lang is not null && lang.TryParseStatus(reply, out var st)) Console.WriteLine($"Trạng thái: {st}");
    }

    public static async Task<int> LabelAsync(Args a, HubContext hub, PrinterService service, CancellationToken ct)
    {
        var lang = Language(a, hub, "SBPL");
        var doc = LoadLabel(a);
        return await Deliver(a, hub, service, lang, PrintJob.FromLabel(doc), ct);
    }

    public static async Task<int> BatchAsync(Args a, HubContext hub, PrinterService service, CancellationToken ct)
    {
        var template = DemoLabels.FromJson(File.ReadAllText(a.Require("json")));
        var rows = CsvLite.ReadFile(a.Require("csv"));
        hub.Log.Info($"In {rows.Count} nhãn từ CSV");
        PrinterProfile? profile = a.Get("profile") is { } pn
            ? ProfileStore.Load(a.Get("profiles", "profiles.json")).Find(pn) ?? throw new ArgumentException($"Không có profile {pn}")
            : null;
        var lang = profile is null ? Language(a, hub, "SBPL") : hub.Languages.Create(profile);

        int ok = 0, i = 0;
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var doc = template.Bind(row);
            if (row.TryGetValue("Copies", out var c) && int.TryParse(c, out int copies)) doc.Copies = copies;
            var job = PrintJob.FromLabel(doc, $"Row{++i}");
            if (a.Get("out") is { } dir)
            {
                Directory.CreateDirectory(dir);
                await File.WriteAllBytesAsync(Path.Combine(dir, $"label_{i:D4}.{lang.Name.ToLowerInvariant()}"), PrinterService.Render(lang, job), ct);
                ok++;
                continue;
            }
            var r = profile is not null
                ? await service.PrintAsync(profile, job, ct)
                : await service.PrintAsync(RouteConfig.Parse(a.Require("route")), lang, job, a.GetInt("retry", 1), 1000, ct);
            Console.WriteLine($"#{i}: {r}");
            if (r.Success) ok++;
        }
        Console.WriteLine($"Hoàn tất {ok}/{rows.Count}");
        return ok == rows.Count ? 0 : 2;
    }

    public static async Task<int> StatusAsync(Args a, HubContext hub, PrinterService service, CancellationToken ct)
    {
        RouteConfig route;
        ICommandLanguage lang;
        if (a.Get("profile") is { } pn)
        {
            var p = ProfileStore.Load(a.Get("profiles", "profiles.json")).Find(pn) ?? throw new ArgumentException($"Không có profile {pn}");
            route = p.ParseRoutes().First();
            lang = hub.Languages.Create(p);
        }
        else
        {
            route = RouteConfig.Parse(a.Require("route"));
            lang = Language(a, hub, "SBPL");
        }
        int repeat = a.GetInt("repeat", 0);
        do
        {
            var st = await service.GetStatusAsync(route, lang, ct);
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} {route} → {st}");
            if (repeat > 0) await Task.Delay(repeat, ct);
        } while (repeat > 0);
        return 0;
    }

    public static async Task<int> ProfilePrintAsync(Args a, HubContext hub, PrinterService service, CancellationToken ct)
    {
        var store = ProfileStore.Load(a.Get("profiles", "profiles.json"));
        var profile = store.Find(a.Require("profile")) ?? throw new ArgumentException("Không tìm thấy profile.");
        PrintJob job = a.Get("file") is { } f
            ? PrintJob.FromRaw(await File.ReadAllBytesAsync(f, ct), Path.GetFileName(f), a.Get("format"))
            : PrintJob.FromLabel(LoadLabelForProfile(a, profile));
        var r = await service.PrintAsync(profile, job, ct);
        Console.WriteLine(r);
        return r.Success ? 0 : 2;
    }

    private static LabelDocument LoadLabelForProfile(Args a, PrinterProfile p)
    {
        var doc = a.Get("json") is { } json ? DemoLabels.FromJson(File.ReadAllText(json)) : DemoLabels.Product(p.Dpi);
        var vars = a.Pairs("set");
        return vars.Count > 0 ? doc.Bind(vars) : doc;
    }

    /// <summary>
    /// Hot folder: *.json → nhãn; *.csv → in hàng loạt với --template; còn lại (.prn/.zpl/.sbpl/.pdf...) → gửi raw.
    /// File xong chuyển vào done/, lỗi chuyển vào error/.
    /// </summary>
    public static async Task<int> WatchAsync(Args a, HubContext hub, PrinterService service, CancellationToken ct)
    {
        string folder = Path.GetFullPath(a.Require("folder"));
        var store = ProfileStore.Load(a.Get("profiles", "profiles.json"));
        var profile = store.Find(a.Require("profile")) ?? throw new ArgumentException("Không tìm thấy profile.");
        LabelDocument? template = a.Get("template") is { } t ? DemoLabels.FromJson(File.ReadAllText(t)) : null;
        string done = Path.Combine(folder, "done"), error = Path.Combine(folder, "error");
        Directory.CreateDirectory(done);
        Directory.CreateDirectory(error);
        hub.Log.Info($"Theo dõi {folder} → {profile.Name}. Ctrl+C để dừng.");

        while (!ct.IsCancellationRequested)
        {
            foreach (var file in Directory.GetFiles(folder).OrderBy(File.GetCreationTimeUtc))
            {
                if (!IsReady(file)) continue;
                bool success = true;
                try
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".json")
                        success = (await service.PrintAsync(profile, PrintJob.FromLabel(DemoLabels.FromJson(File.ReadAllText(file)), Path.GetFileName(file)), ct)).Success;
                    else if (ext == ".csv" && template is not null)
                    {
                        foreach (var row in CsvLite.ReadFile(file))
                            success &= (await service.PrintAsync(profile, PrintJob.FromLabel(template.Bind(row)), ct)).Success;
                    }
                    else
                        success = (await service.PrintAsync(profile, PrintJob.FromRaw(await File.ReadAllBytesAsync(file, ct), Path.GetFileName(file),
                            ext == ".pdf" ? "PDF" : ext == ".ps" ? "POSTSCRIPT" : ext == ".pcl" ? "PCL" : null), ct)).Success;
                }
                catch (Exception ex) { hub.Log.Error($"Lỗi xử lý {file}", ex); success = false; }

                string dest = Path.Combine(success ? done : error, $"{DateTime.Now:yyyyMMdd-HHmmss}_{Path.GetFileName(file)}");
                File.Move(file, dest);
                hub.Log.Info($"{Path.GetFileName(file)} → {(success ? "done" : "error")}");
            }
            await Task.Delay(a.GetInt("interval", 1000), ct);
        }
        return 0;
    }

    private static bool IsReady(string file)
    {
        try { using var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None); return true; }
        catch (IOException) { return false; }
    }

    public static async Task<int> DiscoverAsync(Args a, CancellationToken ct)
    {
        var subnets = a.Get("subnet") is { } s ? [s] : NetworkDiscovery.LocalSubnets().ToList();
        int[] ports = a.Get("ports") is { } p ? p.Split(',').Select(int.Parse).ToArray() : NetworkDiscovery.DefaultPorts;
        Console.WriteLine($"Quét {string.Join(", ", subnets)} cổng {string.Join(",", ports)} ...");
        var hosts = subnets.SelectMany(x => NetworkDiscovery.Subnet(x));
        var found = await NetworkDiscovery.ScanAsync(hosts, ports, a.GetInt("timeout", 300),
            progress: new Progress<string>(m => Console.WriteLine("  + " + m)), ct: ct);
        Console.WriteLine($"Tìm thấy {found.Count} thiết bị.");
        return 0;
    }

    public static int ListWindowsPrinters()
    {
        if (!OperatingSystem.IsWindows()) { Console.WriteLine("Chỉ hỗ trợ trên Windows."); return 1; }
        foreach (var p in WindowsPrinters.List())
            Console.WriteLine($"{(p.IsDefault ? "*" : " ")} {p.Name,-40} {p.Brand,-8} port={p.PortName,-20} driver={p.DriverName} [{p.StatusText}] jobs={p.Jobs}");
        return 0;
    }

    public static int ListWindowsJobs(Args a)
    {
        if (!OperatingSystem.IsWindows()) { Console.WriteLine("Chỉ hỗ trợ trên Windows."); return 1; }
        foreach (var j in WindowsPrinters.ListJobs(a.Require("printer")))
            Console.WriteLine($"#{j.JobId} {j.Document} by {j.User} status={j.StatusText ?? j.Status.ToString()} pages={j.PagesPrinted}/{j.TotalPages}");
        return 0;
    }

    public static int ListRoutes(HubContext hub)
    {
        foreach (var (k, d) in hub.Transports.Kinds) Console.WriteLine($"{k,-10} {d}");
        Console.WriteLine("Ngôn ngữ: " + string.Join(", ", hub.Languages.Names));
        return 0;
    }

    public static int Init(Args a)
    {
        string dir = a.Get("dir", ".");
        Directory.CreateDirectory(dir);
        var store = SampleFiles.Profiles();
        store.Save(Path.Combine(dir, "profiles.json"));
        File.WriteAllText(Path.Combine(dir, "label.json"), DemoLabels.ToJson(DemoLabels.Template(203)));
        File.WriteAllText(Path.Combine(dir, "data.csv"), SampleFiles.Csv, new System.Text.UTF8Encoding(true));
        Console.WriteLine($"Đã tạo profiles.json, label.json, data.csv trong {Path.GetFullPath(dir)}");
        return 0;
    }
}
