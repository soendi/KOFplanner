using KOFplanner.Models;
using KOFplanner.Services;

namespace KOFplanner.Forms;

public class TeamForm : Form
{
    private readonly DatabaseService _db;
    private readonly Team _team;
    private readonly TextBox _txtName;
    private readonly ListBox _lbAvailable, _lbMembers;

    public TeamForm(DatabaseService db, Team? team, List<Employee> allEmployees)
    {
        _db = db;
        _team = team ?? new Team { Name = "" };
        Text = team == null ? "Neues Team" : "Team bearbeiten";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(600, 450);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(10), RowCount = 3 };

        tlp.Controls.Add(new Label { Text = "Teamname:", Anchor = AnchorStyles.Left }, 0, 0);
        tlp.SetColumnSpan(new Label { Text = "Teamname:", Anchor = AnchorStyles.Left }, 3);
        _txtName = new TextBox { Dock = DockStyle.Fill, Text = _team.Name };
        tlp.Controls.Add(_txtName, 0, 1);
        tlp.SetColumnSpan(_txtName, 3);

        // Available employees
        var availPanel = new Panel { Dock = DockStyle.Fill };
        availPanel.Controls.Add(new Label { Text = "Verfügbare Mitarbeiter", Dock = DockStyle.Top, Font = new Font(Font, FontStyle.Bold) });
        _lbAvailable = new ListBox { Dock = DockStyle.Fill, DisplayMember = "FullName", AllowDrop = true };

        var existingIds = _team.Members.Select(m => m.Id).ToHashSet();
        foreach (var e in allEmployees.Where(e => !existingIds.Contains(e.Id)))
            _lbAvailable.Items.Add(e);

        var btnAdd = new Button { Text = ">>", Width = 40, Dock = DockStyle.Bottom };
        btnAdd.Click += (s, e) => MoveSelected(_lbAvailable, _lbMembers);
        availPanel.Controls.Add(_lbAvailable);
        availPanel.Controls.Add(btnAdd);
        tlp.Controls.Add(availPanel, 0, 2);

        // Buttons panel
        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        btnPanel.Controls.Add(new Panel { Height = 50 }); // spacer
        var btnRemove = new Button { Text = "<<", Width = 40 };
        btnRemove.Click += (s, e) => MoveSelected(_lbMembers, _lbAvailable);
        btnPanel.Controls.Add(btnRemove);
        tlp.Controls.Add(btnPanel, 1, 2);

        // Members panel
        var memPanel = new Panel { Dock = DockStyle.Fill };
        memPanel.Controls.Add(new Label { Text = "Teammitglieder", Dock = DockStyle.Top, Font = new Font(Font, FontStyle.Bold) });
        _lbMembers = new ListBox { Dock = DockStyle.Fill, DisplayMember = "FullName" };
        foreach (var m in _team.Members)
            _lbMembers.Items.Add(m);
        memPanel.Controls.Add(_lbMembers);
        tlp.Controls.Add(memPanel, 2, 2);

        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Bottom buttons
        var bottomPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.LeftToRight, Height = 40 };
        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0x2E, 0x7D, 0x32), ForeColor = Color.White, Cursor = Cursors.Hand };
        btnOk.Click += (_, _) => Save();
        var btnCancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Width = 80, FlatStyle = FlatStyle.Flat };
        bottomPanel.Controls.AddRange(new Control[] { btnOk, btnCancel });
        Controls.Add(tlp);
        Controls.Add(bottomPanel);
    }

    private static void MoveSelected(ListBox from, ListBox to)
    {
        var items = from.SelectedItems.Cast<object>().ToList();
        foreach (var item in items)
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
        _team.Name = _txtName.Text.Trim();
        _team.Members = _lbMembers.Items.Cast<Employee>().ToList();
        _db.SaveTeam(_team);
    }
}
