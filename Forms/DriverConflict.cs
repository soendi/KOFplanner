using System.Windows.Forms;
using KOFplanner.Models;

namespace KOFplanner.Forms;

internal enum DriverResolution
{
    CancelChange,
    ClearVehicle,
    ChangeVehicle
}

internal static class DriverConflict
{
    // Shows a dialog when a team change would leave no member able to drive the assigned vehicle.
    // Returns the chosen resolution, or null if the dialog was closed without a choice.
    public static DriverResolution? Show(string teamName, Vehicle veh, IWin32Window owner)
    {
        using var dlg = new Form
        {
            Text = "Kein Fahrer fürs Fahrzeug",
            Size = new Size(460, 320),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = new Font("Segoe UI", 9.5f)
        };

        var msg = new Label
        {
            Dock = DockStyle.Top,
            Padding = new Padding(12, 12, 12, 6),
            Height = 110,
            Text = $"Nach der Änderung kann kein Mitglied von Team \"{teamName}\" das zugewiesene Fahrzeug\n{veh.VehicleNumber} ({veh.RequiredLicense}) führen.\n\nMindestens ein Teammitglied muss das Fahrzeug lenken dürfen.\nWie soll fortgefahren werden?"
        };
        dlg.Controls.Add(msg);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12, 0, 12, 12),
            Height = 150
        };
        var btnCancel = new Button { Text = "Änderung rückgängig", Width = 416, Height = 34, DialogResult = DialogResult.Cancel };
        var btnClear = new Button { Text = "Fahrzeugzuweisung entfernen", Width = 416, Height = 34 };
        var btnChange = new Button { Text = "Anderes Fahrzeug wählen", Width = 416, Height = 34 };

        var resolution = (DriverResolution?)null;
        btnClear.Click += (_, _) => { resolution = DriverResolution.ClearVehicle; dlg.DialogResult = DialogResult.OK; };
        btnChange.Click += (_, _) => { resolution = DriverResolution.ChangeVehicle; dlg.DialogResult = DialogResult.OK; };

        flow.Controls.AddRange(new Control[] { btnCancel, btnClear, btnChange });
        dlg.Controls.Add(flow);
        dlg.CancelButton = btnCancel;
        return dlg.ShowDialog(owner) == DialogResult.OK ? resolution : DriverResolution.CancelChange;
    }

    public static Vehicle? PickVehicle(List<Vehicle> vehicles, Vehicle? exclude, IWin32Window owner)
    {
        using var f = new Form
        {
            Text = "Ersatzfahrzeug wählen",
            Size = new Size(360, 240),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = new Font("Segoe UI", 9.5f)
        };
        var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 20, Top = 30, Width = 300 };
        foreach (var v in vehicles.Where(v => exclude == null || v.Id != exclude.Id).OrderBy(v => v.VehicleNumber))
            cmb.Items.Add(v);
        cmb.DisplayMember = "VehicleNumber";
        if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        var ok = new Button { Text = "OK", Left = 20, Top = 130, Width = 120, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Abbrechen", Left = 160, Top = 130, Width = 120, DialogResult = DialogResult.Cancel };
        f.Controls.AddRange(new Control[] { cmb, ok, cancel });
        f.AcceptButton = ok;
        f.CancelButton = cancel;
        return f.ShowDialog(owner) == DialogResult.OK && cmb.SelectedItem is Vehicle sel ? sel : null;
    }
}
