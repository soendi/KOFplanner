using KOFplanner.Models;
using KOFplanner.Services;

namespace KOFplanner.Forms;

public class VacationSicknessForm : Form
{
    private readonly DatabaseService _db;
    private readonly List<Employee> _employees;
    private readonly TabControl _tabControl;
    private readonly FlowLayoutPanel _flowVacations, _flowSickness;
    private readonly ComboBox _cmbEmployee;
    private readonly DateTimePicker _dtpStart, _dtpEnd;
    private readonly TextBox _txtNotes;

    public VacationSicknessForm(DatabaseService db, List<Employee> employees)
    {
        IconHelper.Apply(this);
        _db = db;
        _employees = employees;
        Text = "Urlaub & Krankheit verwalten";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(700, 540);
        Font = new Font("Segoe UI", 9.5f);

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };

        // Top: Tab control with lists
        _tabControl = new TabControl { Dock = DockStyle.Fill };
        var tabVac = new TabPage("Urlaub")
        {
            AutoScroll = true,
            Padding = new Padding(8)
        };
        _flowVacations = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4) };
        tabVac.Controls.Add(_flowVacations);
        LoadVacations();
        _tabControl.TabPages.Add(tabVac);

        var tabSick = new TabPage("Krankheit")
        {
            AutoScroll = true,
            Padding = new Padding(8)
        };
        _flowSickness = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4) };
        tabSick.Controls.Add(_flowSickness);
        LoadSickness();
        _tabControl.TabPages.Add(tabSick);

        tlp.Controls.Add(_tabControl, 0, 0);

        // Bottom: Input panel
        var inputPanel = new GroupBox { Text = "Neuer Eintrag", Dock = DockStyle.Fill };
        var inputTlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(10), RowCount = 3 };

        inputTlp.Controls.Add(new Label { Text = "Mitarbeiter:", Anchor = AnchorStyles.Left }, 0, 0);
        _cmbEmployee = new ComboBox { Dock = DockStyle.Fill, DisplayMember = "FullName" };
        foreach (var e in _employees) _cmbEmployee.Items.Add(e);
        inputTlp.Controls.Add(_cmbEmployee, 1, 0);

        inputTlp.Controls.Add(new Label { Text = "Von:", Anchor = AnchorStyles.Left }, 0, 1);
        _dtpStart = new DateTimePicker { Value = DateTime.Today };
        inputTlp.Controls.Add(_dtpStart, 1, 1);

        inputTlp.Controls.Add(new Label { Text = "Bis:", Anchor = AnchorStyles.Left }, 2, 1);
        _dtpEnd = new DateTimePicker { Value = DateTime.Today.AddDays(7) };
        inputTlp.Controls.Add(_dtpEnd, 3, 1);

        inputTlp.Controls.Add(new Label { Text = "Notiz:", Anchor = AnchorStyles.Left }, 0, 2);
        _txtNotes = new TextBox { Dock = DockStyle.Fill };
        inputTlp.Controls.Add(_txtNotes, 1, 2);
        inputTlp.SetColumnSpan(_txtNotes, 2);

        var btnAdd = new Button { Text = "Hinzufügen", Width = 100, Anchor = AnchorStyles.Right };
        btnAdd.Click += (s, e) => AddEntry();
        inputTlp.Controls.Add(btnAdd, 3, 2);

        inputPanel.Controls.Add(inputTlp);
        tlp.Controls.Add(inputPanel, 0, 1);

        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        Controls.Add(tlp);
    }

    private void LoadVacations()
    {
        _flowVacations.Controls.Clear();
        var list = _db.GetAllVacations().OrderBy(v => v.Employee?.FullName).ThenBy(v => v.StartDate).ToList();
        if (list.Count == 0)
            _flowVacations.Controls.Add(new Label { Text = "Keine Urlaube erfasst.", ForeColor = SystemColors.GrayText, AutoSize = true });
        foreach (var v in list)
        {
            var name = v.Employee?.FullName ?? $"MA {v.EmployeeId}";
            var range = v.StartDate.Date == v.EndDate.Date ? v.StartDate.ToString("dd.MM.yyyy") : $"{v.StartDate:dd.MM.yyyy} – {v.EndDate:dd.MM.yyyy}";
            var text = $"{name}  |  {range}" + (string.IsNullOrWhiteSpace(v.Notes) ? "" : $"  ({v.Notes})");
            var vid = v.Id;
            _flowVacations.Controls.Add(MakeDeletableRow(_flowVacations.Width, text, () =>
            {
                if (MessageBox.Show($"Urlaub löschen?\n{text}", "Urlaub löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _db.DeleteVacation(vid);
                    LoadVacations();
                }
            }));
        }
    }

    private void LoadSickness()
    {
        _flowSickness.Controls.Clear();
        var list = _db.GetAllSickness().OrderBy(s => s.Employee?.FullName).ThenBy(s => s.StartDate).ToList();
        if (list.Count == 0)
            _flowSickness.Controls.Add(new Label { Text = "Keine Krankheiten erfasst.", ForeColor = SystemColors.GrayText, AutoSize = true });
        foreach (var s in list)
        {
            var name = s.Employee?.FullName ?? $"MA {s.EmployeeId}";
            var range = s.StartDate.Date == s.EndDate.Date ? s.StartDate.ToString("dd.MM.yyyy") : $"{s.StartDate:dd.MM.yyyy} – {s.EndDate:dd.MM.yyyy}";
            var text = $"{name}  |  {range}" + (string.IsNullOrWhiteSpace(s.Notes) ? "" : $"  ({s.Notes})");
            var sid = s.Id;
            _flowSickness.Controls.Add(MakeDeletableRow(_flowSickness.Width, text, () =>
            {
                if (MessageBox.Show($"Krankheit löschen?\n{text}", "Krankheit löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _db.DeleteSickness(sid);
                    LoadSickness();
                }
            }));
        }
    }

    private static Panel MakeDeletableRow(int parentWidth, string text, Action onDelete)
    {
        var line = new Panel { Width = Math.Max(200, parentWidth - 24), Height = 32, Margin = new Padding(0, 0, 0, 4), BorderStyle = BorderStyle.FixedSingle, BackColor = SystemColors.Window };
        var lbl = new Label { Text = text, Location = new Point(6, 4), AutoSize = true, MaximumSize = new Size(line.Width - 40, 0), Padding = new Padding(0, 3, 0, 0) };
        var btnX = new Button { Text = "X", Width = 26, Height = 24 };
        btnX.Location = new Point(line.Width - 26 - 2, 4);
        btnX.Click += (_, _) => onDelete();
        line.Controls.Add(lbl);
        line.Controls.Add(btnX);
        return line;
    }

    private void AddEntry()
    {
        if (_cmbEmployee.SelectedItem is not Employee emp)
        {
            MessageBox.Show("Bitte Mitarbeiter auswählen.");
            return;
        }
        if (_dtpStart.Value > _dtpEnd.Value)
        {
            MessageBox.Show("Das Startdatum muss vor dem Enddatum liegen.");
            return;
        }

        if (_tabControl.SelectedIndex == 0) // Vacation
        {
            var v = new Vacation { EmployeeId = emp.Id, StartDate = _dtpStart.Value, EndDate = _dtpEnd.Value, Notes = _txtNotes.Text };
            _db.SaveVacation(v);
            LoadVacations();
        }
        else // Sickness
        {
            var s = new Sickness { EmployeeId = emp.Id, StartDate = _dtpStart.Value, EndDate = _dtpEnd.Value, Notes = _txtNotes.Text };
            _db.SaveSickness(s);
            LoadSickness();
        }
        MessageBox.Show("Eintrag erfolgreich hinzugefügt.");
    }
}
