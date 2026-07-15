using KOFplanner.Models;
using KOFplanner.Services;

namespace KOFplanner.Forms;

public class InformEmployeesForm : UserControl
{
    private readonly DatabaseService _db;
    private readonly SettingsService _settings;
    private readonly NotificationService _notify;
    private readonly ComboBox _cmbRange;
    private readonly DateTimePicker _dtpFrom, _dtpUntil;
    private readonly CheckedListBox _clbSites, _clbTeams;
    private readonly TextBox _txtLog;

    public InformEmployeesForm(DatabaseService db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
        _notify = new NotificationService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KOFplanner"), settings);

        Font = new Font("Segoe UI", 9.5f);

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(12) };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Range
        tlp.Controls.Add(new Label { Text = "Zeitraum:", Anchor = AnchorStyles.Left }, 0, 0);
        _cmbRange = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbRange.Items.AddRange(new object[] { "Woche", "Monat", "Spannweite" });
        _cmbRange.SelectedIndex = 0;
        _cmbRange.SelectedIndexChanged += (_, _) => UpdateRangeDates();
        tlp.Controls.Add(_cmbRange, 1, 0);

        // Dates
        tlp.Controls.Add(new Label { Text = "Von / Bis:", Anchor = AnchorStyles.Left }, 0, 1);
        var pnlDates = new Panel { Dock = DockStyle.Fill };
        _dtpFrom = new DateTimePicker { Left = 0, Top = 2, Width = 130, Format = DateTimePickerFormat.Short };
        _dtpUntil = new DateTimePicker { Left = 140, Top = 2, Width = 130, Format = DateTimePickerFormat.Short };
        pnlDates.Controls.AddRange(new Control[] { _dtpFrom, _dtpUntil });
        tlp.Controls.Add(pnlDates, 1, 1);

        // Sites
        tlp.Controls.Add(new Label { Text = "Baustellen:", Anchor = AnchorStyles.Top }, 0, 2);
        _clbSites = new CheckedListBox { Dock = DockStyle.Fill };
        foreach (var s in _db.GetAllSites().OrderBy(x => x.Name))
            _clbSites.Items.Add(s, true);
        tlp.Controls.Add(_clbSites, 1, 2);

        // Teams
        tlp.Controls.Add(new Label { Text = "Teams:", Anchor = AnchorStyles.Top }, 0, 3);
        _clbTeams = new CheckedListBox { Dock = DockStyle.Fill };
        foreach (var t in _db.GetAllTeams().OrderBy(x => x.Name))
            _clbTeams.Items.Add(t, true);
        tlp.Controls.Add(_clbTeams, 1, 3);

        // Buttons
        var flp = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnEmail = new Button { Text = "Per E-Mail senden", Width = 150, Height = 30 };
        var btnPrint = new Button { Text = "Drucken", Width = 120, Height = 30 };
        btnEmail.Click += (_, _) => Run(false, true);
        btnPrint.Click += (_, _) => Run(true, false);
        flp.Controls.AddRange(new Control[] { btnEmail, btnPrint });
        tlp.Controls.Add(flp, 1, 4);

        // Log
        tlp.Controls.Add(new Label { Text = "Protokoll:", Anchor = AnchorStyles.Top }, 0, 5);
        _txtLog = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true };
        tlp.Controls.Add(_txtLog, 1, 5);

        Controls.Add(tlp);
        UpdateRangeDates();
    }

    private void UpdateRangeDates()
    {
        var today = DateTime.Today;
        switch (_cmbRange.SelectedItem?.ToString())
        {
            case "Woche":
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                _dtpFrom.Value = today.AddDays(-diff);
                _dtpUntil.Value = _dtpFrom.Value.AddDays(6);
                break;
            case "Monat":
                _dtpFrom.Value = new DateTime(today.Year, today.Month, 1);
                _dtpUntil.Value = _dtpFrom.Value.AddMonths(1).AddDays(-1);
                break;
            default:
                if (_dtpFrom.Value > _dtpUntil.Value) _dtpUntil.Value = _dtpFrom.Value;
                break;
        }
    }

    private void Run(bool print, bool email)
    {
        if (print && email) { }
        var from = _dtpFrom.Value.Date;
        var until = _dtpUntil.Value.Date;
        var sites = _clbSites.CheckedItems.Cast<ConstructionSite>().Select(s => s.Id).ToHashSet();
        var teams = _clbTeams.CheckedItems.Cast<Team>().Select(t => t.Id).ToHashSet();

        var all = _db.GetAllAssignments(from, until);
        var filtered = all.Where(a =>
            (sites.Count == 0 || sites.Contains(a.ConstructionSiteId)) &&
            (teams.Count == 0 || (a.TeamId.HasValue && teams.Contains(a.TeamId.Value))))
            .ToList();

        var empIds = filtered
            .SelectMany(a => a.Team != null ? a.Team.Members.Select(m => m.Id) : (a.Employee != null ? new[] { a.Employee.Id } : Array.Empty<int>()))
            .Where(id => _db.GetAllEmployees().Any(e => e.Id == id))
            .ToHashSet();

        if (empIds.Count == 0) { Log("Keine zugewiesenen Mitarbeiter im Zeitraum gefunden."); return; }

        var settings = _settings.Load();
        int done = 0, failed = 0;
        foreach (var id in empIds)
        {
            var emp = _db.GetAllEmployees().First(e => e.Id == id);
            var empAss = filtered
                .Where(a => (a.Team != null && a.Team.Members.Any(m => m.Id == id)) || (a.Employee != null && a.Employee.Id == id))
                .ToList();
            if (empAss.Count == 0) continue;
            try
            {
                var pdf = _notify.GeneratePdf(emp, from, until, empAss);
                if (print) _notify.PrintPdf(pdf, settings.PrinterName);
                if (email)
                {
                    if (string.IsNullOrWhiteSpace(emp.Email))
                        Log($"{emp.FullName}: keine E-Mail-Adresse.");
                    else if (_notify.SendEmail(pdf, emp, settings))
                        Log($"{emp.FullName}: E-Mail gesendet.");
                    else
                        Log($"{emp.FullName}: E-Mail fehlgeschlagen.");
                }
                if (!email) Log($"{emp.FullName}: PDF erstellt{(print ? " + gedruckt" : "")}.");
                done++;
            }
            catch (Exception ex) { Log($"{emp.FullName}: Fehler - {ex.Message}"); failed++; }
        }
        Log($"Fertig. {done} erfolgreich, {failed} fehlgeschlagen.");
    }

    private void Log(string line) { _txtLog.AppendText(line + Environment.NewLine); _txtLog.ScrollToCaret(); }
}
