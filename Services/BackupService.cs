using System.Diagnostics;
using System.Text.Json;

namespace KOFplanner.Services;

public class BackupService
{
    private readonly string _dbPath;
    private readonly string _configPath;

    public BackupService(string dbPath)
    {
        _dbPath = dbPath;
        _configPath = Path.Combine(Path.GetDirectoryName(dbPath)!, "backup_config.json");
    }

    public bool IsConfigured => File.Exists(_configPath);

    public async Task<bool> BackupToDrive()
    {
        if (!IsConfigured) return false;
        var cfg = JsonSerializer.Deserialize<BackupConfig>(await File.ReadAllTextAsync(_configPath));
        if (cfg == null) return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                ArgumentList = { "/c", $"copy \"{_dbPath}\" \"{_dbPath}.bak\" /Y" },
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            await proc!.WaitForExitAsync();
            return true;
        }
        catch { return false; }
    }

    public void ConfigureDriveBackup(string clientId, string clientSecret, string folderId)
    {
        var cfg = new BackupConfig
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            FolderId = folderId,
            LastBackup = DateTime.MinValue
        };
        File.WriteAllText(_configPath, JsonSerializer.Serialize(cfg));
    }

    public string? GetConfigStatus()
    {
        if (!IsConfigured) return null;
        var cfg = JsonSerializer.Deserialize<BackupConfig>(File.ReadAllText(_configPath));
        return cfg?.FolderId;
    }

    class BackupConfig
    {
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string FolderId { get; set; } = "";
        public DateTime LastBackup { get; set; }
    }
}
