using System.Text.Json;

namespace KOFplanner.Services;

public class SettingsService
{
    private readonly string _path;

    public SettingsService(string baseDir)
    {
        _path = Path.Combine(baseDir, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path)) return new AppSettings();
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings(); }
        catch { return new AppSettings(); }
    }

    public void Save(AppSettings s) => File.WriteAllText(_path, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
}

public class AppSettings
{
    public EmailSettings Email { get; set; } = new();
    public string PrinterName { get; set; } = "";
    public string HomeAddress { get; set; } = "";
}

public class EmailSettings
{
    public string Server { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Sender { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
