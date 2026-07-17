using KOFplanner.Models;
using KOFplanner.Services;
using System;
using System.Linq;
using System.Windows.Forms;

namespace KOFplanner.Forms;

public class RecurringPatternForm : Form
{
    private readonly DatabaseService _db;
    private readonly RecurringPattern _pattern;
    private ComboBox _cmbSite = null!;
    private ComboBox _cmbTeam = null!;
    private ComboBox _cmbVehicle = null!;
    private ComboBox _cmbEmployee = null!;
    private CheckedListBox _clbDays = null!;
    private DateTimePicker _dtStart = null!;
    private DateTimePicker _dtEnd = null!;

    public RecurringPatternForm(DatabaseService db, RecurringPattern? pattern = null)
    {
        _db = db;
        _pattern = pattern ?? new RecurringPattern { StartDate = DateTime.Today };
        IconHelper.Apply(this);
        InitializeComponent();
        LoadLookups();
        if (pattern != null) FillForm();
    }

    private void InitializeComponent()
    {
        Text = _pattern.Id == 0 ? "Wiederkehrenden Einsatz anlegen" : "Wiederkehrenden Einsatz bearbeiten";
        Size = new System.Drawing.Size(420, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8, Padding = new Padding(10) };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _cmbSite = new ComboBox { Dock = DockStyle.Fill, DisplayMember = "Name", DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbTeam = new ComboBox { Dock = DockStyle.Fill, DisplayMember = "Name", DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbVehicle = new ComboBox { Dock = DockStyle.Fill, DisplayMember = "Name", DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbEmployee = new ComboBox { Dock = DockStyle.Fill, DisplayMember = "Name", DropDownStyle = ComboBoxStyle.DropDownList };
        _clbDays = new CheckedListBox { Dock = DockStyle.Fill };
        _dtStart = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short };
        _dtEnd = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short, ShowCheckBox = true };

        tlp.AddRow("Baustelle:", _cmbSite);
        tlp.AddRow("Team:", _cmbTeam);
        tlp.AddRow("Fahrzeug:", _cmbVehicle);
        tlp.AddRow("Mitarbeiter:", _cmbEmployee);
        tlp.AddRow("Wochentage:", _clbDays);
        tlp.AddRow("Start:", _dtStart);
        tlp.AddRow("Ende:", _dtEnd);

        var btnOk = new Button { Text = "Speichern", Dock = DockStyle.Fill, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Abbrechen", Dock = DockStyle.Fill, DialogResult = DialogResult.Cancel };
        btnOk.Click += BtnOk_Click;
        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Height = 30 };
        btnRow.Controls.Add(btnCancel);
        btnRow.Controls.Add(btnOk);
        tlp.Controls.Add(btnRow, 0, 7);
        tlp.SetColumnSpan(btnRow, 2);

        Controls.Add(tlp);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private void LoadLookups()
    {
        _cmbSite.Items.Add(new Lookup(0, "–"));
        foreach (var s in _db.GetAllSites()) _cmbSite.Items.Add(new Lookup(s.Id, s.Name));
        _cmbTeam.Items.Add(new Lookup(0, "–"));
        foreach (var t in _db.GetAllTeams()) _cmbTeam.Items.Add(new Lookup(t.Id, t.Name));
        _cmbVehicle.Items.Add(new Lookup(0, "–"));
        foreach (var v in _db.GetAllVehicles()) _cmbVehicle.Items.Add(new Lookup(v.Id, v.VehicleNumber));
        _cmbEmployee.Items.Add(new Lookup(0, "–"));
        foreach (var e in _db.GetAllEmployees()) _cmbEmployee.Items.Add(new Lookup(e.Id, e.FullName));

        foreach (DayOfWeek d in Enum.GetValues(typeof(DayOfWeek)))
            _clbDays.Items.Add(new DayName(d), false);
    }

    private void FillForm()
    {
        _cmbSite.SelectedItem = _cmbSite.Items.Cast<Lookup>().FirstOrDefault(l => l.Id == _pattern.ConstructionSiteId);
        _cmbTeam.SelectedItem = _cmbTeam.Items.Cast<Lookup>().FirstOrDefault(l => l.Id == (_pattern.TeamId ?? 0));
        _cmbVehicle.SelectedItem = _cmbVehicle.Items.Cast<Lookup>().FirstOrDefault(l => l.Id == (_pattern.VehicleId ?? 0));
        _cmbEmployee.SelectedItem = _cmbEmployee.Items.Cast<Lookup>().FirstOrDefault(l => l.Id == (_pattern.EmployeeId ?? 0));
        for (int i = 0; i < _clbDays.Items.Count; i++)
            _clbDays.SetItemChecked(i, _pattern.Weekdays.Contains(((DayName)_clbDays.Items[i]!).Day));
        _dtStart.Value = _pattern.StartDate;
        if (_pattern.EndDate.HasValue) { _dtEnd.Checked = true; _dtEnd.Value = _pattern.EndDate.Value; }
        else _dtEnd.Checked = false;
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        var site = (Lookup)_cmbSite.SelectedItem!;
        if (site.Id == 0) { MessageBox.Show("Bitte eine Baustelle wählen.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); DialogResult = DialogResult.None; return; }
        if (_clbDays.CheckedItems.Count == 0) { MessageBox.Show("Bitte mindestens einen Wochentag wählen.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information); DialogResult = DialogResult.None; return; }

        _pattern.ConstructionSiteId = site.Id;
        _pattern.TeamId = ((Lookup)_cmbTeam.SelectedItem!).Id == 0 ? null : ((Lookup)_cmbTeam.SelectedItem!).Id;
        _pattern.VehicleId = ((Lookup)_cmbVehicle.SelectedItem!).Id == 0 ? null : ((Lookup)_cmbVehicle.SelectedItem!).Id;
        _pattern.EmployeeId = ((Lookup)_cmbEmployee.SelectedItem!).Id == 0 ? null : ((Lookup)_cmbEmployee.SelectedItem!).Id;
        _pattern.Weekdays = _clbDays.CheckedItems.Cast<DayName>().Select(x => x.Day).ToHashSet();
        _pattern.StartDate = _dtStart.Value.Date;
        _pattern.EndDate = _dtEnd.Checked ? _dtEnd.Value.Date : null;
        _db.SaveRecurringPattern(_pattern);
    }
}

internal class Lookup
{
    public int Id { get; }
    public string Name { get; }
    public Lookup(int id, string name) { Id = id; Name = name; }
    public override string ToString() => Name;
}

internal class DayName
{
    public DayOfWeek Day { get; }
    public DayName(DayOfWeek d) { Day = d; }
    public override string ToString() => new System.Globalization.CultureInfo("de-AT").DateTimeFormat.DayNames[(int)Day];
}
