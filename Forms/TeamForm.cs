using KOFplanner.Models;
using KOFplanner.Services;

namespace KOFplanner.Forms;

public class TeamForm : Form
{
    private readonly DatabaseService _db;
    private readonly Team _team;
    private readonly List<Employee> _allEmployees;
    private readonly List<Vehicle> _vehicles;
    private readonly Func<Team, Action, bool>? _ensureDriver;

    private readonly TextBox _txtName;
    private readonly Button _btnColor;
    private ListBox _lbAvailable = null!;
    private ListBox _lbMembers = null!;
    private ListView _lvVehicles = null!;
    private Color _selectedColor;

    public TeamForm(DatabaseService db, Team? team, List<Employee> allEmployees)
        : this(db, team, allEmployees, new List<Vehicle>(), null) { }

    public TeamForm(DatabaseService db, Team? team, List<Employee> allEmployees, List<Vehicle> vehicles)
        : this(db, team, allEmployees, vehicles, null) { }

    public TeamForm(DatabaseService db, Team? team, List<Employee> allEmployees, List<Vehicle> vehicles, Func<Team, Action, bool>? ensureDriver)
    {
        _db = db;
        _team = team ?? new Team { Name = "" };
        _allEmployees = allEmployees;
        _vehicles = vehicles;
        _ensureDriver = ensureDriver;
        _selectedColor = _team.Color;

        Text = team == null ? "Neues Team" : "Team bearbeiten";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(720, 540);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(12)
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        // ---- Row 0: Name + Farbe ----
        var namePanel = new Panel { Dock = DockStyle.Fill };
        namePanel.Controls.Add(new Label { Text = "Teamname:", Left = 0, Top = 0, AutoSize = true });
        _txtName = new TextBox { Left = 0, Top = 20, Width = namePanel.Width - 4, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Text = _team.Name };
        namePanel.Resize += (_, _) => _txtName.Width = namePanel.Width - 4;
        namePanel.Controls.Add(_txtName);
        tlp.Controls.Add(namePanel, 0, 0);

        var colorPanel = new Panel { Dock = DockStyle.Fill };
        colorPanel.Controls.Add(new Label { Text = "Teamfarbe:", Left = 0, Top = 0, AutoSize = true });
        _btnColor = new Button { Left = 0, Top = 20, Width = 120, Height = 30, BackColor = _selectedColor, Text = "" };
        _btnColor.Click += (_, _) =>
        {
            using var dlg = new ColorDialog { Color = _selectedColor, FullOpen = true };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _selectedColor = dlg.Color;
                _btnColor.BackColor = _selectedColor;
            }
        };
        colorPanel.Controls.Add(_btnColor);
        tlp.Controls.Add(colorPanel, 1, 0);

        // ---- Row 1: Mitarbeiter ----
        var memberPanel = BuildMemberPanel();
        tlp.Controls.Add(memberPanel, 0, 1);
        tlp.SetRowSpan(memberPanel, 2);

        // ---- Row 1-2: Fahrzeuge ----
        var vehiclePanel = BuildVehiclePanel();
        tlp.Controls.Add(vehiclePanel, 1, 1);
        tlp.SetRowSpan(vehiclePanel, 2);

