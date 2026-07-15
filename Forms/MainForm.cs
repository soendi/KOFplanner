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

        // Tab 2 controls (Employees, Teams, Vehicles)
        private readonly ListBox _lbEmployees;
        private readonly FlowLayoutPanel _flowTeamCards;
        private readonly Panel _pnlNewTeamDropZone;
        private readonly ListBox _lbVehicles;

    // Tab 4 controls (Sites)
    private readonly ListBox _lbSites;

    // Calendar drag selection
    private DateTime? _dragStartDate, _dragEndDate, _dragCurrentDate;
    private bool _isDragging;
    private bool _dragIsAssignment;
    private bool _suppressClick;
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
        btn.TextAlign = ContentAlignment.MiddleCenter;
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
        hilfeMenu.DropDownItems.Add("Info", null, (_, _) => MessageBox.Show($"KOFplanner v{_update.CurrentVersion}\nAuthor: Lukas Sonderegger", "Info"));
        MainMenuStrip = menu;
        Controls.Add(menu);

        // Main TabControl
        _tabControl = new TabControl { Dock = DockStyle.Fill, ItemSize = new Size(90, 40) };
        _tabControl.Padding = new Point(0, 0);
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
        _calendarPanel.MouseDoubleClick += Calendar_MouseDoubleClick;

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

        // ========== TAB 2: MITARBEITER & TEAMS (3 Spalten) ==========
        var tabMA = new TabPage("Mitarbeiter && Teams");
        var t2Grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(6) };
        t2Grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        t2Grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        t2Grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        t2Grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // ---- Spalte 1: Mitarbeiter ----
        var colEmp = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 4, 0) };
        var empHeader = new Panel { Dock = DockStyle.Top, Height = 40 };
        empHeader.Controls.Add(new Label { Text = "Mitarbeiter", Dock = DockStyle.Left, AutoSize = true, Font = new Font(Font.FontFamily, 11, FontStyle.Bold), Padding = new Padding(0, 6, 0, 0) });
        var btnEmpNew = new Button { Text = "NEU", Width = 90, Height = 28, Dock = DockStyle.Right }; btnEmpNew.Click += (_, _) => EditEmployee(null); StyleButton(btnEmpNew);
        empHeader.Controls.Add(btnEmpNew);
        _lbEmployees = new ListBox { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 40 };
        _lbEmployees.DrawItem += EmployeeList_DrawItem;
        _lbEmployees.MouseDown += EmployeeList_MouseDown;
        colEmp.Controls.Add(_lbEmployees);
        colEmp.Controls.Add(empHeader);
        t2Grid.Controls.Add(colEmp, 0, 0);

        // ---- Spalte 2: Teams ----
        var colTeam = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 0, 4, 0) };
        var teamHeader = new Panel { Dock = DockStyle.Top, Height = 40 };
        teamHeader.Controls.Add(new Label { Text = "Teams", Dock = DockStyle.Left, AutoSize = true, Font = new Font(Font.FontFamily, 11, FontStyle.Bold), Padding = new Padding(0, 6, 0, 0) });
        colTeam.Controls.Add(teamHeader);
        _flowTeamCards = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(2, 2, 2, 4) };
        _flowTeamCards.Resize += (_, _) => LayoutTeamCards();
        _pnlNewTeamDropZone = new Panel
        {
            Height = 76, BackColor = Color.FromArgb(0xE8, 0xF5, 0xE9),
            BorderStyle = BorderStyle.FixedSingle, AllowDrop = true, Margin = new Padding(0, 0, 0, 12),
            Cursor = Cursors.Hand
        };
        _pnlNewTeamDropZone.Paint += (s, e) =>
        {
            using var f = new Font("Segoe UI", 11, FontStyle.Bold);
            using var b = new SolidBrush(Color.FromArgb(0x2E, 0x7D, 0x32));
            var txt = "+ Neues Team  (Mitarbeiter hierher ziehen)";
            var sz = e.Graphics.MeasureString(txt, f);
            e.Graphics.DrawString(txt, f, b, (_pnlNewTeamDropZone.Width - sz.Width) / 2, (_pnlNewTeamDropZone.Height - sz.Height) / 2);
        };
        _pnlNewTeamDropZone.DragEnter += (s, e) => { if (e.Data!.GetDataPresent(typeof(Employee))) { e.Effect = DragDropEffects.Move; _pnlNewTeamDropZone.BackColor = Color.FromArgb(0xD8, 0xF0, 0xDC); } };
        _pnlNewTeamDropZone.DragLeave += (_, _) => _pnlNewTeamDropZone.BackColor = Color.FromArgb(0xE8, 0xF5, 0xE9);
        _pnlNewTeamDropZone.DragDrop += (s, e) =>
        {
            _pnlNewTeamDropZone.BackColor = Color.FromArgb(0xE8, 0xF5, 0xE9);
            if (e.Data!.GetData(typeof(Employee)) is Employee emp)
                CreateTeamFromEmployee(emp);
        };
        _flowTeamCards.Controls.Add(_pnlNewTeamDropZone);
        colTeam.Controls.Add(_flowTeamCards);
        t2Grid.Controls.Add(colTeam, 1, 0);

        // ---- Spalte 3: Fahrzeuge ----
        var colVeh = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 0, 0, 0) };
        var vehHeader = new Panel { Dock = DockStyle.Top, Height = 40 };
        vehHeader.Controls.Add(new Label { Text = "Fahrzeuge", Dock = DockStyle.Left, AutoSize = true, Font = new Font(Font.FontFamily, 11, FontStyle.Bold), Padding = new Padding(0, 6, 0, 0) });
        var btnVehNew = new Button { Text = "NEU", Width = 90, Height = 28, Dock = DockStyle.Right }; btnVehNew.Click += (_, _) => EditVehicle(null); StyleButton(btnVehNew);
        vehHeader.Controls.Add(btnVehNew);
        var vehBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(2) };
        var btnVehEdit = new Button { Text = "Bearbeiten", Width = 100, Height = 28 }; btnVehEdit.Click += (_, _) => { if (_lbVehicles.SelectedItem is Vehicle v) EditVehicle(v); }; StyleButton(btnVehEdit);
        var btnVehDel = new Button { Text = "Löschen", Width = 85, Height = 28 }; btnVehDel.Click += (_, _) => { if (_lbVehicles.SelectedItem is Vehicle v) DeleteVehicle(v); }; StyleButton(btnVehDel);
        vehBtns.Controls.AddRange(new Control[] { btnVehEdit, btnVehDel });
        _lbVehicles = new ListBox { Dock = DockStyle.Fill, DisplayMember = "ToString" };
        _lbVehicles.MouseDown += VehicleList_MouseDown;
        colVeh.Controls.Add(_lbVehicles);
        colVeh.Controls.Add(vehBtns);
        colVeh.Controls.Add(vehHeader);
        t2Grid.Controls.Add(colVeh, 2, 0);

        tabMA.Controls.Add(t2Grid);
        _tabControl.TabPages.Add(tabMA);

        // ========== TAB 4: BAUSTELLEN ==========
        var tabSite = new TabPage("Baustellen");
        _lbSites = new ListBox { Dock = DockStyle.Fill, DisplayMember = "DisplayText" };
        var siteBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 30 };
        var btnSiteNew = new Button { Text = "Neu", Width = 75 }; btnSiteNew.Click += (_, _) => EditSite(null); StyleButton(btnSiteNew);
        var btnSiteEdit = new Button { Text = "Bearbeiten", Width = 100, Height = 28 }; btnSiteEdit.Click += (_, _) => { if (_lbSites.SelectedItem is ConstructionSite s) EditSite(s); }; StyleButton(btnSiteEdit);
        var btnSiteDel = new Button { Text = "Löschen", Width = 85, Height = 28 }; btnSiteDel.Click += (_, _) => { if (_lbSites.SelectedItem is ConstructionSite s) DeleteSite(s); }; StyleButton(btnSiteDel);
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

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        LayoutTeamCards();
    }

    // ====== REFRESH ======
    private void RefreshAllData()
    {
        _employees = _db.GetAllEmployees().OrderBy(e => e.FullName, StringComparer.CurrentCultureIgnoreCase).ToList();
        _teams = _db.GetAllTeams();
        _vehicles = _db.GetAllVehicles().OrderBy(v => v.VehicleNumber, StringComparer.CurrentCultureIgnoreCase).ToList();
        _sites = _db.GetAllSites();
        _vacations = _db.GetAllVacations();
        _sickness = _db.GetAllSickness();

        _lbEmployees.DataSource = null; _lbEmployees.DataSource = _employees;
        _lbVehicles.DataSource = null; _lbVehicles.DataSource = _vehicles;
        RefreshTeamView();
        _lbSites.DataSource = null; _lbSites.DataSource = _sites;
    }

    private void RefreshTeamView()
    {
        while (_flowTeamCards.Controls.Count > 1)
            _flowTeamCards.Controls[1].Dispose();
        foreach (var t in _teams)
            _flowTeamCards.Controls.Add(CreateTeamCard(t));
        LayoutTeamCards();
    }

    private void LayoutTeamCards()
    {
        if (_flowTeamCards.ClientSize.Width <= 0) return;
        var w = _flowTeamCards.ClientSize.Width - _flowTeamCards.Padding.Horizontal;
        _pnlNewTeamDropZone.Width = w;
        foreach (Control c in _flowTeamCards.Controls)
        {
            if (c is not Panel card || card == _pnlNewTeamDropZone) continue;
            card.Width = w;
            foreach (Label l in card.Controls.OfType<Label>())
            {
                l.Left = 12;
                l.Width = (l.Top == 8) ? card.Width - 110 : card.Width - 24;
            }
            var editBtn = card.Controls.OfType<Button>().FirstOrDefault(b => b.Text == "Bearb.");
            var delBtn = card.Controls.OfType<Button>().FirstOrDefault(b => b.Text == "X");
            if (editBtn != null) { editBtn.Left = card.Width - 98; editBtn.Top = 6; }
            if (delBtn != null) { delBtn.Left = card.Width - 48; delBtn.Top = 6; }
        }
    }

    private void VehicleList_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_lbVehicles.SelectedItem is Vehicle veh && e.Button == MouseButtons.Left)
            _lbVehicles.DoDragDrop(veh, DragDropEffects.Move);
    }

    private static Color GetContrastText(Color bg)
    {
        var lum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
        return lum > 0.6 ? Color.Black : Color.White;
    }

    private void EmployeeList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox lb) return;
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= lb.Items.Count) return;
        if (lb.Items[e.Index] is not Employee emp) return;
        var name = emp.FullName;
        var lic = emp.GetLicenseList().Length > 0
            ? "FS: " + string.Join(", ", emp.GetLicenseList())
            : "kein Führerschein";
        using var nameFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var licFont = new Font("Segoe UI", 8f);
        using var nameBrush = new SolidBrush(e.ForeColor);
        using var licBrush = new SolidBrush(Color.FromArgb(0x66, 0x66, 0x66));
        e.Graphics.DrawString(name, nameFont, nameBrush, e.Bounds.Left + 5, e.Bounds.Top + 4);
        e.Graphics.DrawString(lic, licFont, licBrush, e.Bounds.Left + 5, e.Bounds.Top + 22);
        e.DrawFocusRectangle();
    }

    private void RefreshCalendar()
    {
        _lblMonthYear.Text = _currentMonth.ToString("MMMM yyyy");
        _monthAssignments = _db.GetMonthAssignments(_currentMonth);
        _calendarPanel.Invalidate();
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
                if (dayNum < 1 || dayNum > daysInMonth)
                {
                    using var p = new Pen(Color.FromArgb(0xDD, 0xDD, 0xDD));
                    g.DrawRectangle(p, rect);
                    continue;
                }

                var date = new DateTime(_currentMonth.Year, _currentMonth.Month, dayNum);
                var back = Color.White;
                if (IsInDragRange(date)) back = Color.FromArgb(200, 220, 255);
                else if (date == today) back = Color.FromArgb(220, 235, 252);
                else if (col >= 5) back = Color.FromArgb(248, 248, 248);
                using var bb = new SolidBrush(back);
                g.FillRectangle(bb, rect);
                using var p2 = new Pen(Color.FromArgb(0xBB, 0xBB, 0xBB));
                g.DrawRectangle(p2, rect);

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
                _suppressClick = false;
                _dragIsAssignment = (ModifierKeys & Keys.Control) != 0;
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
                _suppressClick = true;
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
            if (_dragIsAssignment && _dragStartDate.HasValue && _dragEndDate.HasValue)
            {
                _suppressClick = true;
                ShowDateRangePopup(_calendarPanel.PointToScreen(e.Location));
            }
        }
    }

    private void Calendar_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && !_isDragging && !_suppressClick)
        {
            var d = GetDateFromPoint(e.Location);
            if (d.HasValue)
            {
                _dragStartDate = _dragEndDate = d;
                _calendarPanel.Invalidate();
            }
        }
    }

    private void Calendar_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var d = GetDateFromPoint(e.Location);
        if (d.HasValue) ShowDayOverview(d.Value);
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
        popup.Size = new Size(340, 360);
        popup.FormBorderStyle = FormBorderStyle.FixedToolWindow;
        popup.ShowInTaskbar = false;
        popup.Font = Font;

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(15, 12, 15, 12), WrapContents = false };

        flow.Controls.Add(new Label { Text = $"Zeitraum: {rangeStr}", Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 8) });
        flow.Controls.Add(new Label { Text = "Zuweisungen", ForeColor = SystemColors.GrayText, AutoSize = true, Margin = new Padding(0, 4, 0, 4) });

        var btnSite = new Button { Text = "Baustelle zuweisen", Width = 290, Height = 38 };
        btnSite.Click += (_, _) => { popup.Close(); AssignSiteToRange(from, until); }; StyleButton(btnSite);
        flow.Controls.Add(btnSite);

        var btnTeamAssign = new Button { Text = "Team zuweisen", Width = 290, Height = 38 };
        btnTeamAssign.Click += (_, _) => { popup.Close(); AssignTeamToRange(from, until); }; StyleButton(btnTeamAssign);
        flow.Controls.Add(btnTeamAssign);

        var btnEmpAssign = new Button { Text = "Mitarbeiter zuweisen", Width = 290, Height = 38 };
        btnEmpAssign.Click += (_, _) => { popup.Close(); AssignEmployeeToRange(from, until); }; StyleButton(btnEmpAssign);
        flow.Controls.Add(btnEmpAssign);

        flow.Controls.Add(new Label { Text = "Abwesenheiten", ForeColor = SystemColors.GrayText, AutoSize = true, Margin = new Padding(0, 8, 0, 4) });

        var btnVac = new Button { Text = "Urlaub eintragen", Width = 290, Height = 38 };
        btnVac.Click += (_, _) => { popup.Close(); AddVacation(from, until); }; StyleButton(btnVac);
        flow.Controls.Add(btnVac);

        var btnSick = new Button { Text = "Krankheit eintragen", Width = 290, Height = 38 };
        btnSick.Click += (_, _) => { popup.Close(); AddSickness(from, until); }; StyleButton(btnSick);
        flow.Controls.Add(btnSick);

        popup.Controls.Add(flow);
        popup.ShowDialog();
        _dragStartDate = _dragEndDate = null;
        _calendarPanel.Invalidate();
    }

    // ====== DAY OVERVIEW ======
    private void ShowDayOverview(DateTime day)
    {
        var das = _monthAssignments.Where(a => a.Date == day).ToList();
        var sites = das.Select(a => a.Site).OfType<ConstructionSite>().DistinctBy(s => s.Id).OrderBy(s => s.Name).ToList();
        var teams = das.Select(a => a.Team).OfType<Team>().DistinctBy(t => t.Id).ToList();
        var employees = das.Select(a => a.Employee).OfType<Employee>()
            .Concat(teams.SelectMany(t => t.Members))
            .DistinctBy(e => e.Id).OrderBy(e => e.FullName).ToList();
        var vehicles = das.Select(a => a.Vehicle).OfType<Vehicle>()
            .Concat(teams.Where(t => t.PreferredVehicleId.HasValue)
                        .Select(t => _vehicles.FirstOrDefault(v => v.Id == t.PreferredVehicleId!.Value))
                        .OfType<Vehicle>())
            .DistinctBy(v => v.Id).OrderBy(v => v.VehicleNumber).ToList();

        using var f = new Form();
        f.Text = $"Übersicht {day:dd.MM.yyyy}";
        f.Size = new Size(420, 540);
        f.MinimumSize = new Size(360, 300);
        f.StartPosition = FormStartPosition.CenterParent;
        f.Font = Font;

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(16) };

        void AddSection(string title, IEnumerable<string> items)
        {
            flow.Controls.Add(new Label { Text = title, Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 12, 0, 4) });
            var list = items.ToList();
            if (list.Count == 0)
                flow.Controls.Add(new Label { Text = "– keine", ForeColor = SystemColors.GrayText, AutoSize = true, Margin = new Padding(0, 0, 0, 8) });
            else
                foreach (var it in list)
                    flow.Controls.Add(new Label { Text = "• " + it, AutoSize = true, Margin = new Padding(2, 0, 0, 2) });
        }

        AddSection($"Baustellen ({sites.Count})", sites.Select(s => s.Name));
        AddSection($"Teams ({teams.Count})", teams.Select(t => t.Name));
        AddSection($"Mitarbeiter ({employees.Count})", employees.Select(e => e.FullName));
        AddSection($"Fahrzeuge ({vehicles.Count})", vehicles.Select(v => v.VehicleNumber));

        f.Controls.Add(flow);
        f.ShowDialog();
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

    private void AssignVehicleToTeam(Team team, Vehicle veh)
    {
        var canDrive = team.Members.Any(m => m.HasDriversLicense && m.GetLicenseList().Contains(veh.RequiredLicense));
        if (!canDrive)
        {
            if (MessageBox.Show($"Kein Teammitglied kann {veh.RequiredLicense} fahren.\nTrotzdem zuweisen?", "Warnung",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        }
        team.PreferredVehicleId = veh.Id;
        _db.SaveTeam(team);
        MessageBox.Show($"Fahrzeug {veh.VehicleNumber} dem Team {team.Name} zugewiesen.");
        RefreshTeamView();
    }

    // ====== TAB 2 DRAG & DROP ======
    private void EmployeeList_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_lbEmployees.SelectedItem is Employee emp && e.Button == MouseButtons.Left)
            _lbEmployees.DoDragDrop(emp, DragDropEffects.Move);
    }

    private void CreateTeamFromEmployee(Employee emp)
    {
        var palette = new[]
        {
            Color.FromArgb(0x2E, 0x7D, 0x32), Color.FromArgb(0x1B, 0x5E, 0x20), Color.FromArgb(0x00, 0x96, 0x88),
            Color.FromArgb(0x00, 0x7B, 0xC0), Color.FromArgb(0xD3, 0x2F, 0x2F), Color.FromArgb(0xE6, 0x5C, 0x00),
            Color.FromArgb(0x6A, 0x1B, 0x9A), Color.FromArgb(0x00, 0x85, 0x3F), Color.FromArgb(0x55, 0x55, 0x55)
        };
        var selectedColor = palette[0];
        using var f = new Form();
        f.Text = "Neues Team";
        f.Size = new Size(360, 240);
        f.StartPosition = FormStartPosition.CenterParent;
        f.FormBorderStyle = FormBorderStyle.FixedDialog;
        f.MaximizeBox = false;
        f.MinimizeBox = false;
        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12), RowCount = 4 };
        tlp.Controls.Add(new Label { Text = "Teamname:", Anchor = AnchorStyles.Left }, 0, 0);
        var txtName = new TextBox { Dock = DockStyle.Fill, Text = $"{emp.LastName}-Team" };
        tlp.Controls.Add(txtName, 1, 0);
        tlp.Controls.Add(new Label { Text = "Farbe:", Anchor = AnchorStyles.Left }, 0, 1);
        var colorPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 40, Padding = new Padding(0) };
        Button? selBtn = null;
        foreach (var col in palette)
        {
            var sw = new Button { Width = 30, Height = 30, BackColor = col, FlatStyle = FlatStyle.Flat, Margin = new Padding(2) };
            sw.Click += (_, _) =>
            {
                selectedColor = col;
                if (selBtn != null) selBtn.FlatAppearance.BorderSize = 0;
                sw.FlatAppearance.BorderSize = 3;
                sw.FlatAppearance.BorderColor = Color.Black;
                selBtn = sw;
            };
            if (col == selectedColor) { sw.FlatAppearance.BorderSize = 3; sw.FlatAppearance.BorderColor = Color.Black; selBtn = sw; }
            colorPanel.Controls.Add(sw);
        }
        tlp.Controls.Add(colorPanel, 1, 1);
        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 38 };
        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
        StyleButton(btnOk);
        var btnCancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Width = 80 };
        btnPanel.Controls.Add(btnOk); btnPanel.Controls.Add(btnCancel);
        tlp.Controls.Add(btnPanel, 0, 3); tlp.SetColumnSpan(btnPanel, 2);
        f.Controls.Add(tlp);
        if (f.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(txtName.Text)) return;

        var team = new Team { Name = txtName.Text.Trim(), ColorArgb = selectedColor.ToArgb() };
        team.Members.Add(emp);
        _db.SaveTeam(team);
        RefreshAllData();
    }

    private Panel CreateTeamCard(Team team)
    {
        var prefVeh = team.PreferredVehicleId.HasValue ? _vehicles.FirstOrDefault(v => v.Id == team.PreferredVehicleId.Value) : null;
        var bg = team.Color;
        var fg = GetContrastText(bg);
        var card = new Panel
        {
            Height = 104,
            BackColor = bg,
            BorderStyle = BorderStyle.FixedSingle,
            AllowDrop = true,
            Margin = new Padding(0, 0, 0, 8),
            Tag = team
        };

        var lblName = new Label { Text = team.Name, AutoSize = false, Height = 22, Left = 12, Top = 8, Width = card.Width - 110, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = fg };
        card.Controls.Add(lblName);

        var lblMembers = new Label { Text = team.MemberSummary, AutoSize = false, Height = 38, Left = 12, Top = 32, Width = card.Width - 24, Font = new Font("Segoe UI", 8), ForeColor = fg };
        card.Controls.Add(lblMembers);

        var lblVeh = new Label { Text = prefVeh != null ? $"Fahrzeug: {prefVeh.VehicleNumber}" : "kein Fahrzeug", AutoSize = false, Height = 16, Left = 12, Top = 72, Width = card.Width - 24, Font = new Font("Segoe UI", 8, FontStyle.Italic), ForeColor = fg };
        card.Controls.Add(lblVeh);

        var btnEdit = new Button { Text = "Bearb.", Width = 46, Height = 28, Left = card.Width - 98, Top = 6, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, BackColor = Color.White, ForeColor = Color.FromArgb(0x1B, 0x5E, 0x20), TextAlign = ContentAlignment.MiddleCenter };
        btnEdit.FlatStyle = FlatStyle.Flat;
        btnEdit.Click += (_, _) => { EditTeam(team); };
        card.Controls.Add(btnEdit);

        var btnDel = new Button { Text = "X", Width = 46, Height = 28, Left = card.Width - 48, Top = 6, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, BackColor = Color.White, ForeColor = Color.Red, TextAlign = ContentAlignment.MiddleCenter };
        btnDel.FlatStyle = FlatStyle.Flat;
        btnDel.Click += (_, _) => { DeleteTeam(team); };
        card.Controls.Add(btnDel);

        // Accept employee / vehicle drops
        card.DragEnter += (s, e) => { if (e.Data!.GetDataPresent(typeof(Employee)) || e.Data!.GetDataPresent(typeof(Vehicle))) e.Effect = DragDropEffects.Move; };
        card.DragDrop += (s, e) =>
        {
            if (e.Data!.GetData(typeof(Employee)) is Employee emp)
            {
                if (!team.Members.Any(m => m.Id == emp.Id))
                {
                    team.Members.Add(emp);
                    _db.SaveTeam(team);
                    RefreshAllData();
                }
            }
            else if (e.Data!.GetData(typeof(Vehicle)) is Vehicle veh)
            {
                AssignVehicleToTeam(team, veh);
            }
        };

        card.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Right)
                ShowTeamContextMenu(team, card.PointToScreen(e.Location));
        };

        return card;
    }

    private void ShowTeamContextMenu(Team team, Point screenPos)
    {
        var cm = new ContextMenuStrip();
        foreach (var m in team.Members.ToList())
        {
            var item = cm.Items.Add($"Entfernen: {m.FullName}");
            item.Click += (_, _) =>
            {
                team.Members.Remove(m);
                _db.SaveTeam(team);
                RefreshAllData();
            };
        }
        if (team.Members.Count > 0)
            cm.Items.Add(new ToolStripSeparator());
        var clearVeh = cm.Items.Add("Fahrzeugzuweisung entfernen");
        clearVeh.Click += (_, _) =>
        {
            team.PreferredVehicleId = null;
            _db.SaveTeam(team);
            RefreshTeamView();
        };
        cm.Show(screenPos);
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
    private static bool ConfirmUpdate()
    {
        return MessageBox.Show("Neue Version gefunden.\n\nMöchten Sie das Update automatisch installieren lassen?",
            "Update verfügbar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }

    private async Task CheckUpdate()
    {
        var v = await _update.CheckForUpdate();
        if (v == null) MessageBox.Show("Kein Update verfügbar.\nVersion ist aktuell.", "Update");
        else if (ConfirmUpdate() && await _update.DownloadAndInstall(v)) Application.Exit();
    }

    private async Task CheckUpdateSilent()
    {
        try
        {
            var v = await _update.CheckForUpdate();
            if (v != null && ConfirmUpdate() && await _update.DownloadAndInstall(v)) Application.Exit();
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

