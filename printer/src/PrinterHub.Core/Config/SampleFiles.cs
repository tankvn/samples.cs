
namespace PrinterHub.Core.Config;

public static class SampleFiles
{
    public static ProfileStore Profiles() => new()
    {
        Printers =
        [
            new PrinterProfile
            {
                Name = "SATO-CL4NX", Brand = "SATO", Model = "CL4NX Plus", Dpi = 305,
                Language = "SBPL", Encoding = "shift_jis",
                Routes = ["sato4://192.168.1.50?data=1024&status=1025", "tcp://192.168.1.50:9100", "spooler://SATO CL4NX Plus 305dpi"],
                LanguageOptions = { ["kanjiFont"] = "K9B", ["jobId"] = "01" },
            },
            new PrinterProfile
            {
                Name = "ZEBRA-ZT411", Brand = "Zebra", Model = "ZT411", Dpi = 203,
                Language = "ZPL", Encoding = "utf-8",
                Routes = ["tcp://192.168.1.51:9100", "spooler://ZDesigner ZT411-203dpi ZPL"],
                LanguageOptions = { ["unicodeFont"] = "E:ANMDJ.TTF" },
            },
            new PrinterProfile
            {
                Name = "GODEX-G500", Brand = "Godex", Model = "G500", Dpi = 203,
                Language = "EZPL", Encoding = "utf-8",
                Routes = ["tcp://192.168.1.52:9100", "spooler://Godex G500"],
                LanguageOptions = { ["enableImmediateStatus"] = "true" },
            },
            new PrinterProfile
            {
                Name = "FUJIFILM-APEOS", Brand = "FUJIFILM", Model = "ApeosPort C3570", Dpi = 600,
                Language = "PJL", Encoding = "utf-8",
                Routes = ["tcp://192.168.1.60:9100", "lpr://192.168.1.60/lp"],
                LanguageOptions = { ["language"] = "PDF", ["paper"] = "A4" },
            },
            new PrinterProfile
            {
                Name = "SIMULATOR", Brand = "SATO", Model = "PrinterHub.Simulator", Dpi = 203,
                Language = "SBPL", Encoding = "shift_jis",
                Routes = ["sato4://127.0.0.1?data=1024&status=1025", "tcp://127.0.0.1:9100", "lpr://127.0.0.1:5515/lp"],
            },
        ],
    };

    public const string Csv = """
        Title,PartNo,Qty,Lot,Name,Copies
        PRINTERHUB TEST,P2000800,100,L20261006,部品テスト,1
        PRINTERHUB TEST,P2000801,50,L20261006,ネジ M3x10,2
        PRINTERHUB TEST,P2000802,25,L20261007,Bu long M8,1
        """;
}
