using KOFplanner.Services;

namespace KOFplanner.Forms;

public class SettingsForm : Form
{
    private readonly SettingsService _settings;
    private readonly TextBox _txtServer, _txtPort, _txtSender, _txtUser, _txtPass;
    private readonly CheckBox _chkSsl;
    private readonly ComboBox _cmbPrinter;
    private readonly TextBox _txtHome;

    public SettingsForm(SettingsService settings)
    {
        _settings = settings;
        var s = _settings.Load();

        Text = "Einstellungen";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(460, 440);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 11, Padding = new Padding(12) };
        for (int i = 0; i < 7; i++) tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        tlp.Controls.Add(new Label { Text = "E-Mail (SMTP)", Font = new Font("Segoe UI", 11, FontStyle.Bold) }, 0, 0);
        tlp.SetColumnSpan(tlp.Controls[^1], 2);

        tlp.Controls.Add(new Label { Text = "Server:", Anchor = AnchorStyles.Left }, 0, 1);
        _txtServer = new TextBox { Dock = DockStyle.Fill, Text = s.Email.Server };
        tlp.Controls.Add(_txtServer, 1, 1);

        tlp.Controls.Add(new Label { Text = "Port:", Anchor = AnchorStyles.Left }, 0, 2);
        _txtPort = new TextBox { Dock = DockStyle.Fill, Text = s.Email.Port.ToString() };
        tlp.Controls.Add(_txtPort, 1, 2);

        tlp.Controls.Add(new Label { Text = "SSL:", Anchor = AnchorStyles.Left }, 0, 3);
        _chkSsl = new CheckBox { Text = "Verschlüsselt", AutoSize = true, Checked = s.Email.UseSsl };
        tlp.Controls.Add(_chkSsl, 1, 3);

        tlp.Controls.Add(new Label { Text = "Absender:", Anchor = AnchorStyles.Left }, 0, 4);
        _txtSender = new TextBox { Dock = DockStyle.Fill, Text = s.Email.Sender };
        tlp.Controls.Add(_txtSender, 1, 4);

        tlp.Controls.Add(new Label { Text = "Benutzer:", Anchor = AnchorStyles.Left }, 0, 5);
        _txtUser = new TextBox { Dock = DockStyle.Fill, Text = s.Email.Username };
        tlp.Controls.Add(_txtUser, 1, 5);

        tlp.Controls.Add(new Label { Text = "Passwort:", Anchor = AnchorStyles.Left }, 0, 6);
        _txtPass = new TextBox { Dock = DockStyle.Fill, Text = s.Email.Password, UseSystemPasswordChar = true };
        tlp.Controls.Add(_txtPass, 1, 6);

        tlp.Controls.Add(new Label { Text = "Drucker", Font = new Font("Segoe UI", 11, FontStyle.Bold) }, 0, 7);
        tlp.SetColumnSpan(tlp.Controls[^1], 2);

        tlp.Controls.Add(new Label { Text = "Standard:", Anchor = AnchorStyles.Left }, 0, 8);
        _cmbPrinter = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        try { foreach (var p in new NotificationService("", _settings).GetPrinters()) _cmbPrinter.Items.Add(p); } catch { }
        if (_cmbPrinter.Items.Count > 0)
            _cmbPrinter.SelectedIndex = Math.Max(0, _cmbPrinter.Items.IndexOf(s.PrinterName));
        else
            _cmbPrinter.Items.Add("(keine Drucker gefunden)");
        tlp.Controls.Add(_cmbPrinter, 1, 8);

        tlp.Controls.Add(new Label { Text = "Heimatadresse", Font = new Font("Segoe UI", 11, FontStyle.Bold) }, 0, 9);
        tlp.SetColumnSpan(tlp.Controls[^1], 2);

        tlp.Controls.Add(new Label { Text = "Adresse:", Anchor = AnchorStyles.Left }, 0, 10);
        _txtHome = new TextBox { Dock = DockStyle.Fill, Text = s.HomeAddress };
        tlp.Controls.Add(_txtHome, 1, 10);

        var btnCancel = new Button { Text = "Abbrechen", Width = 100, Height = 32, DialogResult = DialogResult.Cancel };
        var btnOk = new Button { Text = "Speichern", Width = 100, Height = 32, DialogResult = DialogResult.OK };
        btnOk.Click += (_, _) => Save(s);
        var flp = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        flp.Controls.AddRange(new Control[] { btnOk, btnCancel });

        Controls.Add(tlp);
        Controls.Add(flp);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private void Save(AppSettings s)
    {
        s.Email.Server = _txtServer.Text.Trim();
        s.Email.Port = int.TryParse(_txtPort.Text, out var p) ? p : 587;
        s.Email.UseSsl = _chkSsl.Checked;
        s.Email.Sender = _txtSender.Text.Trim();
        s.Email.Username = _txtUser.Text.Trim();
        s.Email.Password = _txtPass.Text;
        s.PrinterName = _cmbPrinter.SelectedItem?.ToString() == "(keine Drucker gefunden)" ? "" : _cmbPrinter.Text;
        s.HomeAddress = _txtHome.Text.Trim();
        _settings.Save(s);
    }
}
