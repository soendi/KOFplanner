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
    private readonly List<CalendarSpan> _spans = new();
    private readonly HashSet<string> _multiDayKeys = new();

    private sealed class CalendarSpan
    {
        public DateTime From;
        public DateTime To;
        public string Label = "";
        public Color Fill;
        public Color Border;
        public Color Text;
    }
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
        private readonly ListView _lvVehicles;

    // Tab 4 controls (Sites)
    private readonly ListView _lvSites;

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
        dateiMenu.DropDownItems.Add("Datenbank wiederherstellen...", null, (_, _) => RestoreDatabase());
        dateiMenu.DropDownItems.Add("Google Drive Backup...", null, (_, _) => ConfigureBackup());
        dateiMenu.DropDownItems.Add(new ToolStripSeparator());
        dateiMenu.DropDownItems.Add("Einstellungen...", null, (_, _) => OpenSettings());
        dateiMenu.DropDownItems.Add(new ToolStripSeparator());
        dateiMenu.DropDownItems.Add("Beenden", null, (_, _) => Close());
        var hilfeMenu = menu.Items.Add("&Hilfe") as ToolStripMenuItem;
        hilfeMenu!.DropDownItems.Add("Hilfe öffnen...", null, (_, _) => OpenHelp());
        hilfeMenu.DropDownItems.Add("Nach Updates suchen...", null, async (_, _) => await CheckUpdate());
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
        var btnEmpDel = new Button { Text = "Löschen", Width = 100, Height = 28 }; btnEmpDel.Click += (_, _) => { if (_lbEmployees.SelectedItem is Employee e) DeleteEmployee(e); }; StyleButton(btnEmpDel);
        empBtns.Controls.AddRange(new Control[] { btnEmpDel, btnEmpEdit, btnEmpNew });
        empHeader.Controls.Add(empBtns);
        _lbEmployees = new ListBox { Dock = DockStyle.Fill, DisplayMember = "FullName" };
        _lbEmployees.MouseDown += EmployeeList_MouseDown;
        _lbEmployees.MouseDoubleClick += (_, _) => { if (_lbEmployees.SelectedItem is Employee e) EditEmployee(e); };
        colEmp.Controls.Add(_lbEmployees);
        colEmp.Controls.Add(empHeader);
        t2Grid.Controls.Add(colEmp, 0, 0);

        // ---- Spalte 2: Teams ----
        var colTeam = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 0, 4, 0) };
        var teamHeader = new Panel { Dock = DockStyle.Top, Height = 30 };
        teamHeader.Controls.Add(new Label { Text = "Teams", Dock = DockStyle.Left, AutoSize = true, Font = new Font(Font.FontFamily, 11, FontStyle.Bold), Padding = new Padding(0, 4, 0, 0) });
        colTeam.Controls.Add(teamHeader);
        _flowTeamCards = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(2, 20, 2, 4) };
        _flowTeamCards.Resize += (_, _) => LayoutTeamCards();
        _pnlNewTeamDropZone = new Panel
        {
            Height = 76, BackColor = Color.FromArgb(0xE8, 0xF5, 0xE9),
            BorderStyle = BorderStyle.FixedSingle, AllowDrop = true, Margin = new Padding(0, 20, 0, 12),
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
        var btnVehDel = new Button { Text = "Löschen", Width = 80, Height = 28 }; btnVehDel.Click += (_, _) =>
        {
            if (_lvVehicles.SelectedItems.Count > 0 && _lvVehicles.SelectedItems[0].Tag is Vehicle v) DeleteVehicle(v);
        }; StyleButton(btnVehDel);
        vehBtns.Controls.Add(btnVehNew);
        vehBtns.Controls.Add(btnVehDel);
        vehHeader.Controls.Add(vehBtns);
        _lvVehicles = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = false, HeaderStyle = ColumnHeaderStyle.Nonclickable };
        _lvVehicles.Columns.Add("Fahrzeugnummer", 110);
        _lvVehicles.Columns.Add("Fahrzeugeigenschaft", 130);
        _lvVehicles.Columns.Add("Kennzeichen", 120);
        _lvVehicles.Columns.Add("Info", 200);
        _lvVehicles.Resize += (_, _) => ResizeListColumns(_lvVehicles);
        _lvVehicles.MouseDoubleClick += (_, e) =>
        {
            if (_lvVehicles.GetItemAt(e.X, e.Y)?.Tag is Vehicle v) EditVehicle(v);
        };
        _lvVehicles.ItemDrag += (_, e) =>
        {
            if (_lvVehicles.SelectedItems.Count > 0 && _lvVehicles.SelectedItems[0].Tag is Vehicle v)
                _lvVehicles.DoDragDrop(v, DragDropEffects.Move);
        };
        colVeh.Controls.Add(_lvVehicles);
        colVeh.Controls.Add(vehHeader);
        t2Grid.Controls.Add(colVeh, 2, 0);

        tabMA.Controls.Add(t2Grid);
        _tabControl.TabPages.Add(tabMA);

        // ========== TAB 4: BAUSTELLEN ==========
        var tabSite = new TabPage("Baustellen");
        _lvSites = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = false, HeaderStyle = ColumnHeaderStyle.Nonclickable };
        _lvSites.Columns.Add("Name", 160);
        _lvSites.Columns.Add("Adresse", 200);
        _lvSites.Columns.Add("Zeitraum", 160);
        _lvSites.Columns.Add("Info", 200);
        _lvSites.Resize += (_, _) => ResizeListColumns(_lvSites);
        _lvSites.MouseDoubleClick += (_, e) =>
        {
            if (_lvSites.GetItemAt(e.X, e.Y)?.Tag is ConstructionSite s) EditSite(s);
        };
        var siteBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 30 };
        var btnSiteNew = new Button { Text = "Neu", Width = 75 }; btnSiteNew.Click += (_, _) => EditSite(null); StyleButton(btnSiteNew);
        var btnSiteDel = new Button { Text = "Löschen", Width = 75 }; btnSiteDel.Click += (_, _) =>
        {
            if (_lvSites.SelectedItems.Count > 0 && _lvSites.SelectedItems[0].Tag is ConstructionSite s) DeleteSite(s);
        }; StyleButton(btnSiteDel);
        siteBtns.Controls.Add(btnSiteNew);
        siteBtns.Controls.Add(btnSiteDel);
        tabSite.Controls.Add(_lvSites);
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
        RefreshTeamView();
        RefreshVehicleList();
        RefreshSiteList();
        RefreshCalendar();
    }

    private void RefreshVehicleList()
    {
        _lvVehicles.Items.Clear();
        foreach (var v in _vehicles)
        {
            var item = new ListViewItem(new[]
            {
                v.VehicleNumber,
                v.RequiredLicense,
                v.LicensePlate,
                v.ToString()
            })
            { Tag = v };
            _lvVehicles.Items.Add(item);
        }
        ResizeListColumns(_lvVehicles);
    }

    private void RefreshSiteList()
    {
        _lvSites.Items.Clear();
        foreach (var s in _sites.OrderBy(s => s.Name))
        {
            var item = new ListViewItem(new[]
            {
                s.Name,
                s.Address,
                s.EndDate.HasValue ? $"{s.StartDate:dd.MM.yy} – {s.EndDate.Value:dd.MM.yy}" : s.StartDate.ToString("dd.MM.yy"),
                s.DisplayText
            })
            { Tag = s };
            _lvSites.Items.Add(item);
        }
        ResizeListColumns(_lvSites);
    }

    private Panel MakeCrudRow(int parentWidth, string text, Action onDelete, Action? onEdit = null, Func<object>? dragData = null)
    {
        var line = new Panel { Width = Math.Max(180, parentWidth - 16), Height = 32, Margin = new Padding(0, 0, 0, 4), BorderStyle = BorderStyle.FixedSingle, BackColor = SystemColors.Window };
        var lbl = new Label { Text = text, Location = new Point(6, 4), AutoSize = true, MaximumSize = new Size(line.Width - 40, 0), Padding = new Padding(0, 3, 0, 0), Cursor = dragData != null ? Cursors.Hand : Cursors.Default };
        if (onEdit != null) lbl.DoubleClick += (_, _) => onEdit();
        if (dragData != null)
            lbl.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && lbl.Parent is Panel p && p.ClientRectangle.Contains(e.Location) && e.Location.X < line.Width - 30)
                    lbl.DoDragDrop(dragData(), DragDropEffects.Move);
            };
        var btnX = new Button { Text = "X", Width = 26, Height = 24, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0xF4, 0x43, 0x36), ForeColor = Color.White, Cursor = Cursors.Hand };
        btnX.FlatAppearance.BorderSize = 0;
        btnX.Location = new Point(line.Width - 26 - 2, 4);
        btnX.Click += (_, _) => onDelete();
        line.Controls.Add(lbl);
        line.Controls.Add(btnX);
        return line;
    }

    private static void ResizeListColumns(ListView lv)
    {
        if (lv.Columns.Count == 0) return;
        var last = lv.Columns[lv.Columns.Count - 1];
        var used = 0;
        for (var i = 0; i < lv.Columns.Count - 1; i++) used += lv.Columns[i].Width;
        last.Width = Math.Max(60, lv.ClientSize.Width - used - 4);
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

    private static Color GetContrastText(Color bg)
    {
        var lum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
        return lum > 0.6 ? Color.Black : Color.White;
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
        BuildSpans();
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

        // Continuous bars for multi-day assignments (one bar spanning all days)
        DrawSpanBars(g);

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
            var site = ta.Site!;
            if (_multiDayKeys.Contains($"T:{site.Id}:{ta.TeamId}")) continue;
            var team = ta.TeamId.HasValue ? _teams.FirstOrDefault(t => t.Id == ta.TeamId.Value) ?? ta.Team! : ta.Team!;
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
        {
            if (_multiDayKeys.Contains($"S:{site.Id}")) continue;
            lines.Add((site.Name + SpanSuffix(site.Id, null, null, null), Color.White, Color.Black, Color.Black));
        }

        foreach (var v in day.Select(a => a.Vehicle).OfType<Vehicle>()
                      .Where(v => !connectedSiteIds.Contains(v.Id == 0 ? -1 : day.First(a => a.Vehicle != null && a.Vehicle.Id == v.Id).ConstructionSiteId))
                      .DistinctBy(v => v.Id).OrderBy(v => v.VehicleNumber))
        {
            if (_multiDayKeys.Contains($"V:{v.Id}")) continue;
            lines.Add((v.VehicleNumber + SpanSuffix(null, null, v.Id, null), Color.White, Color.Black, Color.Black));
        }

        foreach (var emp in day.Select(a => a.Employee).OfType<Employee>().DistinctBy(e => e.Id).OrderBy(e => e.FullName))
        {
            if (_multiDayKeys.Contains($"E:{emp.Id}")) continue;
            lines.Add(($"PN: {emp.FullName}{SpanSuffix(null, null, null, emp.Id)}", Color.White, Color.Black, Color.Black));
        }

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

    private static string SpanKey(Assignment a)
    {
        if (a.TeamId.HasValue) return $"T:{a.ConstructionSiteId}:{a.TeamId}";
        if (a.VehicleId.HasValue) return $"V:{a.VehicleId}";
        if (a.EmployeeId.HasValue) return $"E:{a.EmployeeId}";
        return $"S:{a.ConstructionSiteId}";
    }

    private void BuildSpans()
    {
        _spans.Clear();
        _multiDayKeys.Clear();
        var groups = _monthAssignments
            .GroupBy(SpanKey)
            .Where(g => g.Min(a => a.Date.Date) != g.Max(a => a.Date.Date))
            .ToList();
        foreach (var g in groups)
        {
            var min = g.Min(a => a.Date.Date);
            var max = g.Max(a => a.Date.Date);
            var sample = g.OrderBy(a => a.Date).First();
            _spans.Add(new CalendarSpan
            {
                From = min,
                To = max,
                Label = SpanLabel(sample) + $" ({min:dd.MM.}–{max:dd.MM.})",
                Fill = SpanFill(sample),
                Border = SpanBorder(sample),
                Text = SpanText(sample)
            });
            _multiDayKeys.Add(g.Key);
        }
    }

    private string SpanLabel(Assignment a)
    {
        if (a.TeamId.HasValue)
        {
            var team = a.TeamId.HasValue ? _teams.FirstOrDefault(t => t.Id == a.TeamId.Value) ?? a.Team! : a.Team!;
            var site = a.Site!;
            var vehicle = a.Vehicle;
            var vehicleText = vehicle != null ? vehicle.VehicleNumber : "–";
            return $"{site.Name} / {team.Name} / {vehicleText}";
        }
        if (a.VehicleId.HasValue) return a.Vehicle!.VehicleNumber;
        if (a.EmployeeId.HasValue) return $"PN: {a.Employee!.FullName}";
        return a.Site!.Name;
    }

    private Color SpanFill(Assignment a)
    {
        if (!a.TeamId.HasValue) return Color.White;
        var t = _teams.FirstOrDefault(x => x.Id == a.TeamId.Value) ?? a.Team;
        return t?.Color ?? Color.Gray;
    }
    private Color SpanBorder(Assignment a)
    {
        if (!a.TeamId.HasValue) return Color.Black;
        var t = _teams.FirstOrDefault(x => x.Id == a.TeamId.Value) ?? a.Team;
        return t?.Color ?? Color.Gray;
    }
    private static Color SpanText(Assignment a) => a.TeamId.HasValue ? Color.White : Color.Black;

    private void DrawSpanBars(Graphics g)
    {
        using var sf = new Font(Font.FontFamily, 7);
        foreach (var s in _spans)
        {
            var (x1, y1) = GetCellPosition(s.From);
            var (x2, y2) = GetCellPosition(s.To);
            if (x1 < 0 || x2 < 0) continue;
            var barY = y1 + 16;
            var barX = x1 + 2;
            var barW = (x2 - x1) + _calendarDayWidth - 4;
            var barH = 12;
            using var fb = new SolidBrush(s.Fill);
            g.FillRectangle(fb, barX, barY, barW, barH);
            using var bp = new Pen(s.Border, 1);
            g.DrawRectangle(bp, barX, barY, barW, barH);
            var detail = FitText(g, sf, s.Label, barW - 4, 120);
            using var tb = new SolidBrush(s.Text);
            g.DrawString(detail, sf, tb, barX + 2, barY + 1);
        }
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
        f.Size = new Size(520, 600);
        f.MinimumSize = new Size(420, 320);
        f.StartPosition = FormStartPosition.CenterParent;
        f.Font = Font;

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = f.Height - 54, IsSplitterFixed = true };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(16) };
        var bottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        var btnClose = new Button { Text = "Schliessen", Dock = DockStyle.Right, Width = 120, Height = 34 };
        StyleButton(btnClose);
        btnClose.Click += (_, _) => f.Close();
        bottom.Controls.Add(btnClose);

        if (das.Count == 0)
        {
            flow.Controls.Add(new Label { Text = "Keine Einträge an diesem Tag.", ForeColor = SystemColors.GrayText, AutoSize = true });
        }
        else
        {
            var sites = das.Select(a => a.Site ?? _sites.FirstOrDefault(s => s.Id == a.ConstructionSiteId))
                           .OfType<ConstructionSite>().DistinctBy(s => s.Id).OrderBy(s => s.Name).ToList();

            foreach (var site in sites)
            {
                flow.Controls.Add(new Label
                {
                    Text = site.Name,
                    Font = new Font(Font, FontStyle.Bold | FontStyle.Underline),
                    ForeColor = Color.FromArgb(0x1B, 0x5E, 0x20),
                    AutoSize = true,
                    Margin = new Padding(0, 12, 0, 4)
                });

                var siteEntries = BuildDayEntries(_monthAssignments, day, site.Id).OrderBy(e => e.TeamId).ToList();

                foreach (var entry in siteEntries)
                {
                    var captured = entry;
                    var spanTxt = entry.MultiDay ? $"  ({entry.From:dd.MM.}–{entry.To:dd.MM.})" : "";

                    // Team (löschen cascaded Fahrzeug + Baustelle über den Zeitraum)
                    if (captured.TeamId.HasValue)
                    {
                        var team = _teams.FirstOrDefault(t => t.Id == captured.TeamId.Value);
                        flow.Controls.Add(MakeDeletableLine(flow.Width - 20, $"Team: {team?.Name ?? "Team"}{spanTxt}",
                            () => DeleteTeamCascade(captured, day, f), "Team löschen (Fahrzeug & Baustelle ebenfalls)"));
                    }

                    // Fahrzeug (löschen oder ersetzen)
                    if (captured.VehicleId.HasValue)
                    {
                        var vehicle = _vehicles.FirstOrDefault(v => v.Id == captured.VehicleId.Value);
                        flow.Controls.Add(MakeDeletableLine(flow.Width - 20, $"Fahrzeug: {vehicle?.VehicleNumber ?? "Fahrzeug"}{spanTxt}",
                            () => DeleteVehicle(captured, day, f), "Fahrzeug löschen oder ersetzen"));
                    }

                    // Mitarbeiter (einzeln löschbar)
                    var empRows = captured.Assignments.Where(a => a.EmployeeId.HasValue)
                                        .GroupBy(a => a.EmployeeId!.Value)
                                        .Select(g => g.First())
                                        .OrderBy(a => a.Employee?.FullName)
                                        .ToList();
                    foreach (var er in empRows)
                    {
                        var emp = er.Employee ?? _employees.FirstOrDefault(e => e.Id == er.EmployeeId!.Value);
                        flow.Controls.Add(MakeDeletableLine(flow.Width - 20, $"Mitarbeiter: {emp?.FullName ?? "MA"}",
                            () => DeleteEmployeeRow(captured, er, day, f), "Mitarbeiter löschen"));
                    }
                }
            }
        }

        // Vacation / Sickness for this day (deletable)
        var dayVac = _vacations.Where(v => day >= v.StartDate.Date && day <= v.EndDate.Date).OrderBy(v => v.Employee?.FullName).ToList();
        var daySick = _sickness.Where(s => day >= s.StartDate.Date && day <= s.EndDate.Date).OrderBy(s => s.Employee?.FullName).ToList();

        if (dayVac.Count > 0)
        {
            flow.Controls.Add(new Label
            {
                Text = "Urlaub",
                Font = new Font(Font, FontStyle.Bold | FontStyle.Underline),
                ForeColor = Color.FromArgb(0x2E, 0x7D, 0x32),
                AutoSize = true,
                Margin = new Padding(0, 12, 0, 4)
            });
            foreach (var v in dayVac)
            {
                var name = v.Employee?.FullName ?? $"MA {v.EmployeeId}";
                var suffix = v.StartDate.Date == v.EndDate.Date ? "" : $"  ({v.StartDate:dd.MM.}–{v.EndDate:dd.MM.})";
                var vid = v.Id;
                flow.Controls.Add(MakeDeletableLine(flow.Width - 20, $"Urlaub: {name}{suffix}", () =>
                {
                    if (MessageBox.Show($"Urlaub löschen?\n{name}{suffix}", "Urlaub löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        _db.DeleteVacation(vid);
                        RefreshAllData();
                        ShowDayOverview(day);
                    }
                }));
            }
        }

        if (daySick.Count > 0)
        {
            flow.Controls.Add(new Label
            {
                Text = "Krankheit",
                Font = new Font(Font, FontStyle.Bold | FontStyle.Underline),
                ForeColor = Color.FromArgb(0xC6, 0x28, 0x28),
                AutoSize = true,
                Margin = new Padding(0, 12, 0, 4)
            });
            foreach (var s in daySick)
            {
                var name = s.Employee?.FullName ?? $"MA {s.EmployeeId}";
                var suffix = s.StartDate.Date == s.EndDate.Date ? "" : $"  ({s.StartDate:dd.MM.}–{s.EndDate:dd.MM.})";
                var sid = s.Id;
                flow.Controls.Add(MakeDeletableLine(flow.Width - 20, $"Krank: {name}{suffix}", () =>
                {
                    if (MessageBox.Show($"Krankheit löschen?\n{name}{suffix}", "Krankheit löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        _db.DeleteSickness(sid);
                        RefreshAllData();
                        ShowDayOverview(day);
                    }
                }));
            }
        }

        split.Panel1.Controls.Add(flow);
        split.Panel2.Controls.Add(bottom);
        f.Controls.Add(split);
        f.ShowDialog();
    }

    private Panel MakeDeletableLine(int parentWidth, string text, Action onDelete, string? tooltip = null)
    {
        var innerWidth = parentWidth - 44;
        // Measure the wrapped text height so the panel grows to fit the full text (no clipping).
        var measure = new Label { AutoSize = true, MaximumSize = new Size(innerWidth - 12, 0), Text = text, Font = Font };
        var textH = measure.GetPreferredSize(Size.Empty).Height;
        var height = Math.Max(28, textH + 12);
        var line = new Panel { Width = parentWidth - 44, Height = height, Margin = new Padding(8, 0, 0, 4), BorderStyle = BorderStyle.FixedSingle, BackColor = SystemColors.Window };
        var lbl = new Label { Text = text, Location = new Point(6, 6), AutoSize = true, MaximumSize = new Size(innerWidth - 12, 0), Font = Font };
        var btnX = new Button { Text = "X", Width = 28, Height = 24, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0xF4, 0x43, 0x36), ForeColor = Color.White, Cursor = Cursors.Hand };
        StyleButton(btnX);
        btnX.FlatAppearance.BorderSize = 0;
        btnX.Location = new Point(line.Width - 28 - 2, (height - 24) / 2);
        btnX.Click += (_, _) => onDelete();
        line.Controls.Add(lbl);
        line.Controls.Add(btnX);
        if (tooltip != null)
        {
            var tip = new ToolTip();
            tip.SetToolTip(btnX, tooltip);
            tip.SetToolTip(lbl, text);
        }
        return line;
    }

    private static void RelayoutCrudRows(FlowLayoutPanel flow)
    {
        foreach (Control c in flow.Controls)
        {
            if (c is not Panel line) continue;
            line.Width = Math.Max(180, flow.ClientSize.Width - 16);
            if (line.Controls[0] is Label lbl) lbl.MaximumSize = new Size(line.Width - 40, 0);
            if (line.Controls[^1] is Button x) x.Location = new Point(line.Width - 26 - 2, 4);
        }
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

    private List<AssignmentEntry> BuildDayEntries(List<Assignment> allAssignments, DateTime day, int? siteId = null)
    {
        var groups = allAssignments
                        .Where(a => siteId == null || a.ConstructionSiteId == siteId)
                        .Where(a => a.Date.Date == day.Date)
                        // Group by (site, team, vehicle): employees belong to the team/vehicle
                        // assignment and are listed individually as deletable sub-rows.
                        .GroupBy(a => (a.ConstructionSiteId, a.TeamId, a.VehicleId))
                        .Select(g =>
                        {
                            var key = g.Key;
                            var all = allAssignments
                                .Where(a => a.ConstructionSiteId == key.ConstructionSiteId
                                         && a.TeamId == key.TeamId
                                         && a.VehicleId == key.VehicleId)
                                .ToList();
                            var dates = all.Select(a => a.Date.Date).Distinct().OrderBy(d => d).ToList();
                            var min = dates.Min();
                            var max = dates.Max();
                            var a0 = all.First();
                            var site = a0.Site ?? _sites.FirstOrDefault(s => s.Id == a0.ConstructionSiteId);
                            var team = a0.TeamId.HasValue ? _teams.FirstOrDefault(t => t.Id == a0.TeamId.Value) : null;
                            var vehicle = a0.Vehicle ?? (a0.VehicleId.HasValue ? _vehicles.FirstOrDefault(v => v.Id == a0.VehicleId.Value) : null);

                            var parts = new List<string>();
                            if (team != null) parts.Add(team.Name);
                            if (vehicle != null) parts.Add(vehicle.VehicleNumber);
                            var label = parts.Count > 0 ? string.Join("\n", parts) : (site?.Name ?? "Eintrag");

                            return new AssignmentEntry
                            {
                                ConstructionSiteId = a0.ConstructionSiteId,
                                TeamId = a0.TeamId,
                                VehicleId = a0.VehicleId,
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

    // Asks whether to delete the whole span or only the opened day.
    // Returns the days to delete, or null if cancelled.
    private List<DateTime>? AskSpan(AssignmentEntry entry, DateTime day, string title)
    {
        if (!entry.MultiDay)
        {
            if (MessageBox.Show($"{title}?\n{entry.Label.Replace("\n", "  ")}\n{entry.From:dd.MM.yyyy}", title,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return null;
            return new List<DateTime> { day.Date };
        }

        using var dlg = new Form();
        dlg.Text = title;
        dlg.Size = new Size(460, 250);
        dlg.StartPosition = FormStartPosition.CenterParent;
        dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
        dlg.MaximizeBox = dlg.MinimizeBox = false;
        dlg.Font = Font;
        dlg.Controls.Add(new Label { Text = $"{entry.Label.Replace("\n", "  ")}\n{entry.From:dd.MM.yyyy} – {entry.To:dd.MM.yyyy}", Location = new Point(16, 16), AutoSize = true });
        dlg.Controls.Add(new Label { Text = "Wie soll gelöscht werden?", Location = new Point(16, 56), AutoSize = true });
        var btnWhole = new Button { Text = "Ganzer Termin", Location = new Point(16, 110), Width = 195, Height = 38, BackColor = Color.FromArgb(0xF4, 0x43, 0x36), ForeColor = Color.White };
        StyleButton(btnWhole);
        var btnDay = new Button { Text = "Nur an diesem Tag", Location = new Point(229, 110), Width = 195, Height = 38 };
        StyleButton(btnDay);
        var result = 0;
        btnWhole.Click += (_, _) => { result = 1; dlg.Close(); };
        btnDay.Click += (_, _) => { result = 2; dlg.Close(); };
        dlg.Controls.AddRange(new Control[] { btnWhole, btnDay });
        dlg.ShowDialog();

        if (result == 1)
            return entry.Assignments.Select(a => a.Date.Date).Distinct().ToList();
        if (result == 2)
            return new List<DateTime> { day.Date };
        return null;
    }

    // Team löschen -> über denselben Zeitraum auch Fahrzeug und Baustelle entfernen.
    private void DeleteTeamCascade(AssignmentEntry entry, DateTime day, Form owner)
    {
        var days = AskSpan(entry, day, "Team löschen");
        if (days == null) return;

        // Alle Zeilen dieses (Baustelle, Team, Fahrzeug)-Zusammenhangs im Zeitraum entfernen.
        // Dadurch verschwinden Team, Fahrzeug und Baustelle gemeinsam (Fahrzeug/Team ohne
        // einander bzw. Baustelle ohne Fahrzeug+Team sind nicht sinnvoll).
        foreach (var a in entry.Assignments.Where(a => days.Contains(a.Date.Date)))
            _db.DeleteAssignment(a.Id);

        RefreshAllData();
        owner.Close();
    }

    // Fahrzeug löschen oder durch ein anderes (freies) Fahrzeug ersetzen.
    private void DeleteVehicle(AssignmentEntry entry, DateTime day, Form owner)
    {
        var days = AskSpan(entry, day, "Fahrzeug löschen");
        if (days == null) return;

        var choice = MessageBox.Show(
            $"Fahrzeug wirklich löschen?\n\nJa = Fahrzeug entfernen\nNein = anderes Fahrzeug zuweisen",
            "Fahrzeug", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (choice == DialogResult.Cancel) return;

        if (choice == DialogResult.No)
        {
            // Ersatz-Fahrzeug wählen, das an keinem der Tage schon anderswo zugewiesen ist.
            var rows = entry.Assignments.Where(a => days.Contains(a.Date.Date)).ToList();
            var candidates = _vehicles.Where(v => days.All(d => !_db.IsVehicleAssigned(v.Id, d,
                excludeIds: rows.Select(r => r.Id).ToList()))).ToList();
            if (candidates.Count == 0)
            {
                MessageBox.Show("Kein freies Fahrzeug verfügbar (alle sind an einem der Tage bereits zugewiesen).",
                    "Fahrzeug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var f = new Form();
            f.Text = "Ersatz-Fahrzeug wählen";
            f.Size = new Size(420, 300);
            f.StartPosition = FormStartPosition.CenterParent;
            f.Font = Font;
            var lb = new ListBox { Dock = DockStyle.Fill, DataSource = candidates, DisplayMember = "ToString" };
            var btn = new Button { Text = "Übernehmen", Dock = DockStyle.Bottom };
            StyleButton(btn);
            Vehicle? sel = null;
            btn.Click += (_, _) => { sel = lb.SelectedItem as Vehicle; f.Close(); };
            f.Controls.Add(lb); f.Controls.Add(btn);
            f.ShowDialog();
            if (sel == null) return;
            foreach (var r in rows) { r.VehicleId = sel.Id; _db.SaveAssignment(r); }
        }
        else
        {
            // Fahrzeug aus den Zeilen entfernen (Baustelle/Team bleiben erhalten).
            foreach (var a in entry.Assignments.Where(a => days.Contains(a.Date.Date)))
            {
                a.VehicleId = null;
                _db.SaveAssignment(a);
            }
        }

        RefreshAllData();
        owner.Close();
    }

    // Einzelnen Mitarbeiter aus dem Termin entfernen.
    private static bool CanDrive(Employee? e, Vehicle? v)
    {
        if (e == null || v == null) return false;
        if (string.IsNullOrEmpty(v.RequiredLicense)) return true; // kein Führerschein nötig
        return e.HasDriversLicense && e.GetLicenseList().Contains(v.RequiredLicense);
    }

    private void DeleteEmployeeRow(AssignmentEntry entry, Assignment row, DateTime day, Form owner)
    {
        var emp = row.Employee ?? _employees.FirstOrDefault(e => e.Id == row.EmployeeId);
        var name = emp?.FullName ?? "Mitarbeiter";

        if (MessageBox.Show($"Mitarbeiter löschen?\n{name}\n{row.Date:dd.MM.yyyy}", "Mitarbeiter löschen",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        // Prüfen, ob danach im verknüpften Termin (selbe Baustelle/Team/Fahrzeug) noch
        // jemand übrig ist, der den erforderlichen Führerschein für das Fahrzeug hat.
        var vehicle = entry.VehicleId.HasValue ? _vehicles.FirstOrDefault(v => v.Id == entry.VehicleId.Value) : null;
        bool needsDriverCheck = vehicle != null && !string.IsNullOrEmpty(vehicle.RequiredLicense);
        if (needsDriverCheck)
        {
            var remainingEmployees = entry.Assignments
                .Where(a => a.EmployeeId.HasValue && a.EmployeeId != row.EmployeeId)
                .Select(a => a.Employee ?? _employees.FirstOrDefault(e => e.Id == a.EmployeeId!.Value))
                .Where(e => e != null)
                .Distinct()
                .ToList();
            var stillDrivable = remainingEmployees.Any(e => CanDrive(e, vehicle));

            if (!stillDrivable)
            {
                var res = ShowNoDriverDialog(vehicle, entry, name);
                switch (res)
                {
                    case DialogResult.Cancel:   // Nicht löschen
                        return;
                    case DialogResult.Abort:    // Ganzen Termin löschen
                        foreach (var a in entry.Assignments)
                            _db.DeleteAssignment(a.Id);
                        RefreshAllData();
                        owner.Close();
                        return;
                    case DialogResult.Retry:    // Anderes Fahrzeug wählen
                        _db.DeleteAssignment(row.Id);
                        ReplaceVehicleInEntry(entry, owner);
                        return;
                    // OK / sonst: nur diese Person löschen (wie gewählt)
                    default:
                        break;
                }
            }
        }

        _db.DeleteAssignment(row.Id);
        RefreshAllData();
        owner.Close();
    }

    // Letzte befähigte Person wird gelöscht -> Auswahl anbieten.
    private DialogResult ShowNoDriverDialog(Vehicle vehicle, AssignmentEntry entry, string name)
    {
        using var dlg = new Form();
        dlg.Text = "Kein Fahrer mehr für Fahrzeug";
        dlg.Size = new Size(480, 320);
        dlg.StartPosition = FormStartPosition.CenterParent;
        dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
        dlg.MaximizeBox = dlg.MinimizeBox = false;
        dlg.Font = Font;
        dlg.Controls.Add(new Label
        {
            Text = $"{name} war die letzte Person mit Führerschein '{vehicle.RequiredLicense}'\n" +
                   $"für Fahrzeug {vehicle.VehicleNumber} in diesem Termin.\n\nWie soll verfahren werden?",
            Location = new Point(16, 16), AutoSize = true
        });

        var btnKeep = new Button { Text = "Nicht löschen", Location = new Point(16, 130), Width = 210, Height = 40 };
        var btnDelete = new Button { Text = "Löschen", Location = new Point(244, 130), Width = 210, Height = 40 };
        var btnReplace = new Button { Text = "Anderes Fahrzeug wählen", Location = new Point(16, 184), Width = 210, Height = 40 };
        var btnWhole = new Button { Text = "Ganzen Termin löschen", Location = new Point(244, 184), Width = 210, Height = 40, BackColor = Color.FromArgb(0xF4, 0x43, 0x36), ForeColor = Color.White };
        StyleButton(btnKeep); StyleButton(btnDelete); StyleButton(btnReplace); StyleButton(btnWhole);

        var result = DialogResult.Cancel;
        btnKeep.Click += (_, _) => { result = DialogResult.Cancel; dlg.Close(); };
        btnDelete.Click += (_, _) => { result = DialogResult.OK; dlg.Close(); };
        btnReplace.Click += (_, _) => { result = DialogResult.Retry; dlg.Close(); };
        btnWhole.Click += (_, _) => { result = DialogResult.Abort; dlg.Close(); };
        dlg.Controls.AddRange(new Control[] { btnKeep, btnDelete, btnReplace, btnWhole });
        dlg.ShowDialog();
        return result;
    }

    // Weist dem verknüpften Termin ein anderes, freies Fahrzeug zu (kein Konflikt an den Tagen).
    private void ReplaceVehicleInEntry(AssignmentEntry entry, Form owner)
    {
        var days = entry.Assignments.Select(a => a.Date.Date).Distinct().ToList();
        var rows = entry.Assignments.ToList();
        var candidates = _vehicles.Where(v => days.All(d => !_db.IsVehicleAssigned(v.Id, d, rows.Select(r => r.Id).ToList()))).ToList();
        if (candidates.Count == 0)
        {
            MessageBox.Show("Kein freies Fahrzeug verfügbar – Termin bleibt ohne Fahrzeug.", "Fahrzeug", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshAllData();
            owner.Close();
            return;
        }
        using var f = new Form();
        f.Text = "Ersatz-Fahrzeug wählen";
        f.Size = new Size(420, 300);
        f.StartPosition = FormStartPosition.CenterParent;
        f.Font = Font;
        var lb = new ListBox { Dock = DockStyle.Fill, DataSource = candidates, DisplayMember = "ToString" };
        var btn = new Button { Text = "Übernehmen", Dock = DockStyle.Bottom };
        StyleButton(btn);
        Vehicle? sel = null;
        btn.Click += (_, _) => { sel = lb.SelectedItem as Vehicle; f.Close(); };
        f.Controls.Add(lb); f.Controls.Add(btn);
        f.ShowDialog();
        if (sel != null)
            foreach (var r in rows) { r.VehicleId = sel.Id; _db.SaveAssignment(r); }
        RefreshAllData();
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
        // Team already has a preferred vehicle -> assign it silently, no prompt.
        if (team.PreferredVehicleId.HasValue)
        {
            var pref = _vehicles.FirstOrDefault(v => v.Id == team.PreferredVehicleId.Value);
            if (pref != null)
            {
                for (var d = from; d <= until; d = d.AddDays(1))
                    if (!_db.IsVehicleAssigned(pref.Id, d))
                        _db.SaveAssignment(new Assignment { ConstructionSiteId = siteId, VehicleId = pref.Id, Date = d });
                return;
            }
        }

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
            team.PreferredVehicleId = sel.Id;
            _db.SaveTeam(team);
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
        EnterAbsence(emp, from, until, "Urlaub", isVacation: true);
    }

    private void AddSickness(DateTime from, DateTime until)
    {
        var emp = SelectEmployee();
        if (emp == null) return;
        EnterAbsence(emp, from, until, "Krankheit", isVacation: false);
    }

    // Urlaub/Krankheit eintragen und vorher prüfen, ob der Mitarbeiter in diesem Zeitraum
    // tatsächlich eingeteilt ist. Nur bei ÜBERSCHNEIDUNG nachfragen; sonst einfach eintragen.
    private void EnterAbsence(Employee emp, DateTime from, DateTime until, string kind, bool isVacation)
    {
        var conflictDays = new List<DateTime>();
        for (var d = from.Date; d <= until.Date; d = d.AddDays(1))
            if (_db.IsEmployeeAssigned(emp.Id, d))
                conflictDays.Add(d);

        if (conflictDays.Count > 0)
        {
            var daysTxt = string.Join("\n", conflictDays.OrderBy(d => d).Select(d => $"  • {d:ddd dd.MM.yyyy}"));
            var msg = $"{emp.FullName} ist im Zeitraum {from:dd.MM.yyyy} – {until:dd.MM.yyyy} bereits eingeteilt:\n\n" +
                      $"{daysTxt}\n\n" +
                      $"Wie soll verfahren werden?\n" +
                      $"Ja = {kind} NICHT eintragen\n" +
                      $"Nein = {kind} eintragen und Mitarbeiter aus dem/den Team(s) entfernen";
            var res = MessageBox.Show(msg, $"{kind} mit Terminkonflikt", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (res == DialogResult.Yes) return;        // nicht eintragen
            if (res == DialogResult.Cancel) return;      // abbrechen
            // Nein -> eintragen und aus Teams entfernen
        }

        if (isVacation)
        {
            _db.SaveVacation(new Vacation { EmployeeId = emp.Id, StartDate = from, EndDate = until });
            _vacations = _db.GetAllVacations();
        }
        else
        {
            _db.SaveSickness(new Sickness { EmployeeId = emp.Id, StartDate = from, EndDate = until });
            _sickness = _db.GetAllSickness();
        }

        // Bei Konflikt: Mitarbeiter aus allen Teams entfernen (nur die Teams, in denen er ist).
        if (conflictDays.Count > 0)
        {
            var affected = _teams.Where(t => t.Members.Any(m => m.Id == emp.Id)).ToList();
            foreach (var team in affected)
            {
                team.Members.RemoveAll(m => m.Id == emp.Id);
                _db.SaveTeam(team);
            }
            MessageBox.Show($"{kind} für {emp.FullName} eingetragen.\nMitarbeiter aus {affected.Count} Team(s) entfernt.", kind);
        }
        else
        {
            MessageBox.Show($"{kind} für {emp.FullName} von {from:dd.MM.yyyy} bis {until:dd.MM.yyyy} eingetragen.", kind);
        }

        RefreshAllData();
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
        var idx = _lbEmployees.IndexFromPoint(e.Location);
        if (idx < 0 || idx >= _lbEmployees.Items.Count) return;
        if (_lbEmployees.Items[idx] is Employee dragEmp && e.Button == MouseButtons.Left)
        {
            _lbEmployees.SelectedIndex = idx;
            _lbEmployees.DoDragDrop(dragEmp, DragDropEffects.Move);
        }
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
    private void OpenHelp()
    {
        // Die Hilfe-Datei liegt neben der ausführbaren Datei (HILFE.md).
        var exeDir = AppContext.BaseDirectory;
        var path = Path.Combine(exeDir, "HILFE.md");
        if (!File.Exists(path))
        {
            MessageBox.Show($"Hilfe-Datei nicht gefunden:\n{path}", "Hilfe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show($"Hilfe konnte nicht geöffnet werden:\n{ex.Message}", "Hilfe", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    // ====== BACKUP ======
    private async Task DoBackup()
    {
        if (await _backup.BackupToDrive())
            MessageBox.Show("Google Drive Backup erfolgreich!", "Backup");
        else
        {
            var dbPath = _db.DbPath;
            if (!File.Exists(dbPath))
            {
                MessageBox.Show($"Datenbank nicht gefunden:\n{dbPath}", "Backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var bp = Path.Combine(Path.GetDirectoryName(dbPath)!, $"kofplanner_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            try { File.Copy(dbPath, bp); MessageBox.Show($"Lokales Backup erstellt:\n{bp}", "Backup"); }
            catch (Exception ex) { MessageBox.Show($"Fehler: {ex.Message}"); }
        }
    }

    private void RestoreDatabase()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Datenbank (*.db)|*.db|Alle Dateien (*.*)|*.*",
            Title = "Datenbank wiederherstellen"
        };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        var dbPath = _db.DbPath;
        if (MessageBox.Show($"Die aktuelle Datenbank wird durch\n{Path.GetFileName(ofd.FileName)}\nersetzt. Ein Backup der aktuellen Datei wird zuvor erstellt.\n\nFortfahren?",
                "Datenbank wiederherstellen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            if (File.Exists(dbPath))
            {
                var bak = Path.Combine(Path.GetDirectoryName(dbPath)!, $"kofplanner_backup_vor_restore_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                File.Copy(dbPath, bak, true);
            }
            File.Copy(ofd.FileName, dbPath, true);
            MessageBox.Show("Datenbank wiederhergestellt. Die Anwendung wird neu geladen.", "Wiederhergestellt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Restart();
        }
        catch (Exception ex) { MessageBox.Show($"Fehler beim Wiederherstellen: {ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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

