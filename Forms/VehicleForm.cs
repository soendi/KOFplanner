using KOFplanner.Models;
using KOFplanner.Services;

namespace KOFplanner.Forms;

public class VehicleForm : Form
{
    private readonly DatabaseService _db;
    private readonly Vehicle? _vehicle;
    private readonly ComboBox _cmbLicense;
    private readonly TextBox _txtNumber, _txtPlate, _txtSeats;

    public VehicleForm(DatabaseService db, Vehicle? vehicle)
    {
        IconHelper.Apply(this);
        _db = db;
        _vehicle = vehicle;
        Text = vehicle == null ? "Neues Fahrzeug" : "Fahrzeug bearbeiten";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(400, 280);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10), RowCount = 5 };
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

        tlp.Controls.Add(new Label { Text = "Plätze (0 = keine Begrenzung):", Anchor = AnchorStyles.Left }, 0, 3);
        _txtSeats = new TextBox { Dock = DockStyle.Fill, Text = "0" };
        tlp.Controls.Add(_txtSeats, 1, 3);

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.LeftToRight, Height = 40 };
        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0x2E, 0x7D, 0x32), ForeColor = Color.White, Cursor = Cursors.Hand };
        btnOk.Click += (_, _) => Save();
        var btnCancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Width = 80, FlatStyle = FlatStyle.Flat };
        btnPanel.Controls.AddRange(new Control[] { btnOk, btnCancel });
        tlp.Controls.Add(btnPanel, 0, 4);
        tlp.SetColumnSpan(btnPanel, 2);
        Controls.Add(tlp);

        if (vehicle != null)
        {
            _cmbLicense.Text = vehicle.RequiredLicense;
            _txtNumber.Text = vehicle.VehicleNumber;
            _txtPlate.Text = vehicle.LicensePlate;
            _txtSeats.Text = vehicle.Seats.ToString();
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
        if (!int.TryParse(_txtSeats.Text, out var seats) || seats < 0)
        {
            MessageBox.Show("Bitte bei Plätze eine Zahl >= 0 eingeben.");
            DialogResult = DialogResult.None;
            return;
        }
        var number = _txtNumber.Text.Trim();
        var plate = _txtPlate.Text.Trim();
        var dup = _db.GetAllVehicles().FirstOrDefault(v =>
            v.Id != (_vehicle?.Id ?? 0) &&
            (string.Equals(v.VehicleNumber, number, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(v.LicensePlate, plate, StringComparison.OrdinalIgnoreCase)));
        if (dup != null)
        {
            MessageBox.Show($"Es existiert bereits ein Fahrzeug mit dieser Nummer oder diesem Kennzeichen:\n[{dup.RequiredLicense}] {dup.VehicleNumber} ({dup.LicensePlate})");
            DialogResult = DialogResult.None;
            return;
        }
        var veh = _vehicle ?? new Vehicle();
        veh.RequiredLicense = _cmbLicense.Text.Trim();
        veh.VehicleNumber = number;
        veh.LicensePlate = plate;
        veh.Seats = seats;
        try { _db.SaveVehicle(veh); }
        catch (Exception ex) { MessageBox.Show("Fahrzeug konnte nicht gespeichert werden:\n" + ex.Message); DialogResult = DialogResult.None; }
    }
}
