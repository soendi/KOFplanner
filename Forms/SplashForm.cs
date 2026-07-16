using System.Reflection;
using System.Windows.Forms;

namespace KOFplanner.Forms;

public class SplashForm : Form
{
    private readonly Image? _img;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly int _baseW;
    private readonly int _baseH;
    private int _elapsed;
    private const int HoldMs = 3000;
    private const int FadeMs = 600;

    public SplashForm(int durationMs = 2500)
    {
        IconHelper.Apply(this);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        TransparencyKey = Color.Black;
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
        {
            _baseW = _img.Width / 2;
            _baseH = _img.Height / 2;
            // the form is sized to fit the 200% zoom; the base image is drawn centered
            ClientSize = new Size(_img.Width, _img.Height);
        }
        else
        {
            _baseW = 400;
            _baseH = 240;
            ClientSize = new Size(800, 480);
        }

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
        double ease = p * p;
        Opacity = 1.0 - ease;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int cx = ClientSize.Width / 2;
        int cy = ClientSize.Height / 2;

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
        double ease = p * p;

        // zoom from 100% (base image) up to 200%, centered on the client center
        double scale = 1.0 + ease;
        int w = (int)(_baseW * scale);
        int h = (int)(_baseH * scale);
        int x = cx - w / 2;
        int y = cy - h / 2;

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
