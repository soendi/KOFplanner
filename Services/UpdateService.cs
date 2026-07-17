using System.Diagnostics;
using System.Text.Json;

namespace KOFplanner.Services;

public class UpdateService
{
    private const string ApiReleases = "https://api.github.com/repos/soendi/KOFplanner/releases?per_page=10";
    private const string VersionUrl = "https://raw.githubusercontent.com/soendi/KOFplanner/master/version.json";
    private readonly string _appPath;

    public Version CurrentVersion => typeof(UpdateService).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);

    public UpdateService()
    {
        _appPath = AppContext.BaseDirectory;
    }

    public async Task<Version?> CheckForUpdate()
    {
        // Primaer: echten GitHub-Release-Tag pruefen (nur verfuegbar, wenn Build gelaufen ist).
        // apiOk = API war erreichbar (auch bei leerem Ergebnis) -> kein version.json-Fallback.
        var (releaseVersion, releaseFound, apiOk) = await CheckReleaseTag();
        if (apiOk)
            return releaseFound && releaseVersion > CurrentVersion ? releaseVersion : null;

        // Fallback nur bei Netz/API-Fehler: version.json (Repo-Master) als grobe Info.
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var json = await http.GetStringAsync(VersionUrl);
            var info = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (info?.Version == null) return null;
            var remote = new Version(info.Version);
            return remote > CurrentVersion ? remote : null;
        }
        catch { return null; }
    }

    private async Task<(Version version, bool found, bool apiOk)> CheckReleaseTag()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.TryParseAdd("KOFplanner");
            http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            var json = await http.GetStringAsync(ApiReleases);
            using var doc = JsonDocument.Parse(json);
            foreach (var rel in doc.RootElement.EnumerateArray())
            {
                if (rel.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) continue;
                if (rel.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
                if (!rel.TryGetProperty("tag_name", out var tag)) continue;
                var name = tag.GetString() ?? "";
                if (!name.StartsWith("v", StringComparison.OrdinalIgnoreCase)) continue;
                var verStr = name.Substring(1);
                if (Version.TryParse(verStr, out var v) && v > CurrentVersion) return (v, true, true);
            }
            return (new Version(0, 0, 0), false, true);
        }
        catch { return (new Version(0, 0, 0), false, false); }
    }

    public async Task<bool> DownloadAndInstall(Version newVersion)
    {
        var url = $"https://github.com/soendi/KOFplanner/releases/download/v{newVersion}/KOFplanner-Setup.exe";
        var tempDir = Path.Combine(Path.GetTempPath(), "KOFplannerUpdate");
        Directory.CreateDirectory(tempDir);
        var installerPath = Path.Combine(tempDir, "KOFplanner-Setup.exe");

        using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Download fehlgeschlagen (HTTP {(int)response.StatusCode}). Datei im Release nicht gefunden?");
            var total = response.Content.Headers.ContentLength ?? -1L;
            using var src = await response.Content.ReadAsStreamAsync();
            using var dst = File.Create(installerPath);
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n));
                read += n;
                if (total > 0) DownloadProgress?.Invoke((double)read / total);
            }
        }

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT /CURRENTUSER",
            UseShellExecute = true
        };
        Process.Start(psi);
        return true;
    }

    public event Action<double>? DownloadProgress;


    class UpdateInfo
    {
        public string Version { get; set; } = "";
        public string Notes { get; set; } = "";
    }
}
