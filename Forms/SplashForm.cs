using System.Reflection;
using System.Windows.Forms;

namespace KOFplanner.Forms;

public class SplashForm : Form
{
    private readonly Image? _img;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Size _baseSize;
    private int _elapsed;
    private const int HoldMs = 200;
    private const int FadeMs = 400;

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

        _baseSize = _img != null ? new Size(_img.Width / 2, _img.Height / 2) : new Size(400, 240);
        ClientSize = _baseSize;

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

        double p = (double)t / FadeMs;
        double ease = p * p; // beschleunigt

        double scale = 1.0 + ease * 6.0;
        int w = (int)(_baseSize.Width * scale);
        int h = (int)(_baseSize.Height * scale);
        ClientSize = new Size(w, h);
        var screen = Screen.FromPoint(Location);
        Location = new Point(
            screen.WorkingArea.X + (screen.WorkingArea.Width - w) / 2,
            screen.WorkingArea.Y + (screen.WorkingArea.Height - h) / 2);
        Opacity = 1.0 - ease;
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

        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(_img, ClientRectangle);
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
