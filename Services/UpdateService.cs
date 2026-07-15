using System.Diagnostics;
using System.Text.Json;

namespace KOFplanner.Services;

public class UpdateService
{
    private const string VersionUrl = "https://raw.githubusercontent.com/soendi/KOFplanner/master/version.json";
    private readonly string _appPath;

    public Version CurrentVersion => typeof(UpdateService).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);

    public UpdateService()
    {
        _appPath = AppContext.BaseDirectory;
    }

    public async Task<Version?> CheckForUpdate()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var json = await http.GetStringAsync(VersionUrl);
            var info = JsonSerializer.Deserialize<UpdateInfo>(json);
            if (info?.Version == null) return null;
            var remote = new Version(info.Version);
            return remote > CurrentVersion ? remote : null;
        }
        catch { return null; }
    }

    public async Task<bool> DownloadAndInstall(Version newVersion)
    {
        try
        {
            var url = $"https://github.com/soendi/KOFplanner/releases/download/v{newVersion}/KOFplanner-Setup.exe";
            var tempDir = Path.Combine(Path.GetTempPath(), "KOFplannerUpdate");
            Directory.CreateDirectory(tempDir);
            var installerPath = Path.Combine(tempDir, "KOFplanner-Setup.exe");

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var data = await http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(installerPath, data);

            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SILENT /CURRENTUSER",
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch { return false; }
    }

    class UpdateInfo
    {
        public string Version { get; set; } = "";
        public string Notes { get; set; } = "";
    }
}
