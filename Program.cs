using KOFplanner.Forms;
using KOFplanner.Services;

namespace KOFplanner;

static class Program
{
    [STAThread]
    static void Main()
    {
        var dbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KOFplanner");
        Directory.CreateDirectory(dbDir);

        Application.ThreadException += (_, e) =>
            LogAndShow(dbDir, "UI-Fehler", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            LogAndShow(dbDir, "Unerwarteter Fehler", ex);
        };

        try
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

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
            LogAndShow(dbDir, "Fehler beim Start", ex);
        }
    }

    private static void LogAndShow(string dbDir, string title, Exception? ex)
    {
        var log = Path.Combine(dbDir, "crash.log");
        try { File.AppendAllText(log, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {title}\n{ex}\n\n"); } catch { }
        MessageBox.Show($"{title}:\n{ex?.Message}\n\nDetails in: {log}", "KOFplanner",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
