using KOFplanner.Models;
using KOFplanner.Services;

namespace KOFplanner.Forms;

public class SiteForm : Form
{
    private readonly DatabaseService _db;
    private readonly SettingsService _settings;
    private readonly ConstructionSite? _site;
    private readonly TextBox _txtName, _txtAddress;
    private readonly DateTimePicker _dtpStart, _dtpEnd;
    private readonly CheckBox _chkEnd;
    private readonly Label _lblStatus;

    public SiteForm(DatabaseService db, SettingsService settings, ConstructionSite? site)
    {
        IconHelper.Apply(this);
        _db = db;
        _settings = settings;
        _site = site;
        Text = site == null ? "Neue Baustelle" : "Baustelle bearbeiten";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(460, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10), RowCount = 6 };
        tlp.Controls.Add(new Label { Text = "Name:", Anchor = AnchorStyles.Left }, 0, 0);
        _txtName = new TextBox { Dock = DockStyle.Fill, Text = site?.Name ?? "" };
        tlp.Controls.Add(_txtName, 1, 0);

        tlp.Controls.Add(new Label { Text = "Adresse:", Anchor = AnchorStyles.Left }, 0, 1);
        _txtAddress = new TextBox { Dock = DockStyle.Fill, Text = site?.Address ?? "" };
        tlp.Controls.Add(_txtAddress, 1, 1);

        tlp.Controls.Add(new Label { Text = "Startdatum:", Anchor = AnchorStyles.Left }, 0, 2);
        _dtpStart = new DateTimePicker { Dock = DockStyle.Fill, Value = site?.StartDate ?? DateTime.Today };
        tlp.Controls.Add(_dtpStart, 1, 2);

        tlp.Controls.Add(new Label { Text = "Enddatum:", Anchor = AnchorStyles.Left }, 0, 3);
        var endPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
        _chkEnd = new CheckBox { Text = "kein Enddatum", AutoSize = true, Checked = site?.EndDate == null };
        _dtpEnd = new DateTimePicker { Value = site?.EndDate ?? DateTime.Today.AddMonths(1), Enabled = !_chkEnd.Checked };
        _chkEnd.CheckedChanged += (s, e) => _dtpEnd.Enabled = !_chkEnd.Checked;
        endPanel.Controls.Add(_dtpEnd);
        endPanel.Controls.Add(_chkEnd);
        tlp.Controls.Add(endPanel, 1, 3);

        _lblStatus = new Label { Text = "", Dock = DockStyle.Fill, ForeColor = Color.Gray, AutoSize = false };
        tlp.Controls.Add(_lblStatus, 0, 4);
        tlp.SetColumnSpan(_lblStatus, 2);

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.LeftToRight, Height = 40 };
        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
        btnOk.Click += (_, _) => Save();
        var btnCancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Width = 80 };
        btnPanel.Controls.AddRange(new Control[] { btnOk, btnCancel });
        tlp.Controls.Add(btnPanel, 0, 5);
        tlp.SetColumnSpan(btnPanel, 2);
        Controls.Add(tlp);
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("Bitte Namen eingeben.");
            DialogResult = DialogResult.None;
            return;
        }
        var name = _txtName.Text.Trim();
        var address = _txtAddress.Text.Trim();
        var dup = _db.GetAllSites().FirstOrDefault(s =>
            s.Id != (_site?.Id ?? 0) &&
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals((s.Address ?? "").Trim(), address, StringComparison.OrdinalIgnoreCase));
        if (dup != null)
        {
            MessageBox.Show($"Es existiert bereits eine Baustelle mit dem Namen \"{dup.Name}\" an der Adresse \"{dup.Address}\".");
            DialogResult = DialogResult.None;
            return;
        }
        var site = _site ?? new ConstructionSite();
        site.Name = name;
        site.Address = _txtAddress.Text.Trim();
        site.StartDate = _dtpStart.Value;
        site.EndDate = _chkEnd.Checked ? null : _dtpEnd.Value;

        // Fahrdistanz/-zeit von der Heimatadresse zur Baustelle abfragen (Straße/Auto).
        var home = _settings.Load().HomeAddress;
        if (!string.IsNullOrWhiteSpace(home) && !string.IsNullOrWhiteSpace(site.Address))
        {
            _lblStatus.Text = "Fahrdistanz wird abgefragt …";
            var r = new RoutingService().Compute(home, site.Address, out var err);
            if (r != null)
            {
                site.DistanceKm = r.DistanceKm;
                site.DurationMinutes = r.DurationMinutes;
                _lblStatus.Text = $"Fahrt: {r.DistanceKm:0.0} km, {site.DurationText}";
            }
            else
            {
                _lblStatus.Text = "Fahrdistanz nicht ermittelbar: " + (err ?? "unbekannt");
            }
        }
        else
        {
            site.DistanceKm = 0;
            site.DurationMinutes = 0;
        }

        _db.SaveSite(site);
    }
}
