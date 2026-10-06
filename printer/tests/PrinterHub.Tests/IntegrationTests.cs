using System.Net;
using System.Net.Sockets;
using System.Text;
using PrinterHub.Core;
using PrinterHub.Core.Config;
using PrinterHub.Core.Model;
using PrinterHub.Languages;
using PrinterHub.Simulator;
using PrinterHub.Transports;

namespace PrinterHub.Tests;

/// <summary>Kiểm thử đầu-cuối với PrinterSimulator chạy trong tiến trình (cổng ngẫu nhiên).</summary>
public sealed class IntegrationTests : IAsyncDisposable
{
    private readonly PrinterSimulator _sim;
    private readonly int _raw, _satoData, _satoStatus, _lpd;
    private readonly HubContext _hub;
    private readonly PrinterService _service;

    public IntegrationTests()
    {
        _raw = FreePort(); _satoData = FreePort(); _satoStatus = FreePort(); _lpd = FreePort();
        _sim = new PrinterSimulator(new SimulatorOptions
        {
            RawPort = _raw, SatoDataPort = _satoData, SatoStatusPort = _satoStatus, LpdPort = _lpd,
            JobsDirectory = null, LabelMs = 50,
        });
        _sim.Start();
        _hub = new HubContext();
        _hub.Transports.AddNetworkTransports();
        _hub.Languages.AddBuiltInLanguages();
        _service = new PrinterService(_hub);
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private async Task<PrinterSimulator.ReceivedJob> WaitJobAsync(int count = 1)
    {
        for (int i = 0; i < 100 && _sim.Jobs.Count < count; i++) await Task.Delay(30);
        Assert.True(_sim.Jobs.Count >= count, $"Simulator chưa nhận đủ {count} job (có {_sim.Jobs.Count})");
        return _sim.Jobs.ToArray()[count - 1];
    }

    [Test]
    public async Task Tcp_Sbpl_Print_And_Status()
    {
        var route = RouteConfig.Parse($"tcp://127.0.0.1:{_raw}");
        var lang = _hub.Languages.Create("SBPL");
        var doc = DemoLabels.Product();
        doc.Copies = 3;
        var r = await _service.PrintAsync(route, lang, PrintJob.FromLabel(doc));
        Assert.True(r.Success, r.Error);
        Assert.Equal(PrinterState.Ready, r.StatusBefore!.State);

        var job = await WaitJobAsync();
        Assert.Equal((byte)0x02, job.Data[0]);
        Assert.Equal((byte)0x03, job.Data[^1]);
        Assert.Equal(lang.Render(doc).Length, job.Data.Length);
    }

    [Test]
    public async Task Sato4_TwoPort_Status_Tracks_Remaining()
    {
        var route = RouteConfig.Parse($"sato4://127.0.0.1?data={_satoData}&status={_satoStatus}");
        var lang = _hub.Languages.Create("SBPL");
        var doc = new LabelDocument().Text(10, 10, "X");
        doc.Copies = 20;
        var r = await _service.PrintAsync(route, lang, PrintJob.FromLabel(doc));
        Assert.True(r.Success, r.Error);
        await WaitJobAsync();
        var st = await _service.GetStatusAsync(route, lang);
        Assert.Equal(PrinterState.Printing, st.State);
        Assert.True(st.RemainingLabels is > 0 and <= 20, $"remaining={st.RemainingLabels}");
    }

    [Test]
    public async Task Status_AllLanguages_OverTcp()
    {
        var route = RouteConfig.Parse($"tcp://127.0.0.1:{_raw}");
        foreach (var name in new[] { "SBPL", "ZPL", "EZPL", "PJL" })
        {
            var st = await _service.GetStatusAsync(route, _hub.Languages.Create(name));
            Assert.Equal(PrinterState.Ready, st.State);
        }
    }

    [Test]
    public async Task Printer_Error_Blocks_Print()
    {
        _sim.SatoErrorCode = 'c';
        var route = RouteConfig.Parse($"tcp://127.0.0.1:{_raw}");
        var r = await _service.PrintAsync(route, _hub.Languages.Create("SBPL"), PrintJob.FromLabel(DemoLabels.Product()), retryCount: 1, retryDelayMs: 10);
        Assert.False(r.Success);
        Assert.Equal(2, r.Attempts);
        Assert.Contains("Hết giấy", r.Error!);
        await Task.Delay(100);
        Assert.Equal(0, _sim.Jobs.Count);
    }

    [Test]
    public async Task Lpr_Delivers_Job()
    {
        var route = RouteConfig.Parse($"lpr://127.0.0.1:{_lpd}/zebra");
        var r = await _service.PrintAsync(route, _hub.Languages.Create("ZPL"), PrintJob.FromLabel(DemoLabels.Product()));
        Assert.True(r.Success, r.Error);
        var job = await WaitJobAsync();
        Assert.Equal("LPD-zebra", job.Channel);
        Assert.True(Encoding.UTF8.GetString(job.Data).StartsWith("^XA"));
    }

    [Test]
    public async Task Profile_Failover_To_Second_Route()
    {
        var profile = new PrinterProfile
        {
            Name = "T", Language = "EZPL", RetryCount = 0, RetryDelayMs = 10,
            Routes = [$"tcp://127.0.0.1:{FreePort()}?connectTimeout=300", $"tcp://127.0.0.1:{_raw}"],
        };
        var r = await _service.PrintAsync(profile, PrintJob.FromLabel(DemoLabels.Product()));
        Assert.True(r.Success, r.Error);
        Assert.Equal($"tcp://127.0.0.1:{_raw}", r.Route);
        Assert.Equal(2, r.Attempts);
        await WaitJobAsync();
    }

    [Test]
    public async Task Raw_SendAndReceive_Enq()
    {
        var route = RouteConfig.Parse($"tcp://127.0.0.1:{_raw}");
        byte[] reply = await _service.SendAndReceiveAsync(route, [0x05], TimeSpan.FromMilliseconds(500));
        Assert.True(reply.Length > 0 && reply[0] == 0x02 && reply[^1] == 0x03);
    }

    [Test]
    public async Task File_Transport_Writes()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ph_{Guid.NewGuid():N}", "job.prn");
        var r = await _service.PrintAsync(RouteConfig.Parse($"file://{path}"), _hub.Languages.Create("RAW"),
            PrintJob.FromRaw([1, 2, 3]) with { CheckStatusBeforePrint = false });
        Assert.True(r.Success, r.Error);
        Assert.SequenceEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [Test]
    public async Task Discovery_Finds_Simulator()
    {
        var found = await NetworkDiscovery.ScanAsync(["127.0.0.1"], [_raw, _satoData, _satoStatus], 300);
        Assert.Equal(1, found.Count);
        Assert.Equal(3, found[0].OpenPorts.Count);
    }

    public async ValueTask DisposeAsync() => await _sim.DisposeAsync();
}