        // ---- Row 3: OK / Abbrechen ----
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnCancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Width = 100, Height = 30 };
        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 100, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0x2E, 0x7D, 0x32), ForeColor = Color.White, Cursor = Cursors.Hand };
        btnOk.Click += (_, _) => Save();
        bottom.Controls.AddRange(new Control[] { btnCancel, btnOk });
        tlp.Controls.Add(bottom, 0, 3);
        tlp.SetColumnSpan(bottom, 2);

        Controls.Add(tlp);
        CancelButton = btnCancel;
    }

    private Panel BuildMemberPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 6, 0) };
        panel.Controls.Add(new Label { Text = "Mitarbeiter", Dock = DockStyle.Top, Font = new Font(Font, FontStyle.Bold) });

        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 24, 0, 0) };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));

        var availPanel = new Panel { Dock = DockStyle.Fill };
        availPanel.Controls.Add(new Label { Text = "Verfügbar", Dock = DockStyle.Top, Font = new Font(Font, FontStyle.Italic) });
        _lbAvailable = new ListBox { Dock = DockStyle.Fill, DisplayMember = "FullName", AllowDrop = true };
        WireEmployeeDragDrop(_lbAvailable);
        availPanel.Controls.Add(_lbAvailable);
        split.Controls.Add(availPanel, 0, 0);

        var mid = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(2, 30, 2, 0) };
        var btnAdd = new Button { Text = ">>", Width = 38, Height = 28 };
        btnAdd.Click += (_, _) => MoveSelected(_lbAvailable, _lbMembers);
        var btnRemove = new Button { Text = "<<", Width = 38, Height = 28 };
        btnRemove.Click += (_, _) => MoveSelected(_lbMembers, _lbAvailable);
        mid.Controls.Add(btnAdd);
        mid.Controls.Add(btnRemove);
        split.Controls.Add(mid, 1, 0);

        var memPanel = new Panel { Dock = DockStyle.Fill };
        memPanel.Controls.Add(new Label { Text = "Teammitglieder", Dock = DockStyle.Top, Font = new Font(Font, FontStyle.Italic) });
        _lbMembers = new ListBox { Dock = DockStyle.Fill, DisplayMember = "FullName", AllowDrop = true };
        WireEmployeeDragDrop(_lbMembers);
        memPanel.Controls.Add(_lbMembers);
        split.Controls.Add(memPanel, 2, 0);

        panel.Controls.Add(split);
        return panel;
    }

    private Panel BuildVehiclePanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 0, 0, 0) };
        panel.Controls.Add(new Label { Text = "Fahrzeug (Teamfahrzeug)", Dock = DockStyle.Top, Font = new Font(Font, FontStyle.Bold) });

        _lvVehicles = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = false, HeaderStyle = ColumnHeaderStyle.Nonclickable, Top = 24, Height = 0 };
        _lvVehicles.Columns.Add("Fahrzeugnummer", 110);
        _lvVehicles.Columns.Add("Eigenschaft", 110);
        _lvVehicles.Columns.Add("Kennzeichen", 110);
        _lvVehicles.Resize += (_, _) =>
        {
            var last = _lvVehicles.Columns[_lvVehicles.Columns.Count - 1];
            var used = 0;
            for (var i = 0; i < _lvVehicles.Columns.Count - 1; i++) used += _lvVehicles.Columns[i].Width;
            last.Width = Math.Max(60, _lvVehicles.ClientSize.Width - used - 4);
        };
        RefreshVehicleList();
        panel.Controls.Add(_lvVehicles);

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.LeftToRight, Height = 34 };
        var btnAssign = new Button { Text = "Als Teamfahrzeug setzen", Width = 170, Height = 28 };
        btnAssign.Click += (_, _) =>
        {
            if (_lvVehicles.SelectedItems.Count > 0 && _lvVehicles.SelectedItems[0].Tag is Vehicle v)
                MarkPreferred(v);
        };
        var btnClear = new Button { Text = "Fahrzeug entfernen", Width = 140, Height = 28 };
        btnClear.Click += (_, _) => MarkPreferred(null);
        btnPanel.Controls.AddRange(new Control[] { btnAssign, btnClear });
        panel.Controls.Add(btnPanel);

        return panel;
    }

    private void RefreshVehicleList()
    {
        _lvVehicles.Items.Clear();
        foreach (var v in _vehicles.OrderBy(v => v.VehicleNumber, StringComparer.CurrentCultureIgnoreCase))
        {
            var item = new ListViewItem(new[] { v.VehicleNumber, v.RequiredLicense, v.LicensePlate }) { Tag = v };
            if (_team.PreferredVehicleId.HasValue && v.Id == _team.PreferredVehicleId.Value)
                item.BackColor = Color.FromArgb(0xD8, 0xF0, 0xDC);
            _lvVehicles.Items.Add(item);
        }
        var last = _lvVehicles.Columns[_lvVehicles.Columns.Count - 1];
        var used = 0;
        for (var i = 0; i < _lvVehicles.Columns.Count - 1; i++) used += _lvVehicles.Columns[i].Width;
        last.Width = Math.Max(60, _lvVehicles.ClientSize.Width - used - 4);
    }

    private void MarkPreferred(Vehicle? v)
    {
        _team.PreferredVehicleId = v?.Id;
        RefreshVehicleList();
    }

    private void WireEmployeeDragDrop(ListBox lb)
    {
        lb.MouseDown += (_, e) =>
        {
            var idx = lb.IndexFromPoint(e.Location);
            if (idx >= 0 && e.Button == MouseButtons.Left)
            {
                lb.SelectedIndex = idx;
                if (lb.Items[idx] is Employee emp)
                    lb.DoDragDrop(emp, DragDropEffects.Move);
            }
        };
        lb.DragEnter += (_, e) => { if (e.Data!.GetDataPresent(typeof(Employee))) e.Effect = DragDropEffects.Move; };
        lb.DragDrop += (_, e) =>
        {
            if (e.Data!.GetData(typeof(Employee)) is Employee emp)
            {
                if (lb == _lbMembers && !_lbMembers.Items.Contains(emp))
                    _lbMembers.Items.Add(emp);
                else if (lb == _lbAvailable && !_lbAvailable.Items.Contains(emp))
                    _lbAvailable.Items.Add(emp);
                _lbAvailable.Items.Remove(emp);
                _lbMembers.Items.Remove(emp);
                if (lb == _lbMembers) _lbMembers.Items.Add(emp);
                else _lbAvailable.Items.Add(emp);
            }
        };
    }

    private static void MoveSelected(ListBox from, ListBox to)
    {
        foreach (var item in from.SelectedItems.Cast<object>().ToList())
        {
            from.Items.Remove(item);
            to.Items.Add(item);
        }
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("Bitte Teamnamen eingeben.");
            DialogResult = DialogResult.None;
            return;
        }

        var originalMembers = _team.Members.ToList();
        var originalVehicle = _team.PreferredVehicleId;

        _team.Name = _txtName.Text.Trim();
        _team.ColorArgb = _selectedColor.ToArgb();
        _team.Members = _lbMembers.Items.Cast<Employee>().ToList();

        if (_ensureDriver != null)
        {
            var ok = _ensureDriver(_team, () =>
            {
                _team.Members = originalMembers;
                _team.PreferredVehicleId = originalVehicle;
            });
            if (!ok)
            {
                _team.Members = originalMembers;
                _team.PreferredVehicleId = originalVehicle;
                DialogResult = DialogResult.None;
                return;
            }
        }
        _db.SaveTeam(_team);
    }
}
