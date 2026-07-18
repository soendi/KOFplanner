using KOFplanner.Models;
using KOFplanner.Services;
using System;
using System.Linq;
using System.Windows.Forms;

namespace KOFplanner.Forms;

public class StatisticsForm : Form
{
    private readonly DatabaseService _db;
    private DateTimePicker _dtFrom = null!;
    private DateTimePicker _dtUntil = null!;
    private ListView _lvVehicle = null!;
    private ListView _lvTeam = null!;

    public StatisticsForm(DatabaseService db)
    {
        _db = db;
        IconHelper.Apply(this);
        Text = "Statistik / Auslastung";
        Size = new System.Drawing.Size(720, 480);
        StartPosition = FormStartPosition.CenterParent;
        BuildUi();
        RefreshStats();
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
        btn.Click += (_, _) => RefreshStats();
        filter.Controls.Add(btn);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var tabV = new TabPage("Fahrzeuge");
        var tabT = new TabPage("Teams");

        _lvVehicle = new ListView { Dock = DockStyle.Fill, View = View.Details, GridLines = true, FullRowSelect = true };
        _lvVehicle.Columns.Add("Fahrzeug", 140); _lvVehicle.Columns.Add("Einsätze", 90); _lvVehicle.Columns.Add("Tage", 70); _lvVehicle.Columns.Add("Km gesamt", 110); _lvVehicle.Columns.Add("Fahrtzeit (h)", 110);
        tabV.Controls.Add(_lvVehicle);

        _lvTeam = new ListView { Dock = DockStyle.Fill, View = View.Details, GridLines = true, FullRowSelect = true };
        _lvTeam.Columns.Add("Team", 180); _lvTeam.Columns.Add("Einsätze", 90); _lvTeam.Columns.Add("Personentage", 110);
        tabT.Controls.Add(_lvTeam);

        tabs.TabPages.Add(tabV); tabs.TabPages.Add(tabT);

        Controls.Add(tabs);
        Controls.Add(filter);
    }

    private void RefreshStats()
    {
        var from = _dtFrom.Value.Date;
        var until = _dtUntil.Value.Date;
        var assignments = _db.GetAllAssignments(from, until);
        var veh = _db.GetAllVehicles().ToDictionary(v => v.Id, v => v.VehicleNumber);
        var teams = _db.GetAllTeams().ToDictionary(t => t.Id, t => t.Name);

        _lvVehicle.Items.Clear();
        foreach (var g in assignments.Where(a => a.VehicleId.HasValue).GroupBy(a => a.VehicleId!.Value))
        {
            var blocks = AssignmentBlocks.Build(g.ToList());
            var km = g.Sum(a => a.Site?.DistanceKm ?? 0);
            var min = g.Sum(a => a.Site?.DurationMinutes ?? 0);
            var item = new ListViewItem(veh.TryGetValue(g.Key, out var n) ? n : "?");
            item.SubItems.Add(blocks.Count.ToString());
            item.SubItems.Add(blocks.Sum(b => b.Days).ToString());
            item.SubItems.Add(km.ToString("F1"));
            item.SubItems.Add((min / 60.0).ToString("F1"));
            _lvVehicle.Items.Add(item);
        }

        _lvTeam.Items.Clear();
        foreach (var g in assignments.Where(a => a.TeamId.HasValue).GroupBy(a => a.TeamId!.Value))
        {
            var blocks = AssignmentBlocks.Build(g.ToList());
            var personTage = blocks.Sum(b => b.Days) + g.Count(a => a.EmployeeId.HasValue);
            var item = new ListViewItem(teams.TryGetValue(g.Key, out var n) ? n : "?");
            item.SubItems.Add(blocks.Count.ToString());
            item.SubItems.Add(personTage.ToString());
            _lvTeam.Items.Add(item);
        }
    }
}
