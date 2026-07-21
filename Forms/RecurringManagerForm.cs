using KOFplanner.Models;
using KOFplanner.Services;
using System;
using System.Linq;
using System.Windows.Forms;

namespace KOFplanner.Forms;

public class RecurringManagerForm : Form
{
    private readonly DatabaseService _db;
    private ListView _lv = null!;

    public RecurringManagerForm(DatabaseService db)
    {
        _db = db;
        IconHelper.Apply(this);
        Text = "Wiederkehrende Einsätze";
        Size = new System.Drawing.Size(700, 400);
        StartPosition = FormStartPosition.CenterParent;
        BuildUi();
        RefreshList();
    }

    private void BuildUi()
    {
        _lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = false };
        _lv.Columns.Add("Baustelle", 130);
        _lv.Columns.Add("Team", 95);
        _lv.Columns.Add("Fahrzeug", 85);
        _lv.Columns.Add("Mitarbeiter", 115);
        _lv.Columns.Add("Wochentage", 130);
        _lv.Columns.Add("Zeitraum", 105);
        _lv.DoubleClick += (_, _) => EditSelected();

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlp.Controls.Add(_lv, 0, 0);

        var pnl = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
        var btnNew = new Button { Text = "Neu", AutoSize = true };
        var btnEdit = new Button { Text = "Bearbeiten", AutoSize = true };
        var btnDel = new Button { Text = "Löschen", AutoSize = true };
        btnNew.Click += (_, _) => EditSelected();
        btnEdit.Click += (_, _) => EditSelected();
        btnDel.Click += BtnDel_Click;
        pnl.Controls.Add(btnDel); pnl.Controls.Add(btnEdit); pnl.Controls.Add(btnNew);
        tlp.Controls.Add(pnl, 0, 1);
        Controls.Add(tlp);
    }

    private void RefreshList()
    {
        var sites = _db.GetAllSites().ToDictionary(s => s.Id, s => s.Name);
        var teams = _db.GetAllTeams().ToDictionary(t => t.Id, t => t.Name);
        var veh = _db.GetAllVehicles().ToDictionary(v => v.Id, v => v.VehicleNumber);
        var emp = _db.GetAllEmployees().ToDictionary(e => e.Id, e => e.FullName);
        var ci = new System.Globalization.CultureInfo("de-AT");

        _lv.Items.Clear();
        foreach (var p in _db.GetAllRecurringPatterns())
        {
            var days = string.Join(", ", p.Weekdays.OrderBy(d => (int)d).Select(d => ci.DateTimeFormat.ShortestDayNames[(int)d]));
            var range = $"{p.StartDate:dd.MM.yy}" + (p.EndDate.HasValue ? $" – {p.EndDate:dd.MM.yy}" : " – offen");
            var item = new ListViewItem(sites.TryGetValue(p.ConstructionSiteId, out var n) ? n : "?");
            item.SubItems.Add(p.TeamId.HasValue && teams.TryGetValue(p.TeamId.Value, out var t) ? t : "–");
            item.SubItems.Add(p.VehicleId.HasValue && veh.TryGetValue(p.VehicleId.Value, out var v) ? v : "–");
            item.SubItems.Add(p.EmployeeId.HasValue && emp.TryGetValue(p.EmployeeId.Value, out var e) ? e : "–");
            item.SubItems.Add(days);
            item.SubItems.Add(range);
            item.Tag = p;
            _lv.Items.Add(item);
        }
    }

    private void EditSelected()
    {
        RecurringPattern? sel = _lv.SelectedItems.Count > 0 ? (RecurringPattern)_lv.SelectedItems[0].Tag! : null;
        using var f = new RecurringPatternForm(_db, sel);
        if (f.ShowDialog(this) == DialogResult.OK) RefreshList();
    }

    private void BtnDel_Click(object? sender, EventArgs e)
    {
        if (_lv.SelectedItems.Count == 0) return;
        var p = (RecurringPattern)_lv.SelectedItems[0].Tag!;
        if (MessageBox.Show("Wiederkehrenden Einsatz löschen?", "Bestätigen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _db.DeleteRecurringPattern(p.Id);
            RefreshList();
        }
    }
}
