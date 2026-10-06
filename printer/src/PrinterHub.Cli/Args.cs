namespace PrinterHub.Cli;

/// <summary>Bộ phân tích tham số dòng lệnh tối giản: command --key value --flag --set A=1 --set B=2.</summary>
public sealed class Args
{
    public string Command { get; private set; } = "help";
    private readonly Dictionary<string, List<string>> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Positional { get; } = [];

    public static Args Parse(string[] argv)
    {
        var a = new Args();
        int i = 0;
        if (argv.Length > 0 && !argv[0].StartsWith('-')) { a.Command = argv[0].ToLowerInvariant(); i = 1; }
        for (; i < argv.Length; i++)
        {
            string s = argv[i];
            if (s.StartsWith("--") || (s.StartsWith('-') && s.Length == 2))
            {
                string key = s.TrimStart('-');
                int eq = key.IndexOf('=');
                if (eq > 0) { a.Add(key[..eq], key[(eq + 1)..]); continue; }
                if (i + 1 < argv.Length && !argv[i + 1].StartsWith("--")) a.Add(key, argv[++i]);
                else a._flags.Add(key);
            }
            else a.Positional.Add(s);
        }
        return a;
    }

    private void Add(string key, string value)
    {
        if (!_values.TryGetValue(key, out var list)) _values[key] = list = [];
        list.Add(value);
    }

    public string? Get(string key) => _values.TryGetValue(key, out var l) ? l[^1] : null;
    public string Get(string key, string fallback) => Get(key) ?? fallback;
    public int GetInt(string key, int fallback) => int.TryParse(Get(key), out int v) ? v : fallback;
    public IReadOnlyList<string> GetAll(string key) => _values.TryGetValue(key, out var l) ? l : [];
    public bool Flag(string key) => _flags.Contains(key) || (Get(key) is { } v && bool.TryParse(v, out bool b) && b);

    public string Require(string key) => Get(key) ?? throw new ArgumentException($"Thiếu tham số --{key}");

    /// <summary>Đọc các cặp --set Key=Value (hoặc --opt) thành dictionary.</summary>
    public Dictionary<string, string> Pairs(string key)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in GetAll(key))
        {
            int eq = p.IndexOf('=');
            if (eq > 0) d[p[..eq]] = p[(eq + 1)..];
        }
        return d;
    }
}
