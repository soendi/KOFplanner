using KOFplanner.Forms;
using KOFplanner.Services;

namespace KOFplanner;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kofplanner.db");
        var db = new DatabaseService(dbPath);
        var backup = new BackupService(dbPath);
        var update = new UpdateService();

        Application.Run(new MainForm(db, backup, update));
    }
}
