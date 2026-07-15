using KOFplanner.Models;
using KOFplanner.Services;

namespace KOFplanner.Forms;

public class EmployeeForm : Form
{
    private readonly DatabaseService _db;
    private readonly Employee? _employee;
    private readonly TextBox _txtFirst, _txtLast, _txtEmail;
    private readonly CheckBox _chkLicense;
    private readonly CheckBox _chkPaper;
    private readonly CheckedListBox _clbCategories;

    public EmployeeForm(DatabaseService db, Employee? employee)
    {
        _db = db;
        _employee = employee;
        Text = employee == null ? "Neuer Mitarbeiter" : "Mitarbeiter bearbeiten";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(440, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10), RowCount = 6 };

        tlp.Controls.Add(new Label { Text = "Vorname:", Anchor = AnchorStyles.Left }, 0, 0);
        _txtFirst = new TextBox { Dock = DockStyle.Fill };
        tlp.Controls.Add(_txtFirst, 1, 0);

        tlp.Controls.Add(new Label { Text = "Nachname:", Anchor = AnchorStyles.Left }, 0, 1);
        _txtLast = new TextBox { Dock = DockStyle.Fill };
        tlp.Controls.Add(_txtLast, 1, 1);

        tlp.Controls.Add(new Label { Text = "E-Mail:", Anchor = AnchorStyles.Left }, 0, 2);
        _txtEmail = new TextBox { Dock = DockStyle.Fill };
        tlp.Controls.Add(_txtEmail, 1, 2);

        tlp.Controls.Add(new Label { Text = "Führerschein:", Anchor = AnchorStyles.Left }, 0, 3);
        _chkLicense = new CheckBox { Text = "Ja", AutoSize = true };
        _chkLicense.CheckedChanged += (_, _) => _clbCategories.Enabled = _chkLicense.Checked;
        tlp.Controls.Add(_chkLicense, 1, 3);

        tlp.Controls.Add(new Label { Text = "Kategorien:", Anchor = AnchorStyles.Top }, 0, 4);
        _clbCategories = new CheckedListBox { Dock = DockStyle.Fill, Enabled = false, Height = 100 };
        foreach (var cat in Employee.AllLicenseCategories)
            _clbCategories.Items.Add(cat, false);
        tlp.Controls.Add(_clbCategories, 1, 4);

        tlp.Controls.Add(new Label { Text = "Papierdruck:", Anchor = AnchorStyles.Left }, 0, 5);
        _chkPaper = new CheckBox { Text = "Einsatzplan als Papierausdruck senden", AutoSize = true };
        tlp.Controls.Add(_chkPaper, 1, 5);

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.LeftToRight, Height = 40 };
        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0x2E, 0x7D, 0x32), ForeColor = Color.White, Cursor = Cursors.Hand };
        btnOk.Click += (_, _) => Save();
        var btnCancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Width = 80, FlatStyle = FlatStyle.Flat };
        btnPanel.Controls.AddRange(new Control[] { btnOk, btnCancel });
        tlp.Controls.Add(btnPanel, 0, 6);
        tlp.SetColumnSpan(btnPanel, 2);
        Controls.Add(tlp);

        if (employee != null)
        {
            _txtFirst.Text = employee.FirstName;
            _txtLast.Text = employee.LastName;
            _txtEmail.Text = employee.Email;
            _chkLicense.Checked = employee.HasDriversLicense;
            _clbCategories.Enabled = employee.HasDriversLicense;
            _chkPaper.Checked = employee.PaperPrint;
            foreach (var cat in employee.GetLicenseList())
            {
                var idx = _clbCategories.Items.IndexOf(cat);
                if (idx >= 0) _clbCategories.SetItemChecked(idx, true);
            }
        }
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_txtFirst.Text) || string.IsNullOrWhiteSpace(_txtLast.Text))
        {
            MessageBox.Show("Bitte Vor- und Nachname eingeben.");
            DialogResult = DialogResult.None;
            return;
        }
        var emp = _employee ?? new Employee();
        emp.FirstName = _txtFirst.Text.Trim();
        emp.LastName = _txtLast.Text.Trim();
        emp.Email = _txtEmail.Text.Trim();
        emp.HasDriversLicense = _chkLicense.Checked;
        emp.SetLicenseList(_clbCategories.CheckedItems.Cast<string>().ToArray());
        emp.PaperPrint = _chkPaper.Checked;
        _db.SaveEmployee(emp);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _txtFirst.Focus();
    }
}
