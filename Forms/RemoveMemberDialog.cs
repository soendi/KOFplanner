using KOFplanner.Models;

namespace KOFplanner.Forms;

internal enum RemoveMemberChoice
{
    Cancel,
    KeepAndClearVehicle,
    KeepAndChangeVehicle
}

internal sealed class RemoveMemberDialog : Form
{
    public RemoveMemberChoice Choice { get; private set; } = RemoveMemberChoice.Cancel;

    public RemoveMemberDialog(string teamName, string memberName, Vehicle vehicle, List<Vehicle> vehicles)
    {
        IconHelper.Apply(this);
        Text = "Teammitglied entfernen";
        Size = new Size(460, 320);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        var msg = new Label
        {
            Dock = DockStyle.Top,
            Padding = new Padding(12, 12, 12, 6),
            Height = 110,
            Text = $"{memberName} ist das einzige Teammitglied von \"{teamName}\", das das zugewiesene Fahrzeug\n{vehicle.VehicleNumber} ({vehicle.RequiredLicense}) führen darf.\n\nWas soll beim Entfernen geschehen?"
        };
        Controls.Add(msg);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12, 0, 12, 12),
            Height = 150
        };
        var btnKeep = new Button { Text = "Mitglied nicht entfernen", Width = 416, Height = 34 };
        var btnClear = new Button { Text = "Mitglied entfernen und Fahrzeug entfernen", Width = 416, Height = 34 };
        var btnChange = new Button { Text = "Mitglied entfernen und Fahrzeug wechseln", Width = 416, Height = 34 };

        btnKeep.Click += (_, _) => { Choice = RemoveMemberChoice.Cancel; DialogResult = DialogResult.Cancel; };
        btnClear.Click += (_, _) => { Choice = RemoveMemberChoice.KeepAndClearVehicle; DialogResult = DialogResult.OK; };
        btnChange.Click += (_, _) => { Choice = RemoveMemberChoice.KeepAndChangeVehicle; DialogResult = DialogResult.OK; };

        flow.Controls.AddRange(new Control[] { btnKeep, btnClear, btnChange });
        Controls.Add(flow);
        CancelButton = btnKeep;
    }
}
