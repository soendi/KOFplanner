namespace KOFplanner.Forms;

internal sealed class EmployeeDeleteDialog : Form
{
    public bool RemoveFromTeams { get; private set; }

    public EmployeeDeleteDialog(string employeeName, string teamNames)
    {
        Text = "Mitarbeiter löschen";
        Size = new Size(460, 240);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        var msg = new Label
        {
            Dock = DockStyle.Top,
            Padding = new Padding(12, 12, 12, 6),
            Height = 90,
            Text = $"{employeeName} ist noch in folgenden Team(s) zugeordnet:\n{teamNames}\n\n" +
                   "Soll der Mitarbeiter aus diesen Team(s) entfernt und dann gelöscht werden?"
        };
        Controls.Add(msg);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12, 0, 12, 12),
            Height = 90
        };
        var btnKeep = new Button { Text = "Person nicht löschen", Width = 416, Height = 34 };
        var btnRemove = new Button { Text = "Aus Team(s) entfernen und löschen", Width = 416, Height = 34 };

        btnKeep.Click += (_, _) => { DialogResult = DialogResult.Cancel; };
        btnRemove.Click += (_, _) => { RemoveFromTeams = true; DialogResult = DialogResult.OK; };

        flow.Controls.AddRange(new Control[] { btnKeep, btnRemove });
        Controls.Add(flow);
        CancelButton = btnKeep;
    }
}
