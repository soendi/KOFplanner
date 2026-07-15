using KOFplanner.Models;
using KOFplanner.Services;
using System.Drawing.Drawing2D;

namespace KOFplanner.Forms;

public class MainForm : Form
{
    private readonly DatabaseService _db;
    private readonly BackupService _backup;
    private readonly UpdateService _update;
    private readonly SettingsService _settings;
    private DateTime _currentMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1);
    private bool _weekView;
    private DateTime? _hoverDate;
    private int _calendarDayWidth, _calendarDayHeight;
    private readonly Point _calendarOrigin = new(15, 45);
    private List<Assignment> _monthAssignments = new();
    private List<Vacation> _vacations = new();
    private List<Sickness> _sickness = new();
    private List<Employee> _employees = new();
    private List<Team> _teams = new();
    private List<Vehicle> _vehicles = new();
    private List<ConstructionSite> _sites = new();

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
    private Team? _selectedTeam;

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

    public MainForm(DatabaseService db, BackupService backup, UpdateService update, SettingsService settings)
    {
        _settings = settings;
        _db = db;
        _backup = backup;
        _update = update;
        Text = "KOFplanner - Baustelleneinsatzplanung v" + _update.CurrentVersion;
        Size = new Size(1400, 850);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        var menu = new MenuStrip { Dock = DockStyle.Top, AutoSize = false, Height = 24, Padding = new Padding(0) };
        var dateiMenu = menu.Items.Add("&Datei") as ToolStripMenuItem;
        dateiMenu!.DropDownItems.Add("Datenbank sichern...", null, async (_, _) => await DoBackup());
        dateiMenu.DropDownItems.Add("Google Drive Backup...", null, (_, _) => ConfigureBackup());
        dateiMenu.DropDownItems.Add(new ToolStripSeparator());
        dateiMenu.DropDownItems.Add("Einstellungen...", null, (_, _) => OpenSettings());
        dateiMenu.DropDownItems.Add(new ToolStripSeparator());
        dateiMenu.DropDownItems.Add("Beenden", null, (_, _) => Close());
        var hilfeMenu = menu.Items.Add("&Hilfe") as ToolStripMenuItem;
        hilfeMenu!.DropDownItems.Add("Nach Updates suchen...", null, async (_, _) => await CheckUpdate());
        hilfeMenu.DropDownItems.Add("Info", null, (_, _) => MessageBox.Show($"KOFplanner v{_update.CurrentVersion}\nAuthor: Lukas Sonderegger", "Info"));
        MainMenuStrip = menu;
        Controls.Add(menu);

        // TableLayoutPanel guarantees the 20px spacer and reserves the rest for the
        // TabControl, so the tab strip always renders at its full ItemSize height.
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.Control }, 0, 0);

        // Main TabControl. Reserve exactly the tab-strip height so nothing is clipped.
        _tabControl = new TabControl { Dock = DockStyle.Fill, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(260, 42), Padding = new Point(0, 0) };
        _tabControl.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
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
        layout.Controls.Add(_tabControl, 0, 1);
        Controls.Add(layout);

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
        btnPrev.Click += (_, _) => Navigate(-1);
        StyleButton(btnPrev);
        var btnNext = new Button { Text = ">", Width = 40, Height = 28, Location = new Point(60, 6) };
        btnNext.Click += (_, _) => Navigate(1);
        StyleButton(btnNext);
        var btnToday = new Button { Text = "Heute", Width = 60, Height = 28, Location = new Point(105, 6) };
        btnToday.Click += (_, _) => { _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1); _dragStartDate = _dragEndDate = null; RefreshCalendar(); };
        StyleButton(btnToday);
        var btnView = new Button { Text = "Wochenansicht", Width = 110, Height = 28, Location = new Point(175, 6) };
        btnView.Click += (_, _) => { _weekView = !_weekView; btnView.Text = _weekView ? "Monatsansicht" : "Wochenansicht"; _dragStartDate = _dragEndDate = null; RefreshCalendar(); };
        StyleButton(btnView);
        _lblMonthYear = new Label { Text = "", Location = new Point(300, 8), AutoSize = true, Font = new Font(Font.FontFamily, 12, FontStyle.Bold) };
        nav.Controls.AddRange(new Control[] { btnPrev, btnNext, btnToday, btnView, _lblMonthYear });
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
        var empBtns = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Padding = new Padding(0) };
        var btnEmpNew = new Button { Text = "Neu", Width = 80, Height = 28 }; btnEmpNew.Click += (_, _) => EditEmployee(null); StyleButton(btnEmpNew);
        var btnEmpEdit = new Button { Text = "Bearbeiten", Width = 100, Height = 28 }; btnEmpEdit.Click += (_, _) => { if (_lbEmployees.SelectedItem is Employee e) EditEmployee(e); }; StyleButton(btnEmpEdit);
        var btnEmpDel = new Button { Text = "Löschen", Width = 85, Height = 28 }; btnEmpDel.Click += (_, _) => { if (_lbEmployees.SelectedItem is Employee e) DeleteEmployee(e); }; StyleButton(btnEmpDel);
        empBtns.Controls.AddRange(new Control[] { btnEmpEdit, btnEmpDel, btnEmpNew });
        empHeader.Controls.Add(empBtns);
        _lbEmployees = new ListBox { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 40 };
        _lbEmployees.DrawItem += EmployeeList_DrawItem;
        _lbEmployees.MouseDown += EmployeeList_MouseDown;
        colEmp.Controls.Add(_lbEmployees);
        colEmp.Controls.Add(empHeader);
        t2Grid.Controls.Add(colEmp, 0, 0);

        // ---- Spalte 2: Teams ----
        var colTeam = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 0, 4, 0) };
        var teamHeader = new Panel { Dock = DockStyle.Top, Height = 30 };
        teamHeader.Controls.Add(new Label { Text = "Teams", Dock = DockStyle.Left, AutoSize = true, Font = new Font(Font.FontFamily, 11, FontStyle.Bold), Padding = new Padding(0, 4, 0, 0) });
        colTeam.Controls.Add(teamHeader);
        _flowTeamCards = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(2, 8, 2, 4) };
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
        var vehBtns = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Padding = new Padding(0) };
        var btnVehNew = new Button { Text = "Neu", Width = 80, Height = 28 }; btnVehNew.Click += (_, _) => EditVehicle(null); StyleButton(btnVehNew);
        var btnVehEdit = new Button { Text = "Bearbeiten", Width = 100, Height = 28 }; btnVehEdit.Click += (_, _) => { if (_lbVehicles.SelectedItem is Vehicle v) EditVehicle(v); }; StyleButton(btnVehEdit);
        var btnVehDel = new Button { Text = "Löschen", Width = 85, Height = 28 }; btnVehDel.Click += (_, _) => { if (_lbVehicles.SelectedItem is Vehicle v) DeleteVehicle(v); }; StyleButton(btnVehDel);
        vehBtns.Controls.AddRange(new Control[] { btnVehEdit, btnVehDel, btnVehNew });
        vehHeader.Controls.Add(vehBtns);
        _lbVehicles = new ListBox { Dock = DockStyle.Fill, DisplayMember = "ToString" };
        _lbVehicles.MouseDown += VehicleList_MouseDown;
        colVeh.Controls.Add(_lbVehicles);
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

        // ========== TAB 5: MITARBEITER INFORMIEREN ==========
        var tabInform = new TabPage("Mitarbeiter informieren");
        tabInform.Controls.Add(new InformEmployeesForm(_db, _settings) { Dock = DockStyle.Fill });
        _tabControl.TabPages.Add(tabInform);

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

    private void OpenSettings()
    {
        using var f = new SettingsForm(_settings);
        f.ShowDialog(this);
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
            if (card.Tag is Team t)
                card.BackColor = t.Color;
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
        using var licBrush = new SolidBrush((e.State & DrawItemState.Selected) == DrawItemState.Selected
            ? Color.White : Color.FromArgb(0x66, 0x66, 0x66));
        e.Graphics.DrawString(name, nameFont, nameBrush, e.Bounds.Left + 5, e.Bounds.Top + 4);
        e.Graphics.DrawString(lic, licFont, licBrush, e.Bounds.Left + 5, e.Bounds.Top + 22);
        e.DrawFocusRectangle();
    }

    private void Navigate(int step)
    {
        if (_weekView)
        {
            var monday = MondayOf(_currentMonth);
            var target = monday.AddDays(step * 7);
            _currentMonth = new DateTime(target.Year, target.Month, 1);
            _dragStartDate = _dragEndDate = null;
            RefreshCalendar();
        }
        else
        {
            _currentMonth = _currentMonth.AddMonths(step);
            _dragStartDate = _dragEndDate = null;
            RefreshCalendar();
        }
    }

    private static DateTime MondayOf(DateTime d)
    {
        var diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
        return d.AddDays(-diff).Date;
    }

    private void RefreshCalendar()
    {
        if (_weekView)
        {
            var mon = MondayOf(_currentMonth);
            _lblMonthYear.Text = $"{mon:dd.MM.} – {mon.AddDays(6):dd.MM.yyyy}";
            _monthAssignments = _db.GetAllAssignments(mon, mon.AddDays(6));
        }
        else
        {
            _lblMonthYear.Text = _currentMonth.ToString("MMMM yyyy");
            _monthAssignments = _db.GetMonthAssignments(_currentMonth);
        }
        _vacations = _db.GetAllVacations();
        _sickness = _db.GetAllSickness();
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
        int rows = _weekView ? 1 : 6;
        _calendarDayHeight = h / rows;
        if (_calendarDayWidth < 10) return;

        DateTime gridStart;
        if (_weekView)
        {
            gridStart = MondayOf(_currentMonth);
        }
        else
        {
            var firstDow = (int)_currentMonth.DayOfWeek;
            var offset = firstDow == 0 ? 6 : firstDow - 1;
            gridStart = new DateTime(_currentMonth.Year, _currentMonth.Month, 1).AddDays(-offset);
        }

        var dayNames = new[] { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };
        using var hf = new Font(Font, FontStyle.Bold);
        using var hb = new SolidBrush(SystemColors.WindowText);
        for (int i = 0; i < 7; i++)
            g.DrawString(dayNames[i], hf, hb, _calendarOrigin.X + i * _calendarDayWidth + 5, _calendarOrigin.Y - 25);

        var today = DateTime.Today;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                var date = gridStart.AddDays(row * 7 + col);
                var x = _calendarOrigin.X + col * _calendarDayWidth;
                var y = _calendarOrigin.Y + row * _calendarDayHeight;
                DrawDayCell(g, date, x, y, _calendarDayWidth, _calendarDayHeight, false);
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

        // Shift+hover zoom overlay
        if (_hoverDate.HasValue && (ModifierKeys & Keys.Shift) != 0)
        {
            var (zx, zy) = GetCellPosition(_hoverDate.Value);
            if (zx >= 0)
            {
                var zx2 = (int)(zx - _calendarDayWidth * 0.4);
                var zy2 = (int)(zy - _calendarDayHeight * 0.4);
                var zw = (int)(_calendarDayWidth * 1.8);
                var zh = (int)(_calendarDayHeight * 1.8);
                using var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0));
                g.FillRectangle(shadow, zx2 + 4, zy2 + 4, zw, zh);
                DrawDayCell(g, _hoverDate.Value, zx2, zy2, zw, zh, true);
            }
        }
    }

    private void DrawDayCell(Graphics g, DateTime date, int x, int y, int cw, int ch, bool zoomed)
    {
        var back = Color.White;
        if (IsInDragRange(date)) back = Color.FromArgb(200, 220, 255);
        else if (date == DateTime.Today) back = Color.FromArgb(220, 235, 252);
        else if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) back = Color.FromArgb(248, 248, 248);
        using var bb = new SolidBrush(back);
        g.FillRectangle(bb, x, y, cw, ch);
        using var p2 = new Pen(zoomed ? Color.FromArgb(0x2E, 0x7D, 0x32) : Color.FromArgb(0xBB, 0xBB, 0xBB), zoomed ? 2 : 1);
        g.DrawRectangle(p2, x, y, cw, ch);

        using var df = new Font(Font.FontFamily, zoomed ? 10 : 8);
        using var db2 = new SolidBrush(date == DateTime.Today ? Color.Blue : SystemColors.WindowText);
        g.DrawString(date.Day.ToString(), df, db2, x + 3, y + 2);

        var dayAssignments = _monthAssignments.Where(a => a.Date.Date == date.Date).ToList();
        if (dayAssignments.Count > 0 || HasDayBlock(date))
            DrawDayLines(g, date, dayAssignments, x, y, cw, ch, zoomed);
    }

    private void DrawDayLines(Graphics g, DateTime date, List<Assignment> day, int x, int y, int cw, int ch, bool zoomed)
    {
        var lines = new List<(string Label, Color Fill, Color Border, Color Text)>();

        var teamAssignments = day.Where(a => a.Team != null).ToList();
        var connectedSiteIds = new HashSet<int>();

        foreach (var ta in teamAssignments)
        {
            var team = ta.Team!;
            var site = ta.Site!;
            connectedSiteIds.Add(site.Id);
            var color = team.Color;
            var vehicles = day.Where(a => a.Vehicle != null && a.ConstructionSiteId == site.Id)
                              .Select(a => a.Vehicle!).DistinctBy(v => v.Id)
                              .OrderBy(v => v.VehicleNumber).ToList();
            var vehicleText = vehicles.Count > 0 ? string.Join(", ", vehicles.Select(v => v.VehicleNumber)) : "–";
            lines.Add(($"{site.Name} / {team.Name} / {vehicleText}{SpanSuffix(ta)}", color, color, Color.White));
        }

        foreach (var site in day.Select(a => a.Site).OfType<ConstructionSite>()
                     .Where(s => !connectedSiteIds.Contains(s.Id)).DistinctBy(s => s.Id).OrderBy(s => s.Name))
            lines.Add((site.Name + SpanSuffix(site.Id, null, null, null), Color.White, Color.Black, Color.Black));

        foreach (var v in day.Select(a => a.Vehicle).OfType<Vehicle>()
                      .Where(v => !connectedSiteIds.Contains(v.Id == 0 ? -1 : day.First(a => a.Vehicle != null && a.Vehicle.Id == v.Id).ConstructionSiteId))
                      .DistinctBy(v => v.Id).OrderBy(v => v.VehicleNumber))
            lines.Add((v.VehicleNumber + SpanSuffix(null, null, v.Id, null), Color.White, Color.Black, Color.Black));

        foreach (var emp in day.Select(a => a.Employee).OfType<Employee>().DistinctBy(e => e.Id).OrderBy(e => e.FullName))
            lines.Add(($"PN: {emp.FullName}{SpanSuffix(null, null, null, emp.Id)}", Color.White, Color.Black, Color.Black));

        // Vacation / Sickness (light gray bars)
        var grayFill = Color.FromArgb(0xEE, 0xEE, 0xEE);
        var grayBorder = Color.FromArgb(0x99, 0x99, 0x99);
        foreach (var vac in _vacations.Where(v => date >= v.StartDate.Date && date <= v.EndDate.Date))
        {
            var name = vac.Employee?.FullName ?? $"MA {vac.EmployeeId}";
            var suffix = vac.StartDate.Date == vac.EndDate.Date ? "" : $" ({vac.StartDate:dd.MM.}–{vac.EndDate:dd.MM.})";
            lines.Add(($"Urlaub: {name}{suffix}", grayFill, grayBorder, Color.Black));
        }
        foreach (var sic in _sickness.Where(s => date >= s.StartDate.Date && date <= s.EndDate.Date))
        {
            var name = sic.Employee?.FullName ?? $"MA {sic.EmployeeId}";
            var suffix = sic.StartDate.Date == sic.EndDate.Date ? "" : $" ({sic.StartDate:dd.MM.}–{sic.EndDate:dd.MM.})";
            lines.Add(($"Krank: {name}{suffix}", grayFill, grayBorder, Color.Black));
        }

        using var sf = new Font(Font.FontFamily, zoomed ? 9 : 7);
        int ly = y + (zoomed ? 22 : 16);
        var lineH = zoomed ? 18 : 14;
        var maxY = y + ch - 4;
        var maxChars = zoomed ? 80 : Math.Max(8, cw / 6);
        foreach (var (label, lf, lb, lt) in lines)
        {
            if (ly > maxY) break;
            var lx = x + 2;
            var lw = cw - 4;
            using var fb = new SolidBrush(lf);
            g.FillRectangle(fb, lx, ly, lw, lineH);
            using var bp = new Pen(lb, 1);
            g.DrawRectangle(bp, lx, ly, lw, lineH);
            var detail = FitText(g, sf, label, lw - 4, maxChars);
            using var tb = new SolidBrush(lt);
            g.DrawString(detail, sf, tb, lx + 2, ly + (zoomed ? 3 : 1));
            ly += lineH + 1;
        }
    }

    private string SpanSuffix(Assignment sample)
    {
        return SpanSuffix(sample.ConstructionSiteId, sample.TeamId, sample.VehicleId, sample.EmployeeId);
    }

    private string SpanSuffix(int? siteId, int? teamId, int? vehicleId, int? empId)
    {
        var matches = _monthAssignments.Where(a =>
            a.ConstructionSiteId == (siteId ?? a.ConstructionSiteId) &&
            a.TeamId == (teamId ?? a.TeamId) &&
            a.VehicleId == (vehicleId ?? a.VehicleId) &&
            a.EmployeeId == (empId ?? a.EmployeeId)).ToList();
        if (matches.Count < 2) return "";
        var min = matches.Min(a => a.Date.Date);
        var max = matches.Max(a => a.Date.Date);
        if (min == max) return "";
        return $" ({min:dd.MM.}–{max:dd.MM.})";
    }

    private static string FitText(Graphics g, Font f, string text, int maxWidth, int maxChars)
    {
        if (text.Length <= maxChars && g.MeasureString(text, f).Width <= maxWidth) return text;
        var s = text;
        while (s.Length > 4 && g.MeasureString(s + "..", f).Width > maxWidth)
            s = s[..(s.Length - 1)];
        return s + "..";
    }

    private bool HasDayBlock(DateTime date)
    {
        return _vacations.Any(v => date >= v.StartDate.Date && date <= v.EndDate.Date)
            || _sickness.Any(s => date >= s.StartDate.Date && date <= s.EndDate.Date);
    }

    private bool IsInDragRange(DateTime d)
    {
        if (!_dragStartDate.HasValue || !_dragEndDate.HasValue) return false;
        var min = _dragStartDate.Value < _dragEndDate.Value ? _dragStartDate.Value : _dragEndDate.Value;
        var max = _dragStartDate.Value > _dragEndDate.Value ? _dragStartDate.Value : _dragEndDate.Value;
        return d >= min && d <= max;
    }

    private DateTime GridStart()
    {
        if (_weekView) return MondayOf(_currentMonth);
        var firstDow = (int)_currentMonth.DayOfWeek;
        var offset = firstDow == 0 ? 6 : firstDow - 1;
        return new DateTime(_currentMonth.Year, _currentMonth.Month, 1).AddDays(-offset);
    }

    private (int x, int y) GetCellPosition(DateTime date)
    {
        if (_weekView)
        {
            var mon = MondayOf(_currentMonth);
            if (date < mon || date > mon.AddDays(6)) return (-1, -1);
            var idx = (date - mon).Days;
            return (_calendarOrigin.X + idx * _calendarDayWidth, _calendarOrigin.Y);
        }
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
        if (col < 0 || col > 6) return null;
        var maxRow = _weekView ? 0 : 5;
        if (row < 0 || row > maxRow) return null;
        var gridStart = GridStart();
        var date = gridStart.AddDays(row * 7 + col);
        if (_weekView) return date;
        if (date.Year != _currentMonth.Year || date.Month != _currentMonth.Month) return null;
        return date;
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
            if ((ModifierKeys & Keys.Shift) != 0)
            {
                var d = GetDateFromPoint(e.Location);
                if (d != _hoverDate)
                {
                    _hoverDate = d;
                    _calendarPanel.Invalidate();
                }
            }
            else if (_hoverDate.HasValue)
            {
                _hoverDate = null;
                _calendarPanel.Invalidate();
            }
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
        var das = _monthAssignments.Where(a => a.Date.Date == day.Date).ToList();

        using var f = new Form();
        f.Text = $"Übersicht {day:dd.MM.yyyy}";
        f.Size = new Size(460, 560);
        f.MinimumSize = new Size(380, 320);
        f.StartPosition = FormStartPosition.CenterParent;
        f.Font = Font;

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = f.Height - 60, IsSplitterFixed = true };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(16) };
        var bottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        var btnClose = new Button { Text = "Schliessen", Dock = DockStyle.Right, Width = 110, Height = 34 };
        StyleButton(btnClose);
        btnClose.Click += (_, _) => f.Close();
        var btnDelete = new Button { Text = "Löschen", Dock = DockStyle.Left, Width = 110, Height = 34, BackColor = Color.FromArgb(0xF4, 0x43, 0x36), ForeColor = Color.White };
        StyleButton(btnDelete);
        bottom.Controls.AddRange(new Control[] { btnDelete, btnClose });

        var entries = BuildDayEntries(das);

        AssignmentEntry? selected = null;
        var entryPanels = new List<(Panel panel, AssignmentEntry entry)>();

        void SelectEntry(AssignmentEntry entry, Panel panel)
        {
            selected = entry;
            foreach (var (p, _) in entryPanels)
                p.BackColor = SystemColors.Window;
            panel.BackColor = Color.FromArgb(0xC8, 0xE6, 0xC9);
        }

        if (entries.Count == 0)
        {
            flow.Controls.Add(new Label { Text = "Keine Einträge an diesem Tag.", ForeColor = SystemColors.GrayText, AutoSize = true });
        }
        else
        {
            foreach (var entry in entries)
            {
                var span = entry.From == entry.To
                    ? $"{entry.From:dd.MM.yyyy}"
                    : $"{entry.From:dd.MM.yyyy} – {entry.To:dd.MM.yyyy}";
                var text = entry.Label + "\n" + (entry.From == entry.To ? $"am {span}" : $"von {span}");

                var p = new Panel { Width = flow.Width - 40, Height = 46, Margin = new Padding(0, 0, 0, 8), BorderStyle = BorderStyle.FixedSingle, BackColor = SystemColors.Window, Cursor = Cursors.Hand };
                var lbl = new Label { Text = text, Location = new Point(8, 6), AutoSize = false, Width = p.Width - 16, Height = p.Height - 12 };
                p.Controls.Add(lbl);
                p.Click += (_, _) => SelectEntry(entry, p);
                lbl.Click += (_, _) => SelectEntry(entry, p);
                flow.Controls.Add(p);
                entryPanels.Add((p, entry));
            }
        }

        btnDelete.Enabled = entries.Count > 0;
        btnDelete.Click += (_, _) =>
        {
            if (selected == null) { MessageBox.Show("Bitte zuerst einen Eintrag auswählen.", "Löschen", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            DeleteEntry(selected, day, f);
        };

        split.Panel1.Controls.Add(flow);
        split.Panel2.Controls.Add(bottom);
        f.Controls.Add(split);
        f.ShowDialog();
    }

    private sealed class AssignmentEntry
    {
        public int ConstructionSiteId { get; set; }
        public int? TeamId { get; set; }
        public int? VehicleId { get; set; }
        public int? EmployeeId { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<Assignment> Assignments { get; set; } = new();
        public string Label { get; set; } = "";
        public bool MultiDay => From != To;
    }

    private List<AssignmentEntry> BuildDayEntries(List<Assignment> day)
    {
        var groups = day.GroupBy(a => (a.ConstructionSiteId, a.TeamId, a.VehicleId, a.EmployeeId))
                        .Select(g =>
                        {
                            var all = g.ToList();
                            var dates = all.Select(a => a.Date.Date).Distinct().OrderBy(d => d).ToList();
                            // Expanding contiguous runs yields overall min/max as the entry span
                            var min = dates.Min();
                            var max = dates.Max();
                            var a0 = all.First();
                            var site = a0.Site ?? _sites.FirstOrDefault(s => s.Id == a0.ConstructionSiteId);
                            var team = a0.Team ?? (a0.TeamId.HasValue ? _teams.FirstOrDefault(t => t.Id == a0.TeamId.Value) : null);
                            var vehicle = a0.Vehicle ?? (a0.VehicleId.HasValue ? _vehicles.FirstOrDefault(v => v.Id == a0.VehicleId.Value) : null);
                            var emp = a0.Employee ?? (a0.EmployeeId.HasValue ? _employees.FirstOrDefault(e => e.Id == a0.EmployeeId.Value) : null);

                            var parts = new List<string>();
                            if (site != null) parts.Add(site.Name);
                            if (team != null) parts.Add(team.Name);
                            if (vehicle != null) parts.Add(vehicle.VehicleNumber);
                            if (emp != null) parts.Add(emp.FullName);
                            var label = parts.Count > 0 ? string.Join(" / ", parts) : "Eintrag";

                            return new AssignmentEntry
                            {
                                ConstructionSiteId = a0.ConstructionSiteId,
                                TeamId = a0.TeamId,
                                VehicleId = a0.VehicleId,
                                EmployeeId = a0.EmployeeId,
                                From = min,
                                To = max,
                                Assignments = all,
                                Label = label
                            };
                        })
                        .OrderBy(e => e.Label)
                        .ToList();
        return groups;
    }

    private void DeleteEntry(AssignmentEntry entry, DateTime day, Form owner)
    {
        if (entry.MultiDay)
        {
            using var dlg = new Form();
            dlg.Text = "Löschen – Mehrfachzuweisung";
            dlg.Size = new Size(420, 230);
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = dlg.MinimizeBox = false;
            dlg.Font = Font;
            dlg.Controls.Add(new Label { Text = $"{entry.Label}\n{entry.From:dd.MM.yyyy} – {entry.To:dd.MM.yyyy}", Location = new Point(16, 16), AutoSize = true });
            dlg.Controls.Add(new Label { Text = "Wie soll gelöscht werden?", Location = new Point(16, 56), AutoSize = true });
            var btnWhole = new Button { Text = "Ganzer Termin", Location = new Point(16, 110), Width = 170, Height = 38, BackColor = Color.FromArgb(0xF4, 0x43, 0x36), ForeColor = Color.White };
            StyleButton(btnWhole);
            var btnDay = new Button { Text = "Nur an diesem Tag", Location = new Point(206, 110), Width = 180, Height = 38 };
            StyleButton(btnDay);
            var result = 0;
            btnWhole.Click += (_, _) => { result = 1; dlg.Close(); };
            btnDay.Click += (_, _) => { result = 2; dlg.Close(); };
            dlg.Controls.AddRange(new Control[] { btnWhole, btnDay });
            dlg.ShowDialog(owner);

            if (result == 1)
            {
                foreach (var a in entry.Assignments)
                    _db.DeleteAssignment(a.Id);
            }
            else if (result == 2)
            {
                foreach (var a in entry.Assignments.Where(a => a.Date.Date == day.Date))
                    _db.DeleteAssignment(a.Id);
            }
            else return;
        }
        else
        {
            if (MessageBox.Show($"Eintrag löschen?\n{entry.Label}\n{entry.From:dd.MM.yyyy}", "Löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            foreach (var a in entry.Assignments)
                _db.DeleteAssignment(a.Id);
        }

        RefreshCalendar();
        owner.Close();
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
                if (!_db.IsSiteAssigned(site.Id, d) && !_db.IsTeamAssigned(team.Id, d))
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
            if (!_db.IsSiteAssigned(site.Id, d) && !_db.IsTeamAssigned(team.Id, d))
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
            if (e.Button == MouseButtons.Left)
            {
                _selectedTeam = team;
                RefreshTeamView();
            }
            else if (e.Button == MouseButtons.Right)
                ShowTeamContextMenu(team, card.PointToScreen(e.Location));
        };

        card.Paint += (s, pe) =>
        {
            if (_selectedTeam != null && _selectedTeam.Id == team.Id)
            {
                var sel = GetContrastText(team.Color);
                using var p = new Pen(sel, 3);
                pe.Graphics.DrawRectangle(p, 1, 1, card.Width - 3, card.Height - 3);
            }
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

