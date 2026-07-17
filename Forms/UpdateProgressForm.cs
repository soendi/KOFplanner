using System;
using System.Windows.Forms;

namespace KOFplanner.Forms;

public class UpdateProgressForm : Form
{
    private readonly ProgressBar _bar;

    public UpdateProgressForm()
    {
        IconHelper.Apply(this);
        Text = "Update wird installiert";
        Size = new System.Drawing.Size(360, 110);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ControlBox = false;

        var lbl = new Label { Text = "Neue Version wird heruntergeladen…", Dock = DockStyle.Top, Padding = new Padding(10, 10, 10, 0) };
        _bar = new ProgressBar { Dock = DockStyle.Bottom, Style = ProgressBarStyle.Continuous, Height = 24, Margin = new Padding(10) };
        Controls.Add(_bar);
        Controls.Add(lbl);
    }

    public void SetProgress(double fraction)
    {
        if (InvokeRequired) { Invoke(() => SetProgress(fraction)); return; }
        _bar.Value = Math.Max(0, Math.Min(100, (int)(fraction * 100)));
    }
}
