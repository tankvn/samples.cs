using System.Text;
using PrinterHub.Core.Config;
using PrinterHub.Core.Util;

namespace PrinterHub.Core;

/// <summary>Đăng ký các loại transport theo "kind" (tcp, sato4, lpr, spooler, serial...).</summary>
public sealed class TransportRegistry
{
    private readonly Dictionary<string, (Func<RouteConfig, IHubLog, IPrinterTransport> factory, string description)> _map =
        new(StringComparer.OrdinalIgnoreCase);

    public TransportRegistry Register(string kind, Func<RouteConfig, IHubLog, IPrinterTransport> factory, string description = "")
    {
        _map[kind] = (factory, description);
        return this;
    }

    public bool IsRegistered(string kind) => _map.ContainsKey(kind);

    public IReadOnlyDictionary<string, string> Kinds => _map.ToDictionary(k => k.Key, v => v.Value.description);

    public IPrinterTransport Create(RouteConfig route, IHubLog? log = null)
    {
        if (!_map.TryGetValue(route.Kind, out var entry))
            throw new NotSupportedException(
                $"Chưa đăng ký transport '{route.Kind}'. Có: {string.Join(", ", _map.Keys)}");
        return entry.factory(route, log ?? NullLog.Instance);
    }
}

/// <summary>Đăng ký các ngôn ngữ lệnh theo tên (SBPL, ZPL, EZPL, RAW, PJL...).</summary>
public sealed class LanguageRegistry
{
    private readonly Dictionary<string, Func<IReadOnlyDictionary<string, string>, ICommandLanguage>> _map =
        new(StringComparer.OrdinalIgnoreCase);

    public LanguageRegistry Register(string name, Func<IReadOnlyDictionary<string, string>, ICommandLanguage> factory)
    {
        _map[name] = factory;
        return this;
    }

    public IReadOnlyCollection<string> Names => _map.Keys;

    public ICommandLanguage Create(string name, string? encoding = null, IReadOnlyDictionary<string, string>? options = null)
    {
        if (!_map.TryGetValue(name, out var f))
            throw new NotSupportedException($"Chưa đăng ký ngôn ngữ '{name}'. Có: {string.Join(", ", _map.Keys)}");
        var lang = f(options ?? new Dictionary<string, string>());
        if (!string.IsNullOrWhiteSpace(encoding)) lang.TextEncoding = TextEncodings.Get(encoding);
        return lang;
    }

    public ICommandLanguage Create(PrinterProfile profile) =>
        Create(profile.Language, profile.Encoding, profile.LanguageOptions);
}

/// <summary>Ngữ cảnh dùng chung: registry + logger. Tạo một lần khi khởi động app.</summary>
public sealed class HubContext
{
    public TransportRegistry Transports { get; } = new();
    public LanguageRegistry Languages { get; } = new();
    public IHubLog Log { get; set; } = NullLog.Instance;

    public HubContext() => TextEncodings.EnsureRegistered();
}
