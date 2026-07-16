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
    private readonly ListBox _lstSites, _lstTeams, _lstEmployees;
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
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
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

        // Sites (multi-select list; STRG/Klick für Mehrfachauswahl)
        tlp.Controls.Add(new Label { Text = "Baustellen:", Anchor = AnchorStyles.Top }, 0, 2);
        _lstSites = MakeMultiList(_db.GetAllSites().OrderBy(x => x.Name).Select(s => (object)s), s => ((ConstructionSite)s).Name);
        tlp.Controls.Add(_lstSites, 1, 2);

        // Teams
        tlp.Controls.Add(new Label { Text = "Teams:", Anchor = AnchorStyles.Top }, 0, 3);
        _lstTeams = MakeMultiList(_db.GetAllTeams().OrderBy(x => x.Name).Select(t => (object)t), t => ((Team)t).Name);
        tlp.Controls.Add(_lstTeams, 1, 3);

        // Employees
        tlp.Controls.Add(new Label { Text = "Mitarbeiter:", Anchor = AnchorStyles.Top }, 0, 4);
        _lstEmployees = MakeMultiList(_db.GetAllEmployees().OrderBy(x => x.LastName).Select(e => (object)e), e => ((Employee)e).FullName);
        tlp.Controls.Add(_lstEmployees, 1, 4);

        // Button
        var flp = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnSend = new Button { Text = "Infos verschicken", Width = 150, Height = 30, BackColor = Color.FromArgb(0x2E, 0x7D, 0x32), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnSend.Click += (_, _) => Inform();
        flp.Controls.Add(btnSend);
        tlp.Controls.Add(flp, 1, 5);

        // Log (>= 10 Zeilen)
        tlp.Controls.Add(new Label { Text = "Protokoll:", Anchor = AnchorStyles.Top }, 0, 6);
        _txtLog = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true };
        tlp.Controls.Add(_txtLog, 1, 6);

        Controls.Add(tlp);
        UpdateRangeDates();
    }

    private static ListBox MakeMultiList(IEnumerable<object> items, Func<object, string> textSelector)
    {
        var lst = new ListBox { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended, Sorted = false };
        foreach (var item in items)
            lst.Items.Add(new ListItem(item, textSelector(item)));
        return lst;
    }

    private sealed class ListItem
    {
        public object Value;
        public string Text = "";
        public ListItem(object value, string text) { Value = value; Text = text; }
        public override string ToString() => Text;
    }

    private static IEnumerable<object> SelectedTags(ListBox lst) =>
        lst.SelectedItems.Cast<ListItem>().Select(i => i.Value);

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

    // Collects the distinct employees for the chosen range + selected sites/teams/employees.
    private List<Employee> CollectEmployees(out List<Assignment> filtered, out DateTime from, out DateTime until)
    {
        from = _dtpFrom.Value.Date;
        until = _dtpUntil.Value.Date;
        var sites = SelectedTags(_lstSites).Cast<ConstructionSite>().Select(s => s.Id).ToHashSet();
        var teams = SelectedTags(_lstTeams).Cast<Team>().Select(t => t.Id).ToHashSet();

        var all = _db.GetAllAssignments(from, until);
        filtered = all.Where(a =>
            (sites.Count == 0 || sites.Contains(a.ConstructionSiteId)) &&
            (teams.Count == 0 || (a.TeamId.HasValue && teams.Contains(a.TeamId.Value))))
            .ToList();

        var teamMembers = _db.GetAllTeams().ToDictionary(t => t.Id, t => t.Members);
        var employees = _db.GetAllEmployees();

        var empIds = new HashSet<int>();
        foreach (var a in filtered)
        {
            if (a.TeamId.HasValue && teamMembers.TryGetValue(a.TeamId.Value, out var members))
                foreach (var m in members) empIds.Add(m.Id);
            else if (a.EmployeeId.HasValue)
                empIds.Add(a.EmployeeId.Value);
        }

        // Explicitly selected employees: only restrict the result when at least one is selected.
        // If none are selected, the employee list is not used as a filter.
        if (_lstEmployees.SelectedItems.Count > 0)
        {
            var explicitEmps = SelectedTags(_lstEmployees).Cast<Employee>().Select(e => e.Id).ToHashSet();
            empIds = empIds.Intersect(explicitEmps).ToHashSet();
        }

        // Resolve distinct employees (dedup is inherent via the HashSet of ids).
        return employees.Where(e => empIds.Contains(e.Id)).OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ToList();
    }

    private void Inform()
    {
        var emps = CollectEmployees(out var filtered, out var from, out var until);
        if (emps.Count == 0) { Log("Keine zugewiesenen Mitarbeiter im Zeitraum gefunden."); return; }

        using var preview = new InformPreviewForm(emps);
        if (preview.ShowDialog(this) != DialogResult.OK) return;

        var choices = preview.Choices;
        var employees = _db.GetAllEmployees();
        var teamMembers = _db.GetAllTeams().ToDictionary(t => t.Id, t => t.Members);
        var settings = _settings.Load();

        int done = 0, failed = 0;
        foreach (var ch in choices)
        {
            if (!ch.Email && !ch.Print) continue;
            var emp = employees.FirstOrDefault(e => e.Id == ch.Employee.Id);
            if (emp == null) continue;

            var empAss = filtered
                .Where(a => (a.TeamId.HasValue && teamMembers.TryGetValue(a.TeamId.Value, out var mem) && mem.Any(m => m.Id == emp.Id))
                         || (a.EmployeeId.HasValue && a.EmployeeId.Value == emp.Id))
                .ToList();
            if (empAss.Count == 0) continue;

            try
            {
                var pdf = _notify.GeneratePdf(emp, from, until, empAss);
                if (ch.Print) _notify.PrintPdf(pdf, settings.PrinterName);
                if (ch.Email)
                {
                    if (string.IsNullOrWhiteSpace(emp.Email))
                        Log($"{emp.FullName}: keine E-Mail-Adresse hinterlegt.");
                    else if (_notify.SendEmail(pdf, emp, settings))
                        Log($"{emp.FullName}: E-Mail gesendet.");
                    else
                        Log($"{emp.FullName}: E-Mail fehlgeschlagen (SMTP-Einstellungen prüfen).");
                }
                if (ch.Print && !ch.Email) Log($"{emp.FullName}: gedruckt.");
                done++;
            }
            catch (Exception ex) { Log($"{emp.FullName}: Fehler - {ex.Message}"); failed++; }
        }
        Log($"Fertig. {done} verarbeitet, {failed} fehlgeschlagen.");
    }

    private void Log(string line) { _txtLog.AppendText(line + Environment.NewLine); _txtLog.ScrollToCaret(); }
}

