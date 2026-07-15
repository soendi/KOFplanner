using System.Reflection;

namespace KOFplanner.Forms;

public class SplashForm : Form
{
    public SplashForm(int durationMs = 2500)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        TopMost = true;

        Image? img = null;
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("KOFplanner.Resources.splashscreen.png");
            if (stream != null)
                img = Image.FromStream(stream);
        }
        catch
        {
            img = null;
        }

        if (img != null)
        {
            ClientSize = img.Size;
            var pb = new PictureBox
            {
                Image = img,
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            Controls.Add(pb);
        }
        else
        {
            ClientSize = new Size(400, 240);
            Controls.Add(new Label
            {
                Text = "KOFplanner",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 20f, FontStyle.Bold)
            });
        }

        var timer = new System.Windows.Forms.Timer { Interval = durationMs };
        timer.Tick += (_, _) => Close();
        Shown += (_, _) => timer.Start();
    }
}
