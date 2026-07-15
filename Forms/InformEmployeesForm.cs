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
    private readonly FlowLayoutPanel _flowSites, _flowTeams, _flowEmployees;
    private readonly TextBox _txtLog;

    public InformEmployeesForm(DatabaseService db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
        _notify = new NotificationService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KOFplanner"), settings);

        Font = new Font("Segoe UI", 9.5f);

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(12) };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Range
        tlp.Controls.Add(new Label { Text = "Zeitraum:", Anchor = AnchorStyles.Left }, 0, 0);
        _cmbRange = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbRange.Items.AddRange(new object[] { "Aktuelle Woche", "Nächste Woche", "Aktueller Monat", "Nächster Monat", "Spannweite" });
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
        _flowSites = MakeCheckGroup(_db.GetAllSites().OrderBy(x => x.Name).Select(s => (object)s), s => ((ConstructionSite)s).Name);
        tlp.Controls.Add(_flowSites, 1, 2);

        // Teams
        tlp.Controls.Add(new Label { Text = "Teams:", Anchor = AnchorStyles.Top }, 0, 3);
        _flowTeams = MakeCheckGroup(_db.GetAllTeams().OrderBy(x => x.Name).Select(t => (object)t), t => ((Team)t).Name);
        tlp.Controls.Add(_flowTeams, 1, 3);

        // Employees
        tlp.Controls.Add(new Label { Text = "Mitarbeiter:", Anchor = AnchorStyles.Top }, 0, 4);
        _flowEmployees = MakeCheckGroup(_db.GetAllEmployees().OrderBy(x => x.LastName).Select(e => (object)e), e => ((Employee)e).FullName);
        tlp.Controls.Add(_flowEmployees, 1, 4);

        // Buttons
        var flp = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnEmail = new Button { Text = "Per E-Mail senden", Width = 150, Height = 30 };
        var btnPrint = new Button { Text = "Drucken", Width = 120, Height = 30 };
        btnEmail.Click += (_, _) => Run(false, true);
        btnPrint.Click += (_, _) => Run(true, false);
        flp.Controls.AddRange(new Control[] { btnEmail, btnPrint });
        tlp.Controls.Add(flp, 1, 5);

        // Log
        tlp.Controls.Add(new Label { Text = "Protokoll:", Anchor = AnchorStyles.Top }, 0, 6);
        _txtLog = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true };
        tlp.Controls.Add(_txtLog, 1, 5);

        Controls.Add(tlp);
        UpdateRangeDates();
    }

    private static FlowLayoutPanel MakeCheckGroup(IEnumerable<object> items, Func<object, string> textSelector)
    {
        var gb = new GroupBox { Dock = DockStyle.Fill, Padding = new Padding(6) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = true };
        foreach (var item in items)
        {
            var cb = new CheckBox { Text = textSelector(item), AutoSize = true, Checked = true, Margin = new Padding(3, 2, 10, 2), Tag = item };
            flow.Controls.Add(cb);
        }
        gb.Controls.Add(flow);
        return flow;
    }

    private static IEnumerable<object> CheckedItems(FlowLayoutPanel flow) =>
        flow.Controls.OfType<CheckBox>().Where(c => c.Checked).Select(c => c.Tag).OfType<object>();

    private void UpdateRangeDates()
    {
        var today = DateTime.Today;
        switch (_cmbRange.SelectedItem?.ToString())
        {
            case "Aktuelle Woche":
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                _dtpFrom.Value = today.AddDays(-diff);
                _dtpUntil.Value = _dtpFrom.Value.AddDays(6);
                break;
            case "Nächste Woche":
                int diffN = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                var thisMon = today.AddDays(-diffN);
                _dtpFrom.Value = thisMon.AddDays(7);
                _dtpUntil.Value = _dtpFrom.Value.AddDays(6);
                break;
            case "Aktueller Monat":
                _dtpFrom.Value = new DateTime(today.Year, today.Month, 1);
                _dtpUntil.Value = _dtpFrom.Value.AddMonths(1).AddDays(-1);
                break;
            case "Nächster Monat":
                var next = today.AddMonths(1);
                _dtpFrom.Value = new DateTime(next.Year, next.Month, 1);
                _dtpUntil.Value = _dtpFrom.Value.AddMonths(1).AddDays(-1);
                break;
            default:
                if (_dtpFrom.Value > _dtpUntil.Value) _dtpUntil.Value = _dtpFrom.Value;
                break;
        }
    }

    private void Run(bool print, bool email)
    {
        var from = _dtpFrom.Value.Date;
        var until = _dtpUntil.Value.Date;
        var sites = CheckedItems(_flowSites).Cast<ConstructionSite>().Select(s => s.Id).ToHashSet();
        var teams = CheckedItems(_flowTeams).Cast<Team>().Select(t => t.Id).ToHashSet();

        var all = _db.GetAllAssignments(from, until);
        var filtered = all.Where(a =>
            (sites.Count == 0 || sites.Contains(a.ConstructionSiteId)) &&
            (teams.Count == 0 || (a.TeamId.HasValue && teams.Contains(a.TeamId.Value))))
            .ToList();

        // Teams with members (assignments only carry Team.Id/Name, not members)
        var teamMembers = _db.GetAllTeams().ToDictionary(t => t.Id, t => t.Members);
        var employees = _db.GetAllEmployees();

        // Build the set of employees that need to be informed.
        var empIds = new HashSet<int>();
        foreach (var a in filtered)
        {
            if (a.TeamId.HasValue && teamMembers.TryGetValue(a.TeamId.Value, out var members))
                foreach (var m in members) empIds.Add(m.Id);
            else if (a.EmployeeId.HasValue)
                empIds.Add(a.EmployeeId.Value);
        }

        // Explicitly selected employees
        var allEmps = _flowEmployees.Controls.OfType<CheckBox>().ToList();
        var allEmpsChecked = allEmps.All(c => c.Checked);
        var explicitEmps = CheckedItems(_flowEmployees).Cast<Employee>().Select(e => e.Id).ToHashSet();
        if (allEmpsChecked)
            empIds.UnionWith(explicitEmps);
        else
            empIds = empIds.Intersect(explicitEmps).ToHashSet();

        if (empIds.Count == 0) { Log("Keine zugewiesenen Mitarbeiter im Zeitraum gefunden."); return; }

        var settings = _settings.Load();
        int done = 0, failed = 0;
        foreach (var id in empIds)
        {
            var emp = employees.FirstOrDefault(e => e.Id == id);
            if (emp == null) continue;

            // One PDF/e-mail per employee, combining every assignment that concerns them
            // (site, team membership or explicit selection) - no duplicates.
            var empAss = filtered
                .Where(a => (a.TeamId.HasValue && teamMembers.TryGetValue(a.TeamId.Value, out var mem) && mem.Any(m => m.Id == id))
                         || (a.EmployeeId.HasValue && a.EmployeeId.Value == id))
                .ToList();
            if (empAss.Count == 0) continue;
            try
            {
                var pdf = _notify.GeneratePdf(emp, from, until, empAss);
                if (print) _notify.PrintPdf(pdf, settings.PrinterName);
                if (email)
                {
                    if (string.IsNullOrWhiteSpace(emp.Email))
                        Log($"{emp.FullName}: keine E-Mail-Adresse hinterlegt.");
                    else if (_notify.SendEmail(pdf, emp, settings))
                        Log($"{emp.FullName}: E-Mail gesendet.");
                    else
                        Log($"{emp.FullName}: E-Mail fehlgeschlagen (SMTP-Einstellungen prüfen).");
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
