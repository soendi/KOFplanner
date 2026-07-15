using KOFplanner.Forms;
using KOFplanner.Services;

namespace KOFplanner;

static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var dbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KOFplanner");
            Directory.CreateDirectory(dbDir);
            var dbPath = Path.Combine(dbDir, "kofplanner.db");
            var db = new DatabaseService(dbPath);
            var backup = new BackupService(dbPath);
            var update = new UpdateService();
            var settings = new SettingsService(dbDir);

            using (var splash = new SplashForm())
                splash.ShowDialog();

            Application.Run(new MainForm(db, backup, update, settings));
        }
        catch (Exception ex)
        {
            var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KOFplanner", "crash.log");
            File.WriteAllText(log, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}");
            MessageBox.Show($"Fehler beim Start: {ex.Message}\n\nDetails: {log}", "KOFplanner");
        }
    }
}
