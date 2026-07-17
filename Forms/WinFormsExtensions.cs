using System.Windows.Forms;

namespace KOFplanner;

internal static class WinFormsExtensions
{
    public static void AddRow(this TableLayoutPanel panel, string label, Control control)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, row);
        panel.Controls.Add(control, 1, row);
    }
}
