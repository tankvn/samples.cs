using System.Text;
using PrinterHub.Cli;
using PrinterHub.Core;
using PrinterHub.Core.Config;
using PrinterHub.Core.Model;
using PrinterHub.Core.Util;
using PrinterHub.Languages;
using PrinterHub.Transports;
using PrinterHub.Windows;

Console.OutputEncoding = Encoding.UTF8;
var a = Args.Parse(args);
var log = new ConsoleLog(a.Flag("verbose") || a.Flag("v"));
var hub = new HubContext { Log = log };
hub.Transports.AddNetworkTransports().AddWindowsTransports();
hub.Languages.AddBuiltInLanguages();
var service = new PrinterService(hub);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    return a.Command switch
    {
        "send" => await Commands.SendFileAsync(a, hub, service, cts.Token),
        "raw" => await Commands.RawAsync(a, hub, service, cts.Token),
        "label" or "demo" => await Commands.LabelAsync(a, hub, service, cts.Token),
        "batch" => await Commands.BatchAsync(a, hub, service, cts.Token),
        "status" => await Commands.StatusAsync(a, hub, service, cts.Token),
        "print" => await Commands.ProfilePrintAsync(a, hub, service, cts.Token),
        "watch" => await Commands.WatchAsync(a, hub, service, cts.Token),
        "discover" => await Commands.DiscoverAsync(a, cts.Token),
        "printers" => Commands.ListWindowsPrinters(),
        "jobs" => Commands.ListWindowsJobs(a),
        "init" => Commands.Init(a),
        "routes" => Commands.ListRoutes(hub),
        _ => Commands.Help(),
    };
}
catch (OperationCanceledException)
{
    log.Warn("Đã huỷ.");
    return 130;
}
catch (Exception ex)
{
    log.Error("Lỗi", ex);
    if (a.Flag("verbose")) Console.Error.WriteLine(ex);
    return 1;
}
