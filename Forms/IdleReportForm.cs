using KOFplanner.Models;
using KOFplanner.Services;
using System;
using System.Linq;
using System.Windows.Forms;

namespace KOFplanner.Forms;

// Zeigt Fahrzeuge und Mitarbeiter, die im gewaehlten Zeitraum KEINEN
// einzigen Einsatz haben (Leerlauf).
public class IdleReportForm : Form
{
    private readonly DatabaseService _db;
    private DateTimePicker _dtFrom = null!;
    private DateTimePicker _dtUntil = null!;
    private ListView _lvVehicle = null!;
    private ListView _lvEmployee = null!;

    public IdleReportForm(DatabaseService db)
    {
        _db = db;
        IconHelper.Apply(this);
        Text = "Leerlauf-Report";
        Size = new Size(640, 520);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);
        BuildUi();
        RefreshReport();
    }

    private void BuildUi()
    {
        _dtFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) };
        _dtUntil = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today };

        var filter = new FlowLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(8), AutoSize = true };
        filter.Controls.Add(new Label { Text = "Von:", AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft });
        filter.Controls.Add(_dtFrom);
        filter.Controls.Add(new Label { Text = "Bis:", AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft });
        filter.Controls.Add(_dtUntil);
        var btn = new Button { Text = "Aktualisieren", AutoSize = true };
        btn.Click += (_, _) => RefreshReport();
        filter.Controls.Add(btn);
        StyleButton(btn);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var tabV = new TabPage("Fahrzeuge ohne Einsatz");
        var tabE = new TabPage("Mitarbeiter ohne Einsatz");

        _lvVehicle = new ListView { Dock = DockStyle.Fill, View = View.Details, GridLines = true, FullRowSelect = true };
        _lvVehicle.Columns.Add("Fahrzeug", 160); _lvVehicle.Columns.Add("Kennzeichen", 140);
        tabV.Controls.Add(_lvVehicle);

        _lvEmployee = new ListView { Dock = DockStyle.Fill, View = View.Details, GridLines = true, FullRowSelect = true };
        _lvEmployee.Columns.Add("Mitarbeiter", 200); _lvEmployee.Columns.Add("Team", 160);
        tabE.Controls.Add(_lvEmployee);

        tabs.TabPages.Add(tabV); tabs.TabPages.Add(tabE);

        Controls.Add(tabs);
        Controls.Add(filter);
    }

    private void RefreshReport()
    {
        var from = _dtFrom.Value.Date;
        var until = _dtUntil.Value.Date;
        var assignments = _db.GetAllAssignments(from, until);
        var usedVehicleIds = assignments.Where(a => a.VehicleId.HasValue).Select(a => a.VehicleId!.Value).ToHashSet();
        var usedEmployeeIds = assignments.Where(a => a.EmployeeId.HasValue).Select(a => a.EmployeeId!.Value).ToHashSet();
        var teamMembers = _db.GetAllTeams().ToDictionary(t => t.Id, t => t.Members);
        // Mitarbeiter ueber Teammitgliedschaft mitgezaehlt.
        foreach (var a in assignments)
        {
            if (a.TeamId.HasValue && teamMembers.TryGetValue(a.TeamId.Value, out var mem))
                foreach (var m in mem) usedEmployeeIds.Add(m.Id);
        }

        _lvVehicle.Items.Clear();
        foreach (var v in _db.GetAllVehicles().Where(v => !usedVehicleIds.Contains(v.Id)).OrderBy(v => v.VehicleNumber))
        {
            var item = new ListViewItem(v.VehicleNumber);
            item.SubItems.Add(v.LicensePlate ?? "");
            _lvVehicle.Items.Add(item);
        }

        _lvEmployee.Items.Clear();
        foreach (var e in _db.GetAllEmployees().Where(e => !usedEmployeeIds.Contains(e.Id)).OrderBy(e => e.LastName).ThenBy(e => e.FirstName))
        {
            var team = _db.GetAllTeams().FirstOrDefault(t => t.Members.Any(m => m.Id == e.Id));
            var item = new ListViewItem(e.FullName);
            item.SubItems.Add(team?.Name ?? "–");
            _lvEmployee.Items.Add(item);
        }
    }

    private static void StyleButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = Color.FromArgb(0x2E, 0x7D, 0x32);
        btn.ForeColor = Color.White;
        btn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
    }
}
