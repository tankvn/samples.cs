using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrinterHub.Core.Config;

/// <summary>
/// Hồ sơ một máy in: hãng, model, ngôn ngữ, encoding và danh sách route theo thứ tự ưu tiên (failover).
/// </summary>
public sealed class PrinterProfile
{
    public string Name { get; set; } = "";
    public string Brand { get; set; } = "";
    public string? Model { get; set; }
    public int Dpi { get; set; } = 203;
    /// <summary>SBPL, ZPL, EZPL, RAW, PJL...</summary>
    public string Language { get; set; } = "RAW";
    /// <summary>Tên encoding: "utf-8", "shift_jis" (CP932), "windows-1258"...</summary>
    public string Encoding { get; set; } = "utf-8";
    /// <summary>Route dạng URI (xem <see cref="RouteConfig.Parse"/>), theo thứ tự ưu tiên.</summary>
    public List<string> Routes { get; set; } = [];
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 1000;
    /// <summary>Tuỳ chọn riêng cho ngôn ngữ (vd SBPL kanjiFont=K9B).</summary>
    public Dictionary<string, string> LanguageOptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<RouteConfig> ParseRoutes() => Routes.Select(RouteConfig.Parse);
}

public sealed class ProfileStore
{
    public List<PrinterProfile> Printers { get; set; } = [];

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public PrinterProfile? Find(string name) =>
        Printers.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static ProfileStore Load(string path) =>
        JsonSerializer.Deserialize<ProfileStore>(File.ReadAllText(path), JsonOptions) ?? new ProfileStore();

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
}
