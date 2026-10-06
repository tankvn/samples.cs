using System.Text.Json;
using PrinterHub.Core.Config;

namespace PrinterHub.App;

/// <summary>Cài đặt người dùng lưu tại %AppData%\PrinterHub\settings.json.</summary>
internal sealed class AppSettings
{
    public static readonly string Folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PrinterHub");
    public static string SettingsPath => Path.Combine(Folder, "settings.json");
    public static string ProfilesPath => Path.Combine(Folder, "profiles.json");

    public string? LastRoute { get; set; }
    public string? Language { get; set; }
    public string? Encoding { get; set; }
    public List<string> RouteHistory { get; set; } = [];

    public void Remember(string route)
    {
        RouteHistory.Remove(route);
        RouteHistory.Insert(0, route);
        if (RouteHistory.Count > 20) RouteHistory.RemoveRange(20, RouteHistory.Count - 20);
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), ProfileStore.JsonOptions) ?? new AppSettings();
        }
        catch { /* file hỏng → dùng mặc định */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, ProfileStore.JsonOptions));
        }
        catch { /* bỏ qua */ }
    }

    public static ProfileStore DefaultProfiles()
    {
        Directory.CreateDirectory(Folder);
        return SampleFiles.Profiles();
    }
}
