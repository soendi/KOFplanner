using KOFplanner.Models;
using KOFplanner.Services;

namespace KOFplanner.Forms;

public class VehicleForm : Form
{
    private readonly DatabaseService _db;
    private readonly Vehicle? _vehicle;
    private readonly ComboBox _cmbLicense;
    private readonly TextBox _txtNumber, _txtPlate;

    public VehicleForm(DatabaseService db, Vehicle? vehicle)
    {
        _db = db;
        _vehicle = vehicle;
        Text = vehicle == null ? "Neues Fahrzeug" : "Fahrzeug bearbeiten";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(400, 250);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10), RowCount = 4 };
        tlp.Controls.Add(new Label { Text = "Erforderl. Führerschein:", Anchor = AnchorStyles.Left }, 0, 0);
        _cmbLicense = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbLicense.Items.AddRange(Employee.AllLicenseCategories);
        tlp.Controls.Add(_cmbLicense, 1, 0);

        tlp.Controls.Add(new Label { Text = "Fahrzeugnummer:", Anchor = AnchorStyles.Left }, 0, 1);
        _txtNumber = new TextBox { Dock = DockStyle.Fill };
        tlp.Controls.Add(_txtNumber, 1, 1);

        tlp.Controls.Add(new Label { Text = "Kennzeichen:", Anchor = AnchorStyles.Left }, 0, 2);
        _txtPlate = new TextBox { Dock = DockStyle.Fill };
        tlp.Controls.Add(_txtPlate, 1, 2);

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.LeftToRight, Height = 40 };
        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
        btnOk.Click += (_, _) => Save();
        var btnCancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Width = 80 };
        btnPanel.Controls.AddRange(new Control[] { btnOk, btnCancel });
        tlp.Controls.Add(btnPanel, 0, 3);
        tlp.SetColumnSpan(btnPanel, 2);
        Controls.Add(tlp);

        if (vehicle != null)
        {
            _cmbLicense.Text = vehicle.RequiredLicense;
            _txtNumber.Text = vehicle.VehicleNumber;
            _txtPlate.Text = vehicle.LicensePlate;
        }
    }

    private void Save()
    {
        if (_cmbLicense.SelectedItem == null || string.IsNullOrWhiteSpace(_txtNumber.Text) || string.IsNullOrWhiteSpace(_txtPlate.Text))
        {
            MessageBox.Show("Bitte alle Felder ausfüllen.");
            DialogResult = DialogResult.None;
            return;
        }
        var veh = _vehicle ?? new Vehicle();
        veh.RequiredLicense = _cmbLicense.SelectedItem.ToString()!;
        veh.VehicleNumber = _txtNumber.Text.Trim();
        veh.LicensePlate = _txtPlate.Text.Trim();
        _db.SaveVehicle(veh);
    }
}