// Preview dialog listing each person with E-Mail / Druck checkboxes.
internal sealed class InformPreviewForm : Form
{
    public List<EmployeeChoice> Choices { get; } = new();

    public InformPreviewForm(List<Employee> employees)
    {
        Text = "Personen informieren";
        Size = new Size(520, 460);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(10) };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.Fixed3D
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Person", ReadOnly = true, FillWeight = 100 });
        var colMail = new DataGridViewCheckBoxColumn { Name = "Mail", HeaderText = "E-Mail", Width = 70, FillWeight = 20 };
        var colPrint = new DataGridViewCheckBoxColumn { Name = "Print", HeaderText = "Druck", Width = 70, FillWeight = 20 };
        grid.Columns.Add(colMail);
        grid.Columns.Add(colPrint);

        foreach (var emp in employees)
        {
            var hasMail = !string.IsNullOrWhiteSpace(emp.Email);
            var paper = emp.PaperPrint;
            grid.Rows.Add(emp.FullName, hasMail, paper);
            var row = grid.Rows[grid.Rows.Count - 1];
            row.Tag = emp;
            row.Cells[1].ReadOnly = !hasMail;   // E-Mail only selectable if address present
            row.Cells[2].ReadOnly = !paper;     // Druck only selectable if paper preference set
        }
        tlp.Controls.Add(grid, 0, 0);
        tlp.SetColumnSpan(grid, 3);

        var flp = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnOk = new Button { Text = "Senden", Width = 120, Height = 30, BackColor = Color.FromArgb(0x2E, 0x7D, 0x32), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Abbrechen", Width = 120, Height = 30, DialogResult = DialogResult.Cancel };
        btnOk.Click += (_, _) =>
        {
            Choices.Clear();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Tag is not Employee emp) continue;
                var email = row.Cells[1].Value is bool b1 && b1;
                var print = row.Cells[2].Value is bool b2 && b2;
                Choices.Add(new EmployeeChoice { Employee = emp, Email = email, Print = print });
            }
        };
        flp.Controls.AddRange(new Control[] { btnCancel, btnOk });
        tlp.Controls.Add(flp, 2, 1);

        Controls.Add(tlp);
        CancelButton = btnCancel;
    }
}

internal sealed class EmployeeChoice
{
    public Employee Employee = null!;
    public bool Email;
    public bool Print;
}
