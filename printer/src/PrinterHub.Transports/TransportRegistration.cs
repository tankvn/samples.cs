using PrinterHub.Core;

namespace PrinterHub.Transports;

public static class TransportRegistration
{
    /// <summary>Đăng ký các transport đa nền tảng (TCP, SATO Status4, LPR, File/UNC).</summary>
    public static TransportRegistry AddNetworkTransports(this TransportRegistry registry) => registry
        .Register("tcp", (r, l) => new TcpRawTransport(r, l), "Raw TCP (9100/1024/6101...) – hai chiều")
        .Register("sato4", (r, l) => new SatoStatus4Transport(r, l), "SATO Status4 hai cổng (data + status)")
        .Register("lpr", (r, l) => new LprTransport(r, l), "LPR/LPD RFC 1179 (515)")
        .Register("file", (r, l) => new FileTransport(r, l), "File / UNC share / \\\\.\\LPT1");
}
