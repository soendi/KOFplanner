using KOFplanner.Models;
using KOFplanner.Services;
using System.Drawing.Drawing2D;

namespace KOFplanner.Forms;

public class MainForm : Form
{
    private readonly DatabaseService _db;
    private readonly BackupService _backup;
    private readonly UpdateService _update;
    private DateTime _currentMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1);
    private int _calendarDayWidth, _calendarDayHeight;
    private readonly Point _calendarOrigin = new(15, 45);
    private List<Assignment> _monthAssignments = new();
    private List<Employee> _employees = new();
    private List<Team> _teams = new();
    private List<Vehicle> _vehicles = new();
    private List<ConstructionSite> _sites = new();
    private List<Vacation> _vacations = new();
    private List<Sickness> _sickness = new();

    // Tabs
    private readonly TabControl _tabControl;
    private readonly Panel _calendarPanel;
    private readonly Label _lblMonthYear;

    // Tab 2 controls (Employees & Teams)
    private readonly ListBox _lbEmployees, _lbTeams;
    private readonly Label _lblTeamMembers;
    private readonly ComboBox _cmbTeamVehicle;

    // Tab 3 controls (Vehicles)
    private readonly ListBox _lbVehicles;

    // Tab 4 controls (Sites)
    private readonly ListBox _lbSites;

    // Calendar drag selection
    private DateTime? _dragStartDate, _dragEndDate, _dragCurrentDate;
    private bool _isDragging;
    private Point _dragStartPoint;

    private static void StyleButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = Color.FromArgb(0x1B, 0x5E, 0x20);
        btn.BackColor = Color.FromArgb(0x2E, 0x7D, 0x32);
        btn.ForeColor = Color.White;
        btn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        btn.Cursor = Cursors.Hand;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x1B, 0x5E, 0x20);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(0x00, 0x5C, 0x00);
    }

    public MainForm(DatabaseService db, BackupService backup, UpdateService update)
    {
        _db = db;
        _backup = backup;
        _update = update;
        Text = "KOFplanner - Baustelleneinsatzplanung v" + _update.CurrentVersion;
        Size = new Size(1400, 850);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        var menu = new MenuStrip();
        var dateiMenu = menu.Items.Add("&Datei") as ToolStripMenuItem;
        dateiMenu!.DropDownItems.Add("Datenbank sichern...", null, async (_, _) => await DoBackup());
        dateiMenu.DropDownItems.Add("Google Drive Backup...", null, (_, _) => ConfigureBackup());
        dateiMenu.DropDownItems.Add(new ToolStripSeparator());
        dateiMenu.DropDownItems.Add("Beenden", null, (_, _) => Close());
        var hilfeMenu = menu.Items.Add("&Hilfe") as ToolStripMenuItem;
        hilfeMenu!.DropDownItems.Add("Nach Updates suchen...", null, async (_, _) => await CheckUpdate());
        hilfeMenu.DropDownItems.Add("Info", null, (_, _) => MessageBox.Show("KOFplanner v1.1.2.0\nAuthor: Lukas Sonderegger", "Info"));
        MainMenuStrip = menu;
        Controls.Add(menu);

        // Main TabControl
        _tabControl = new TabControl { Dock = DockStyle.Fill, ItemSize = new Size(240, 52) };
        _tabControl.Padding = new Point(28, 14);
        _tabControl.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabControl.DrawItem += (s, e) =>
        {
            var tab = _tabControl;
            var bg = e.State.HasFlag(DrawItemState.Selected) ? Color.FromArgb(0x2E, 0x7D, 0x32) : SystemColors.Control;
            var fg = e.State.HasFlag(DrawItemState.Selected) ? Color.White : Color.FromArgb(0x33, 0x33, 0x33);
            using var b = new SolidBrush(bg);
            e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, tab.TabPages[e.Index].Text, tab.Font, e.Bounds, fg, bg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        Controls.Add(_tabControl);

        // ========== TAB 1: KALENDER ==========
        var tabKalender = new TabPage("Kalender");
        _calendarPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AllowDrop = true };
        _calendarPanel.Paint += Calendar_Paint;
        _calendarPanel.MouseDown += Calendar_MouseDown;
        _calendarPanel.MouseMove += Calendar_MouseMove;
        _calendarPanel.MouseUp += Calendar_MouseUp;
        _calendarPanel.MouseClick += Calendar_MouseClick;

        var nav = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = SystemColors.Control };
        var btnPrev = new Button { Text = "<", Width = 40, Height = 28, Location = new Point(15, 6) };
        btnPrev.Click += (_, _) => { _currentMonth = _currentMonth.AddMonths(-1); _dragStartDate = _dragEndDate = null; RefreshCalendar(); };
        StyleButton(btnPrev);
        var btnNext = new Button { Text = ">", Width = 40, Height = 28, Location = new Point(60, 6) };
        btnNext.Click += (_, _) => { _currentMonth = _currentMonth.AddMonths(1); _dragStartDate = _dragEndDate = null; RefreshCalendar(); };
        StyleButton(btnNext);
        var btnToday = new Button { Text = "Heute", Width = 60, Height = 28, Location = new Point(105, 6) };
        btnToday.Click += (_, _) => { _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1); _dragStartDate = _dragEndDate = null; RefreshCalendar(); };
        StyleButton(btnToday);
        _lblMonthYear = new Label { Text = "", Location = new Point(180, 8), AutoSize = true, Font = new Font(Font.FontFamily, 12, FontStyle.Bold) };
        nav.Controls.AddRange(new Control[] { btnPrev, btnNext, btnToday, _lblMonthYear });
        tabKalender.Controls.Add(_calendarPanel);
        tabKalender.Controls.Add(nav);
        _tabControl.TabPages.Add(tabKalender);

        // ========== TAB 2: MITARBEITER & TEAMS ==========
        var tabMA = new TabPage("Mitarbeiter & Teams");
        var t2Split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 350 };

        // Left: Employees
        var empPanel = new Panel { Dock = DockStyle.Fill };
        empPanel.Controls.Add(new Label { Text = "Mitarbeiter", Dock = DockStyle.Top, Font = new Font(Font.FontFamily, 10, FontStyle.Bold) });
        _lbEmployees = new ListBox { Dock = DockStyle.Fill, DisplayMember = "FullName" };
        var empBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 30 };
        var btnEmpNew = new Button { Text = "Neu", Width = 75 }; btnEmpNew.Click += (_, _) => EditEmployee(null); StyleButton(btnEmpNew);
        var btnEmpEdit = new Button { Text = "Bearbeiten", Width = 90 }; btnEmpEdit.Click += (_, _) => { if (_lbEmployees.SelectedItem is Employee e) EditEmployee(e); }; StyleButton(btnEmpEdit);
        var btnEmpDel = new Button { Text = "Löschen", Width = 80 }; btnEmpDel.Click += (_, _) => { if (_lbEmployees.SelectedItem is Employee e) DeleteEmployee(e); }; StyleButton(btnEmpDel);
        empBtns.Controls.AddRange(new Control[] { btnEmpNew, btnEmpEdit, btnEmpDel });
        empPanel.Controls.Add(_lbEmployees);
        empPanel.Controls.Add(empBtns);
        t2Split.Panel1.Controls.Add(empPanel);

        // Right: Teams
        var teamPanel = new Panel { Dock = DockStyle.Fill };
        teamPanel.Controls.Add(new Label { Text = "Teams", Dock = DockStyle.Top, Font = new Font(Font.FontFamily, 10, FontStyle.Bold) });
        _lbTeams = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Name" };
        _lblTeamMembers = new Label { Text = "", Dock = DockStyle.Bottom, Height = 50, AutoEllipsis = true, Padding = new Padding(3) };
        _lbTeams.SelectedIndexChanged += (_, _) => UpdateTeamDetails();
        var teamBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 30 };
        var btnTeamNew = new Button { Text = "Neu", Width = 75 }; btnTeamNew.Click += (_, _) => EditTeam(null); StyleButton(btnTeamNew);
        var btnTeamEdit = new Button { Text = "Bearbeiten", Width = 90 }; btnTeamEdit.Click += (_, _) => { if (_lbTeams.SelectedItem is Team t) EditTeam(t); }; StyleButton(btnTeamEdit);
        var btnTeamDel = new Button { Text = "Löschen", Width = 80 }; btnTeamDel.Click += (_, _) => { if (_lbTeams.SelectedItem is Team t) DeleteTeam(t); }; StyleButton(btnTeamDel);
        teamBtns.Controls.AddRange(new Control[] { btnTeamNew, btnTeamEdit, btnTeamDel });

        // Vehicle assignment to team
        var vehAssignPanel = new GroupBox { Text = "Fahrzeug zu Team", Dock = DockStyle.Bottom, Height = 50 };
        var vehFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(5) };
        vehFlow.Controls.Add(new Label { Text = "Fahrzeug:", AutoSize = true, Anchor = AnchorStyles.Left });
        _cmbTeamVehicle = new ComboBox { Width = 200, DisplayMember = "ToString" };
        vehFlow.Controls.Add(_cmbTeamVehicle);
        var btnAssignVeh = new Button { Text = "Zuweisen", Width = 85 };
        btnAssignVeh.Click += (_, _) => AssignVehicleToTeam(); StyleButton(btnAssignVeh);
        vehFlow.Controls.Add(btnAssignVeh);
        vehAssignPanel.Controls.Add(vehFlow);

        teamPanel.Controls.Add(_lbTeams);
        teamPanel.Controls.Add(_lblTeamMembers);
        teamPanel.Controls.Add(teamBtns);
        teamPanel.Controls.Add(vehAssignPanel);
        t2Split.Panel2.Controls.Add(teamPanel);
        tabMA.Controls.Add(t2Split);
        _tabControl.TabPages.Add(tabMA);

        // ========== TAB 3: FAHRZEUGE ==========
        var tabVeh = new TabPage("Fahrzeuge");
        _lbVehicles = new ListBox { Dock = DockStyle.Fill, DisplayMember = "ToString" };
        var vehBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 30 };
        var btnVehNew = new Button { Text = "Neu", Width = 75 }; btnVehNew.Click += (_, _) => EditVehicle(null); StyleButton(btnVehNew);
        var btnVehEdit = new Button { Text = "Bearbeiten", Width = 90 }; btnVehEdit.Click += (_, _) => { if (_lbVehicles.SelectedItem is Vehicle v) EditVehicle(v); }; StyleButton(btnVehEdit);
        var btnVehDel = new Button { Text = "Löschen", Width = 80 }; btnVehDel.Click += (_, _) => { if (_lbVehicles.SelectedItem is Vehicle v) DeleteVehicle(v); }; StyleButton(btnVehDel);
        vehBtns.Controls.AddRange(new Control[] { btnVehNew, btnVehEdit, btnVehDel });
        tabVeh.Controls.Add(_lbVehicles);
        tabVeh.Controls.Add(vehBtns);
        _tabControl.TabPages.Add(tabVeh);

        // ========== TAB 4: BAUSTELLEN ==========
        var tabSite = new TabPage("Baustellen");
        _lbSites = new ListBox { Dock = DockStyle.Fill, DisplayMember = "DisplayText" };
        var siteBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 30 };
        var btnSiteNew = new Button { Text = "Neu", Width = 75 }; btnSiteNew.Click += (_, _) => EditSite(null); StyleButton(btnSiteNew);
        var btnSiteEdit = new Button { Text = "Bearbeiten", Width = 90 }; btnSiteEdit.Click += (_, _) => { if (_lbSites.SelectedItem is ConstructionSite s) EditSite(s); }; StyleButton(btnSiteEdit);
        var btnSiteDel = new Button { Text = "Löschen", Width = 80 }; btnSiteDel.Click += (_, _) => { if (_lbSites.SelectedItem is ConstructionSite s) DeleteSite(s); }; StyleButton(btnSiteDel);
        siteBtns.Controls.AddRange(new Control[] { btnSiteNew, btnSiteEdit, btnSiteDel });
        tabSite.Controls.Add(_lbSites);
        tabSite.Controls.Add(siteBtns);
        _tabControl.TabPages.Add(tabSite);

        // Initial load
        RefreshAllData();
        RefreshCalendar();
        _ = CheckUpdateSilent();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _calendarPanel?.Invalidate();
    }

    // ====== REFRESH ======
    private void RefreshAllData()
    {
        _employees = _db.GetAllEmployees();
        _teams = _db.GetAllTeams();
        _vehicles = _db.GetAllVehicles();
        _sites = _db.GetAllSites();
        _vacations = _db.GetAllVacations();
        _sickness = _db.GetAllSickness();

        _lbEmployees.DataSource = null; _lbEmployees.DataSource = _employees;
        _lbTeams.DataSource = null; _lbTeams.DataSource = _teams;
        _lbVehicles.DataSource = null; _lbVehicles.DataSource = _vehicles;
        UpdateTeamDetails();
        _cmbTeamVehicle.DataSource = null; _cmbTeamVehicle.DataSource = _vehicles;
        _lbSites.DataSource = null; _lbSites.DataSource = _sites;
    }

    private void RefreshCalendar()
    {
        _lblMonthYear.Text = _currentMonth.ToString("MMMM yyyy");
        _monthAssignments = _db.GetMonthAssignments(_currentMonth);
        _calendarPanel.Invalidate();
    }

    private void UpdateTeamDetails()
    {
        if (_lbTeams.SelectedItem is Team t)
        {
            _lblTeamMembers.Text = "Mitglieder: " + t.MemberSummary;
            var assigned = _vehicles.FirstOrDefault(v => _monthAssignments.Any(a => a.TeamId == t.Id && a.VehicleId == v.Id));
            if (assigned != null)
                _lblTeamMembers.Text += $"\nFahrzeug: {assigned.VehicleNumber} ({assigned.LicensePlate})";
        }
        else _lblTeamMembers.Text = "";
    }

    // ====== CALENDAR DRAWING ======
    private void Calendar_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.White);
        var w = _calendarPanel.Width;
        var h = _calendarPanel.Height - _calendarOrigin.Y - 10;
        _calendarDayWidth = (w - 30) / 7;
        _calendarDayHeight = h / 6;
        if (_calendarDayWidth < 10) return;

        var dayNames = new[] { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };
        using var hf = new Font(Font, FontStyle.Bold);
        using var hb = new SolidBrush(SystemColors.WindowText);
        for (int i = 0; i < 7; i++)
            g.DrawString(dayNames[i], hf, hb, _calendarOrigin.X + i * _calendarDayWidth + 5, _calendarOrigin.Y - 25);

        var firstDow = (int)_currentMonth.DayOfWeek;
        var offset = firstDow == 0 ? 6 : firstDow - 1;
        var daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);
        var today = DateTime.Today;

        for (int row = 0; row < 6; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                var dayNum = row * 7 + col - offset + 1;
                var x = _calendarOrigin.X + col * _calendarDayWidth;
                var y = _calendarOrigin.Y + row * _calendarDayHeight;
                var rect = new Rectangle(x, y, _calendarDayWidth, _calendarDayHeight);
                g.DrawRectangle(Pens.LightGray, rect);
                if (dayNum < 1 || dayNum > daysInMonth) continue;

                var date = new DateTime(_currentMonth.Year, _currentMonth.Month, dayNum);
                var back = Color.White;
                if (IsInDragRange(date)) back = Color.FromArgb(200, 220, 255);
                else if (date == today) back = Color.FromArgb(220, 235, 252);
                else if (col >= 5) back = Color.FromArgb(248, 248, 248);
                using var bb = new SolidBrush(back);
                g.FillRectangle(bb, rect);

                using var df = new Font(Font.FontFamily, 8);
                using var db2 = new SolidBrush(date == today ? Color.Blue : SystemColors.WindowText);
                g.DrawString(dayNum.ToString(), df, db2, x + 3, y + 2);

                var dayAssignments = _monthAssignments.Where(a => a.Date == date).ToList();
                int ly = y + 16;
                using var sf = new Font(Font.FontFamily, 7);
                foreach (var a in dayAssignments)
                {
                    if (ly > y + _calendarDayHeight - 4) break;
                    using var sb = new SolidBrush(GetSiteColor(a.ConstructionSiteId));
                    g.FillRectangle(sb, x + 2, ly, _calendarDayWidth - 4, 14);
                    var detail = a.Site?.Name ?? "";
                    if (a.Team != null) detail += ":" + a.Team.Name;
                    else if (a.Employee != null) detail += ":" + a.Employee.FullName;
                    detail = detail.Length > 18 ? detail[..15] + ".." : detail;
                    using var tb = new SolidBrush(Color.White);
                    g.DrawString(detail, sf, tb, x + 4, ly + 1);
                    ly += 15;
                }
            }
        }

        // Drag range highlight border
        if (_dragStartDate.HasValue && _dragEndDate.HasValue)
        {
            var minD = _dragStartDate.Value < _dragEndDate.Value ? _dragStartDate.Value : _dragEndDate.Value;
            var maxD = _dragStartDate.Value > _dragEndDate.Value ? _dragStartDate.Value : _dragEndDate.Value;
            var (sx, sy) = GetCellPosition(minD);
            var (ex, ey) = GetCellPosition(maxD);
            if (sx >= 0 && ex >= 0)
            {
                var selRect = new Rectangle(sx, sy, ex - sx + _calendarDayWidth, ey - sy + _calendarDayHeight);
                using var p = new Pen(Color.Blue, 2);
                g.DrawRectangle(p, selRect);
            }
        }
    }

    private bool IsInDragRange(DateTime d)
    {
        if (!_dragStartDate.HasValue || !_dragEndDate.HasValue) return false;
        var min = _dragStartDate.Value < _dragEndDate.Value ? _dragStartDate.Value : _dragEndDate.Value;
        var max = _dragStartDate.Value > _dragEndDate.Value ? _dragStartDate.Value : _dragEndDate.Value;
        return d >= min && d <= max;
    }

    private (int x, int y) GetCellPosition(DateTime date)
    {
        if (date.Year != _currentMonth.Year || date.Month != _currentMonth.Month) return (-1, -1);
        var firstDow = (int)_currentMonth.DayOfWeek;
        var offset = firstDow == 0 ? 6 : firstDow - 1;
        var dayNum = date.Day;
        var row = (dayNum + offset - 1) / 7;
        var col = (dayNum + offset - 1) % 7;
        return (_calendarOrigin.X + col * _calendarDayWidth, _calendarOrigin.Y + row * _calendarDayHeight);
    }

    private DateTime? GetDateFromPoint(Point p)
    {
        if (_calendarDayWidth <= 0 || _calendarDayHeight <= 0) return null;
        var col = (p.X - _calendarOrigin.X) / _calendarDayWidth;
        var row = (p.Y - _calendarOrigin.Y) / _calendarDayHeight;
        if (col < 0 || col > 6 || row < 0 || row > 5) return null;
        var firstDow = (int)_currentMonth.DayOfWeek;
        var offset = firstDow == 0 ? 6 : firstDow - 1;
        var dayNum = row * 7 + col - offset + 1;
        if (dayNum < 1 || dayNum > DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month)) return null;
        return new DateTime(_currentMonth.Year, _currentMonth.Month, dayNum);
    }

    private Color GetSiteColor(int id)
    {
        var colors = new[] { Color.FromArgb(0x2E, 0x7D, 0x32), Color.FromArgb(0x1B, 0x5E, 0x20), Color.FromArgb(0x00, 0x96, 0x88), Color.FromArgb(0x00, 0x7B, 0xC0), Color.FromArgb(0xD3, 0x2F, 0x2F), Color.FromArgb(0xE6, 0x5C, 0x00), Color.FromArgb(0x6A, 0x1B, 0x9A), Color.FromArgb(0x00, 0x85, 0x3F) };
        return colors[id % colors.Length];
    }

    // ====== CALENDAR MOUSE ======
    private void Calendar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var d = GetDateFromPoint(e.Location);
            if (d.HasValue)
            {
                _isDragging = true;
                _dragStartDate = _dragEndDate = _dragCurrentDate = d;
                _dragStartPoint = e.Location;
                _calendarPanel.Invalidate();
            }
        }
    }

    private void Calendar_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            var d = GetDateFromPoint(e.Location);
            if (d.HasValue && d != _dragCurrentDate)
            {
                _dragCurrentDate = _dragEndDate = d;
                _calendarPanel.Invalidate();
            }
        }
        else
        {
            _calendarPanel.Cursor = GetDateFromPoint(e.Location).HasValue ? Cursors.Hand : Cursors.Default;
        }
    }

    private void Calendar_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_isDragging && e.Button == MouseButtons.Left)
        {
            _isDragging = false;
            var d = GetDateFromPoint(e.Location);
            if (d.HasValue)
                _dragEndDate = d;
            if (_dragStartDate.HasValue && _dragEndDate.HasValue)
                ShowDateRangePopup(_calendarPanel.PointToScreen(e.Location));
        }
    }

    private void Calendar_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && !_isDragging)
        {
            var d = GetDateFromPoint(e.Location);
            if (d.HasValue)
            {
                var yOffset = e.Y - _calendarOrigin.Y;
                var row = yOffset / _calendarDayHeight;
                var idx = (e.Y - (_calendarOrigin.Y + row * _calendarDayHeight + 16)) / 15;
                var das = _monthAssignments.Where(a => a.Date == d.Value).ToList();
                if (idx >= 0 && idx < das.Count)
                    ShowAssignmentDetail(das[idx]);
            }
        }
    }

    // ====== DATE RANGE POPUP ======
    private void ShowDateRangePopup(Point screenPos)
    {
        var d1 = _dragStartDate!.Value;
        var d2 = _dragEndDate!.Value;
        var from = d1 < d2 ? d1 : d2;
        var until = d1 > d2 ? d1 : d2;
        var rangeStr = from == until
            ? $"{from:dd.MM.yyyy}"
            : $"{from:dd.MM.yyyy} - {until:dd.MM.yyyy}";

        using var popup = new Form();
        popup.Text = $"Aktion für {rangeStr}";
        popup.StartPosition = FormStartPosition.Manual;
        popup.Location = screenPos;
        popup.Size = new Size(320, 380);
        popup.FormBorderStyle = FormBorderStyle.FixedToolWindow;
        popup.ShowInTaskbar = false;
        popup.Font = Font;

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 8 };

        tlp.Controls.Add(new Label { Text = $"Zeitraum: {rangeStr}", Font = new Font(Font, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, Height = 30 }, 0, 0);

        tlp.Controls.Add(new Label { Text = "--- Zuweisungen ---", TextAlign = ContentAlignment.MiddleCenter, Height = 20, ForeColor = SystemColors.GrayText }, 0, 1);

        var btnSite = new Button { Text = "Baustelle zuweisen", Height = 36, Dock = DockStyle.Fill };
        btnSite.Click += (_, _) => { popup.Close(); AssignSiteToRange(from, until); }; StyleButton(btnSite);
        tlp.Controls.Add(btnSite, 0, 2);

        var btnTeamAssign = new Button { Text = "Team zuweisen", Height = 36, Dock = DockStyle.Fill };
        btnTeamAssign.Click += (_, _) => { popup.Close(); AssignTeamToRange(from, until); }; StyleButton(btnTeamAssign);
        tlp.Controls.Add(btnTeamAssign, 0, 3);

        var btnEmpAssign = new Button { Text = "Mitarbeiter zuweisen", Height = 36, Dock = DockStyle.Fill };
        btnEmpAssign.Click += (_, _) => { popup.Close(); AssignEmployeeToRange(from, until); }; StyleButton(btnEmpAssign);
        tlp.Controls.Add(btnEmpAssign, 0, 4);

        tlp.Controls.Add(new Label { Text = "--- Abwesenheiten ---", TextAlign = ContentAlignment.MiddleCenter, Height = 20, ForeColor = SystemColors.GrayText }, 0, 5);

        var btnVac = new Button { Text = "Urlaub eintragen", Height = 36, Dock = DockStyle.Fill };
        btnVac.Click += (_, _) => { popup.Close(); AddVacation(from, until); }; StyleButton(btnVac);
        tlp.Controls.Add(btnVac, 0, 6);

        var btnSick = new Button { Text = "Krankheit eintragen", Height = 36, Dock = DockStyle.Fill };
        btnSick.Click += (_, _) => { popup.Close(); AddSickness(from, until); }; StyleButton(btnSick);
        tlp.Controls.Add(btnSick, 0, 7);

        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        popup.Controls.Add(tlp);
        popup.ShowDialog();
        _dragStartDate = _dragEndDate = null;
        _calendarPanel.Invalidate();
    }

    // ====== RANGE ACTIONS ======
    private void AssignSiteToRange(DateTime from, DateTime until)
    {
        var site = SelectSite();
        if (site == null) return;

        var team = SelectTeam();
        if (team != null)
        {
            for (var d = from; d <= until; d = d.AddDays(1))
            {
                if (!_db.IsTeamAssigned(team.Id, d))
                    _db.SaveAssignment(new Assignment { ConstructionSiteId = site.Id, TeamId = team.Id, Date = d });
            }
            CheckAutoVehicleAssignment(team, site.Id, from, until);
        }
        else
        {
            var emp = SelectEmployee();
            if (emp != null)
            {
                for (var d = from; d <= until; d = d.AddDays(1))
                {
                    if (!_db.IsEmployeeOnVacationOrSick(emp.Id, d) && !_db.IsEmployeeAssigned(emp.Id, d))
                        _db.SaveAssignment(new Assignment { ConstructionSiteId = site.Id, EmployeeId = emp.Id, Date = d });
                }
            }
        }
        RefreshCalendar();
    }

    private void CheckAutoVehicleAssignment(Team team, int siteId, DateTime from, DateTime until)
    {
        var canDrive = team.Members.Where(m => m.HasDriversLicense && !string.IsNullOrEmpty(m.LicenseCategories))
            .SelectMany(m => m.GetLicenseList()).Distinct().ToList();
        var compat = _vehicles.Where(v => canDrive.Contains(v.RequiredLicense)).ToList();
        if (compat.Count == 0) return;
        if (MessageBox.Show($"Team {team.Name} hat Fahrer für {string.Join(",", canDrive)}.\nFahrzeug zuweisen?", "Fahrzeug",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        using var f = new Form();
        f.Text = "Fahrzeug auswählen";
        f.Size = new Size(400, 250);
        f.StartPosition = FormStartPosition.CenterParent;
        var lb = new ListBox { Dock = DockStyle.Fill, DataSource = compat, DisplayMember = "ToString" };
        var btn = new Button { Text = "OK", Dock = DockStyle.Bottom };
        StyleButton(btn);
        Vehicle? sel = null;
        btn.Click += (_, _) => { sel = lb.SelectedItem as Vehicle; f.Close(); };
        f.Controls.Add(lb); f.Controls.Add(btn);
        f.ShowDialog();
        if (sel != null)
        {
            for (var d = from; d <= until; d = d.AddDays(1))
            {
                if (!_db.IsVehicleAssigned(sel.Id, d))
                    _db.SaveAssignment(new Assignment { ConstructionSiteId = siteId, VehicleId = sel.Id, Date = d });
            }
        }
    }

    private void AssignTeamToRange(DateTime from, DateTime until)
    {
        var team = SelectTeam();
        if (team == null) return;
        var site = SelectSite();
        if (site == null) return;

        for (var d = from; d <= until; d = d.AddDays(1))
            if (!_db.IsTeamAssigned(team.Id, d))
                _db.SaveAssignment(new Assignment { ConstructionSiteId = site.Id, TeamId = team.Id, Date = d });

        CheckAutoVehicleAssignment(team, site.Id, from, until);
        RefreshCalendar();
    }

    private void AssignEmployeeToRange(DateTime from, DateTime until)
    {
        var emp = SelectEmployee();
        if (emp == null) return;
        var site = SelectSite();
        if (site == null) return;

        for (var d = from; d <= until; d = d.AddDays(1))
        {
            if (_db.IsEmployeeOnVacationOrSick(emp.Id, d))
            {
                MessageBox.Show($"{emp.FullName} ist am {d:dd.MM.yyyy} nicht verfügbar.", "Konflikt");
                continue;
            }
            if (!_db.IsEmployeeAssigned(emp.Id, d))
                _db.SaveAssignment(new Assignment { ConstructionSiteId = site.Id, EmployeeId = emp.Id, Date = d });
        }
        RefreshCalendar();
    }

    private void AddVacation(DateTime from, DateTime until)
    {
        var emp = SelectEmployee();
        if (emp == null) return;
        _db.SaveVacation(new Vacation { EmployeeId = emp.Id, StartDate = from, EndDate = until });
        MessageBox.Show($"Urlaub für {emp.FullName} von {from:dd.MM.yyyy} bis {until:dd.MM.yyyy} eingetragen.");
        _vacations = _db.GetAllVacations();
        CheckVacSickConflicts();
    }

    private void AddSickness(DateTime from, DateTime until)
    {
        var emp = SelectEmployee();
        if (emp == null) return;
        _db.SaveSickness(new Sickness { EmployeeId = emp.Id, StartDate = from, EndDate = until });
        MessageBox.Show($"Krankheit für {emp.FullName} von {from:dd.MM.yyyy} bis {until:dd.MM.yyyy} eingetragen.");
        _sickness = _db.GetAllSickness();
        CheckVacSickConflicts();
    }

    // ====== SELECTION HELPERS ======
    private ConstructionSite? SelectSite()
    {
        if (_sites.Count == 0) { MessageBox.Show("Keine Baustellen vorhanden.", ""); return null; }
        using var f = new Form();
        f.Text = "Baustelle auswählen"; f.Size = new Size(400, 300);
        f.StartPosition = FormStartPosition.CenterParent;
        var lb = new ListBox { Dock = DockStyle.Fill, DataSource = _sites, DisplayMember = "DisplayText" };
        var btn = new Button { Text = "OK", Dock = DockStyle.Bottom };
        StyleButton(btn);
        ConstructionSite? r = null;
        btn.Click += (_, _) => { r = lb.SelectedItem as ConstructionSite; f.Close(); };
        f.Controls.Add(lb); f.Controls.Add(btn);
        f.ShowDialog(); return r;
    }

    private Team? SelectTeam()
    {
        if (_teams.Count == 0) { MessageBox.Show("Keine Teams vorhanden.", ""); return null; }
        using var f = new Form();
        f.Text = "Team auswählen"; f.Size = new Size(400, 300);
        f.StartPosition = FormStartPosition.CenterParent;
        var lb = new ListBox { Dock = DockStyle.Fill, DataSource = _teams, DisplayMember = "Name" };
        var btn = new Button { Text = "OK", Dock = DockStyle.Bottom };
        StyleButton(btn);
        Team? r = null;
        btn.Click += (_, _) => { r = lb.SelectedItem as Team; f.Close(); };
        f.Controls.Add(lb); f.Controls.Add(btn);
        f.ShowDialog(); return r;
    }

    private Employee? SelectEmployee()
    {
        if (_employees.Count == 0) { MessageBox.Show("Keine Mitarbeiter vorhanden.", ""); return null; }
        using var f = new Form();
        f.Text = "Mitarbeiter auswählen"; f.Size = new Size(400, 300);
        f.StartPosition = FormStartPosition.CenterParent;
        var lb = new ListBox { Dock = DockStyle.Fill, DataSource = _employees, DisplayMember = "FullName" };
        var btn = new Button { Text = "OK", Dock = DockStyle.Bottom };
        StyleButton(btn);
        Employee? r = null;
        btn.Click += (_, _) => { r = lb.SelectedItem as Employee; f.Close(); };
        f.Controls.Add(lb); f.Controls.Add(btn);
        f.ShowDialog(); return r;
    }

    // ====== ASSIGNMENT DETAIL ======
    private void ShowAssignmentDetail(Assignment a)
    {
        var msg = $"Baustelle: {a.Site?.Name}\nDatum: {a.Date:dd.MM.yyyy}\n";
        if (a.Team != null) msg += $"Team: {a.Team.Name}\n";
        if (a.Employee != null) msg += $"Mitarbeiter: {a.Employee.FullName}\n";
        if (a.Vehicle != null) msg += $"Fahrzeug: {a.Vehicle}\n";
        if (MessageBox.Show(msg + "\nLöschen?", "Zuweisung", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
        { _db.DeleteAssignment(a.Id); RefreshCalendar(); }
    }

    // ====== CRUD ======
    private void EditEmployee(Employee? e)
    {
        using var f = new EmployeeForm(_db, e); if (f.ShowDialog() == DialogResult.OK) RefreshAllData();
    }

    private void DeleteEmployee(Employee e)
    {
        if (MessageBox.Show($"{e.FullName} wirklich löschen?", "Löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        { _db.DeleteEmployee(e.Id); RefreshAllData(); }
    }

    private void EditTeam(Team? t)
    {
        using var f = new TeamForm(_db, t, _employees); if (f.ShowDialog() == DialogResult.OK) RefreshAllData();
    }

    private void DeleteTeam(Team t)
    {
        if (MessageBox.Show($"Team {t.Name} wirklich löschen?", "Löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        { _db.DeleteTeam(t.Id); RefreshAllData(); }
    }

    private void EditVehicle(Vehicle? v)
    {
        using var f = new VehicleForm(_db, v); if (f.ShowDialog() == DialogResult.OK) RefreshAllData();
    }

    private void DeleteVehicle(Vehicle v)
    {
        if (MessageBox.Show($"Fahrzeug {v} wirklich löschen?", "Löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        { _db.DeleteVehicle(v.Id); RefreshAllData(); }
    }

    private void AssignVehicleToTeam()
    {
        if (_lbTeams.SelectedItem is not Team team) { MessageBox.Show("Bitte zuerst Team auswählen."); return; }
        if (_cmbTeamVehicle.SelectedItem is not Vehicle veh) { MessageBox.Show("Bitte Fahrzeug auswählen."); return; }
        var canDrive = team.Members.Any(m => m.HasDriversLicense && m.GetLicenseList().Contains(veh.RequiredLicense));
        if (!canDrive)
        {
            if (MessageBox.Show($"Kein Teammitglied kann {veh.RequiredLicense} fahren.\nTrotzdem zuweisen?", "Warnung",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        }
        MessageBox.Show($"Fahrzeug {veh.VehicleNumber} dem Team {team.Name} zugewiesen.\n(Bitte im Kalender über Baustelle zuweisen buchen)");
    }

    // ====== VAC/SICK CONFLICT CHECK ======
    private void CheckVacSickConflicts()
    {
        foreach (var team in _teams)
        {
            var removed = new List<Employee>();
            foreach (var m in team.Members.ToList())
            {
                for (var d = DateTime.Today.AddDays(-30); d <= DateTime.Today.AddDays(30); d = d.AddDays(1))
                {
                    if (_db.IsEmployeeOnVacationOrSick(m.Id, d))
                    {
                        removed.Add(m);
                        break;
                    }
                }
            }
            if (removed.Count > 0)
            {
                foreach (var r in removed) team.Members.Remove(r);
                _db.SaveTeam(team);
                MessageBox.Show($"{string.Join(", ", removed.Select(x => x.FullName))} aus Team {team.Name} entfernt (Urlaub/Krankheit).", "Auto-Entfernung");
            }
        }
    }

    // ====== BACKUP ======
    private async Task DoBackup()
    {
        if (await _backup.BackupToDrive())
            MessageBox.Show("Google Drive Backup erfolgreich!", "Backup");
        else
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kofplanner.db");
            var bp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"kofplanner_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            try { File.Copy(dbPath, bp); MessageBox.Show($"Lokales Backup: {bp}", "Backup"); }
            catch (Exception ex) { MessageBox.Show($"Fehler: {ex.Message}"); }
        }
    }

    private void ConfigureBackup()
    {
        using var f = new Form();
        f.Text = "Google Drive Backup"; f.Size = new Size(500, 220);
        f.StartPosition = FormStartPosition.CenterParent;
        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10) };
        tlp.Controls.Add(new Label { Text = "Client ID:", Anchor = AnchorStyles.Left }, 0, 0);
        var tid = new TextBox { Dock = DockStyle.Fill }; tlp.Controls.Add(tid, 1, 0);
        tlp.Controls.Add(new Label { Text = "Client Secret:", Anchor = AnchorStyles.Left }, 0, 1);
        var ts = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true }; tlp.Controls.Add(ts, 1, 1);
        tlp.Controls.Add(new Label { Text = "Folder ID:", Anchor = AnchorStyles.Left }, 0, 2);
        var tf = new TextBox { Dock = DockStyle.Fill }; tlp.Controls.Add(tf, 1, 2);
        var btn = new Button { Text = "Speichern", Dock = DockStyle.Bottom, Height = 30 };
        btn.Click += (_, _) => { _backup.ConfigureDriveBackup(tid.Text, ts.Text, tf.Text); MessageBox.Show("Gespeichert!"); f.Close(); }; StyleButton(btn);
        tlp.Controls.Add(btn, 0, 3); tlp.SetColumnSpan(btn, 2);
        f.Controls.Add(tlp); f.ShowDialog();
    }

    // ====== UPDATE ======
    private async Task CheckUpdate()
    {
        var v = await _update.CheckForUpdate();
        if (v == null) MessageBox.Show("Kein Update verfügbar.\nVersion ist aktuell.", "Update");
        else if (await _update.DownloadAndInstall(v)) Application.Exit();
    }

    private async Task CheckUpdateSilent()
    {
        try
        {
            var v = await _update.CheckForUpdate();
            if (v != null && MessageBox.Show($"Neue Version {v} verfügbar.\nJetzt installieren?", "Update",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                if (await _update.DownloadAndInstall(v)) Application.Exit();
        }
        catch { }
    }

    // ====== SITE CRUD ======
    private void EditSite(ConstructionSite? site)
    {
        using var f = new SiteForm(_db, site);
        if (f.ShowDialog(this) == DialogResult.OK)
            RefreshAllData();
    }

    private void DeleteSite(ConstructionSite site)
    {
        if (MessageBox.Show($"Baustelle \"{site.Name}\" wirklich löschen?", "Löschen",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _db.DeleteSite(site.Id);
        RefreshAllData();
    }
}
