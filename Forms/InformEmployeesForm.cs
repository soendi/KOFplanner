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

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));   // Zeitraum
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Listen (3 Spalten)
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));   // Button
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));  // Protokoll

        // ---- Zeitraum ----
        var pnlRange = new Panel { Dock = DockStyle.Fill };
        pnlRange.Controls.Add(new Label { Text = "Zeitraum:", Location = new Point(0, 2), AutoSize = true });
        _cmbRange = new ComboBox { Left = 70, Top = 0, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbRange.Items.AddRange(new object[] { "Aktuelle Woche", "Nächste Woche", "Aktueller Monat", "Nächster Monat", "Spannweite" });
        _cmbRange.SelectedIndex = 0;
        _cmbRange.SelectedIndexChanged += (_, _) => UpdateRangeDates();
        pnlRange.Controls.Add(_cmbRange);
        pnlRange.Controls.Add(new Label { Text = "Von / Bis:", Location = new Point(0, 34), AutoSize = true });
        _dtpFrom = new DateTimePicker { Left = 70, Top = 32, Width = 120, Format = DateTimePickerFormat.Short };
        _dtpUntil = new DateTimePicker { Left = 200, Top = 32, Width = 120, Format = DateTimePickerFormat.Short };
        pnlRange.Controls.AddRange(new Control[] { _dtpFrom, _dtpUntil });
        tlp.Controls.Add(pnlRange, 0, 0);

        // ---- 3 Listen nebeneinander (hochformat) ----
        var lists = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        lists.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _lstSites = MakeMultiList(_db.GetAllSites().OrderBy(x => x.Name).Select(s => (object)s), s => ((ConstructionSite)s).Name);
        _lstTeams = MakeMultiList(_db.GetAllTeams().OrderBy(x => x.Name).Select(t => (object)t), t => ((Team)t).Name);
        _lstEmployees = MakeMultiList(_db.GetAllEmployees().OrderBy(x => x.LastName).Select(e => (object)e), e => ((Employee)e).FullName);
        // Bei markierter Baustelle den Zeitraum automatisch auf deren Laufzeit setzen,
        // damit die zugeordneten Einsätze gefunden werden.
        _lstSites.SelectedIndexChanged += (_, _) => AutoRangeFromSites();

        lists.Controls.Add(MakeListColumn("Baustellen", _lstSites), 0, 0);
        lists.Controls.Add(MakeListColumn("Teams", _lstTeams), 1, 0);
        lists.Controls.Add(MakeListColumn("Mitarbeiter", _lstEmployees), 2, 0);
        tlp.Controls.Add(lists, 0, 1);

        // ---- Button ----
        var flp = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnSend = new Button { Text = "Infos verschicken", Width = 150, Height = 30, BackColor = Color.FromArgb(0x2E, 0x7D, 0x32), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnSend.Click += (_, _) => Inform();
        flp.Controls.Add(btnSend);
        tlp.Controls.Add(flp, 0, 2);

        // ---- Protokoll ----
        tlp.Controls.Add(new Label { Text = "Protokoll:", Anchor = AnchorStyles.Top }, 0, 3);
        _txtLog = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true };
        tlp.Controls.Add(_txtLog, 0, 3);

        Controls.Add(tlp);
        UpdateRangeDates();
    }

    // Baut eine Spalte: Beschriftung oben, Liste füllt den Rest.
    private static Panel MakeListColumn(string caption, ListBox list)
    {
        var p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
        var lbl = new Label { Text = caption, Dock = DockStyle.Top, Height = 18, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        list.Dock = DockStyle.Fill;
        p.Controls.Add(list);
        p.Controls.Add(lbl);
        return p;
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

    // Aktualisiert die drei Listen, nachdem neue Datensätze angelegt wurden.
    public void RefreshLists()
    {
        Reload(_lstSites, _db.GetAllSites().OrderBy(x => x.Name).Select(s => (object)s), s => ((ConstructionSite)s).Name);
        Reload(_lstTeams, _db.GetAllTeams().OrderBy(x => x.Name).Select(t => (object)t), t => ((Team)t).Name);
        Reload(_lstEmployees, _db.GetAllEmployees().OrderBy(x => x.LastName).Select(e => (object)e), e => ((Employee)e).FullName);
    }

    private static void Reload(ListBox lst, IEnumerable<object> items, Func<object, string> textSelector)
    {
        var sel = lst.SelectedItems.Cast<ListItem>().Select(i => i.Value).ToHashSet();
        lst.Items.Clear();
        foreach (var item in items)
            lst.Items.Add(new ListItem(item, textSelector(item)));
        // Vorherige Auswahl wiederherstellen (anhand der Objekt-Referenz).
        for (int i = 0; i < lst.Items.Count; i++)
            if (lst.Items[i] is ListItem li && sel.Contains(li.Value))
                lst.SetSelected(i, true);
    }

    private void AutoRangeFromSites()
    {
        var sites = SelectedTags(_lstSites).Cast<ConstructionSite>().ToList();
        if (sites.Count == 0) return;
        var from = sites.Min(s => s.StartDate.Date);
        var until = sites.Max(s => s.EndDate?.Date ?? s.StartDate.Date.AddMonths(1));
        _dtpFrom.Value = from;
        _dtpUntil.Value = until;
    }

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

    // Collects the distinct employees and matching assignments for the chosen range.
    //  - Nothing selected: ALL assignments of ALL employees in the period.
    //  - Any selection: union of the selected categories (Baustelle / Team / Mitarbeiter).
    //    An assignment matches if it belongs to ANY selected category. Explicitly selected
    //    employees are always included with their own assignments in the period.
    private List<Employee> CollectEmployees(out List<Assignment> filtered, out DateTime from, out DateTime until)
    {
        from = _dtpFrom.Value.Date;
        until = _dtpUntil.Value.Date;
        var sites = SelectedTags(_lstSites).Cast<ConstructionSite>().Select(s => s.Id).ToHashSet();
        var teams = SelectedTags(_lstTeams).Cast<Team>().Select(t => t.Id).ToHashSet();
        var selectedEmps = SelectedTags(_lstEmployees).Cast<Employee>().Select(e => e.Id).ToHashSet();
        bool anyFilter = sites.Count > 0 || teams.Count > 0 || selectedEmps.Count > 0;

        var all = _db.GetAllAssignments(from, until);
        var teamMembers = _db.GetAllTeams().ToDictionary(t => t.Id, t => t.Members);
        var employees = _db.GetAllEmployees();

        bool MatchesSite(Assignment a) => sites.Count == 0 || sites.Contains(a.ConstructionSiteId);
        bool MatchesTeam(Assignment a) => teams.Count == 0 || (a.TeamId.HasValue && teams.Contains(a.TeamId.Value));
        bool MatchesEmployee(Assignment a)
        {
            if (selectedEmps.Count == 0) return true;
            if (a.EmployeeId.HasValue && selectedEmps.Contains(a.EmployeeId.Value)) return true;
            if (a.TeamId.HasValue && teamMembers.TryGetValue(a.TeamId.Value, out var mem))
                return mem.Any(m => selectedEmps.Contains(m.Id));
            return false;
        }

        var matched = anyFilter
            ? all.Where(a => MatchesSite(a) || MatchesTeam(a) || MatchesEmployee(a)).ToList()
            : all.ToList();

        var empIds = new HashSet<int>();

        // Employees from the matched assignments (team members + directly assigned employees).
        // Track which employees come from a selected BAUSTELLE so we can show them ALL their
        // assignments in the period (not only the ones belonging to that site).
        var siteEmployeeIds = new HashSet<int>();
        foreach (var a in matched)
        {
            if (a.TeamId.HasValue && teamMembers.TryGetValue(a.TeamId.Value, out var members))
            {
                foreach (var m in members) { empIds.Add(m.Id); if (sites.Contains(a.ConstructionSiteId)) siteEmployeeIds.Add(m.Id); }
            }
            else if (a.EmployeeId.HasValue)
            {
                empIds.Add(a.EmployeeId.Value);
                if (sites.Contains(a.ConstructionSiteId)) siteEmployeeIds.Add(a.EmployeeId.Value);
            }
        }

        // Explicitly selected employees are always included (with their own assignments).
        foreach (var id in selectedEmps) empIds.Add(id);

        // Filtered assignments for the per-employee PDFs:
        //  - the matched assignments, PLUS
        //  - ALL assignments of explicitly selected employees, PLUS
        //  - ALL assignments of employees tied to a selected Baustelle (they get their full period).
        var filteredSet = new HashSet<int>(matched.Select(a => a.Id));
        var filteredList = new List<Assignment>(matched);
        var expandIds = new HashSet<int>(selectedEmps);
        foreach (var id in siteEmployeeIds) expandIds.Add(id);
        if (expandIds.Count > 0)
        {
            foreach (var a in all)
            {
                bool belongs =
                    (a.EmployeeId.HasValue && expandIds.Contains(a.EmployeeId.Value)) ||
                    (a.TeamId.HasValue && teamMembers.TryGetValue(a.TeamId.Value, out var mem) && mem.Any(m => expandIds.Contains(m.Id)));
                if (belongs && filteredSet.Add(a.Id))
                    filteredList.Add(a);
            }
        }

        filtered = filteredList;
        return employees.Where(e => empIds.Contains(e.Id)).OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ToList();
    }

    private void Inform()
    {
        var emps = CollectEmployees(out var filtered, out var from, out var until);
        Log($"{emps.Count} Mitarbeiter im Zeitraum {from:dd.MM.yyyy} – {until:dd.MM.yyyy} gefunden.");
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
        IconHelper.Apply(this);
        Text = "Personen informieren";
        Size = new Size(520, 460);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

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

        var flp = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 4, 0, 0) };
        var btnOk = new Button { Text = "Senden", Width = 120, Height = 32, BackColor = Color.FromArgb(0x2E, 0x7D, 0x32), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Abbrechen", Width = 120, Height = 32, DialogResult = DialogResult.Cancel };
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
        tlp.Controls.Add(flp, 0, 1);

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
