using System.Reflection;
using System.Windows.Forms;

namespace KOFplanner.Forms;

public class SplashForm : Form
{
    private readonly Image? _img;
    private readonly System.Windows.Forms.Timer _timer;
    private int _elapsed;
    private const int HoldMs = 900;
    private const int FadeMs = 900;

    public SplashForm(int durationMs = 2500)
    {
        IconHelper.Apply(this);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        DoubleBuffered = true;
        Opacity = 1.0;

        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("KOFplanner.Resources.splashscreen.png");
            if (stream != null)
                _img = Image.FromStream(stream);
        }
        catch
        {
            _img = null;
        }

        if (_img != null)
            ClientSize = new Size(_img.Width / 2, _img.Height / 2);
        else
            ClientSize = new Size(400, 240);

        _timer = new System.Windows.Forms.Timer { Interval = 16 };
        _timer.Tick += OnTick;
        Shown += (_, _) => _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _elapsed += _timer.Interval;
        if (_elapsed <= HoldMs)
            return;

        int t = _elapsed - HoldMs;
        if (t >= FadeMs)
        {
            _timer.Stop();
            Close();
            return;
        }

        Opacity = 1.0 - (double)t / FadeMs;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_img == null)
        {
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString("KOFplanner", new Font("Segoe UI", 20f, FontStyle.Bold),
                Brushes.White, ClientRectangle, fmt);
            return;
        }

        double p = 0;
        if (_elapsed > HoldMs)
            p = Math.Min(1.0, (double)(_elapsed - HoldMs) / FadeMs);

        double scale = 1.0 + p * 1.6;
        int w = (int)(ClientSize.Width * scale);
        int h = (int)(ClientSize.Height * scale);
        int x = (ClientSize.Width - w) / 2;
        int y = (ClientSize.Height - h) / 2;

        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(_img, x, y, w, h);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer?.Stop();
            _timer?.Dispose();
            _img?.Dispose();
        }
        base.Dispose(disposing);
    }
}
