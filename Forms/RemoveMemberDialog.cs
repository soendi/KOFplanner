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
        Text = "Teammitglied entfernen";
        Size = new Size(420, 240);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(12);

        var msg = new Label
        {
            Left = 12, Top = 12, Width = 384, Height = 80,
            Text = $"{memberName} ist das einzige Teammitglied von \"{teamName}\", das das zugewiesene Fahrzeug\n{vehicle.VehicleNumber} ({vehicle.RequiredLicense}) führen darf.\n\nWas soll beim Entfernen geschehen?"
        };
        Controls.Add(msg);

        var btnKeep = new Button { Text = "Mitglied nicht entfernen", Left = 12, Top = 150, Width = 384, Height = 28 };
        var btnClear = new Button { Text = "Mitglied entfernen und Fahrzeug entfernen", Left = 12, Top = 184, Width = 384, Height = 28 };
        var btnChange = new Button { Text = "Mitglied entfernen und Fahrzeug wechseln", Left = 12, Top = 218, Width = 384, Height = 28 };

        btnKeep.Click += (_, _) => { Choice = RemoveMemberChoice.Cancel; DialogResult = DialogResult.Cancel; };
        btnClear.Click += (_, _) => { Choice = RemoveMemberChoice.KeepAndClearVehicle; DialogResult = DialogResult.OK; };
        btnChange.Click += (_, _) => { Choice = RemoveMemberChoice.KeepAndChangeVehicle; DialogResult = DialogResult.OK; };

        Controls.AddRange(new Control[] { btnKeep, btnClear, btnChange });
        CancelButton = btnKeep;
    }
}
