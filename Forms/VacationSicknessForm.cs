using KOFplanner.Models;
using KOFplanner.Services;

namespace KOFplanner.Forms;

public class VacationSicknessForm : Form
{
    private readonly DatabaseService _db;
    private readonly List<Employee> _employees;
    private readonly TabControl _tabControl;
    private readonly ListView _lvVacations, _lvSickness;
    private readonly ComboBox _cmbEmployee;
    private readonly DateTimePicker _dtpStart, _dtpEnd;
    private readonly TextBox _txtNotes;

    public VacationSicknessForm(DatabaseService db, List<Employee> employees)
    {
        _db = db;
        _employees = employees;
        Text = "Urlaub & Krankheit verwalten";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(700, 500);
        Font = new Font("Segoe UI", 9.5f);

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };

        // Top: Tab control with lists
        _tabControl = new TabControl { Dock = DockStyle.Fill };
        var tabVac = new TabPage("Urlaub");
        _lvVacations = CreateListView();
        LoadVacations();
        tabVac.Controls.Add(_lvVacations);
        _tabControl.TabPages.Add(tabVac);

        var tabSick = new TabPage("Krankheit");
        _lvSickness = CreateListView();
        LoadSickness();
        tabSick.Controls.Add(_lvSickness);
        _tabControl.TabPages.Add(tabSick);

        tlp.Controls.Add(_tabControl, 0, 0);

        // Bottom: Input panel
        var inputPanel = new GroupBox { Text = "Neuer Eintrag", Dock = DockStyle.Fill };
        var inputTlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(10), RowCount = 2 };

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

        // Row 2: Notes and buttons
        inputTlp.RowCount = 3;
        inputTlp.Controls.Add(new Label { Text = "Notiz:", Anchor = AnchorStyles.Left }, 0, 2);
        _txtNotes = new TextBox { Dock = DockStyle.Fill };
        inputTlp.Controls.Add(_txtNotes, 1, 2);
        inputTlp.SetColumnSpan(_txtNotes, 2);

        var btnAdd = new Button { Text = "Hinzufügen", Width = 100, Anchor = AnchorStyles.Right };
        btnAdd.Click += (s, e) => AddEntry();
        inputTlp.Controls.Add(btnAdd, 3, 2);

        inputPanel.Controls.Add(inputTlp);
        tlp.Controls.Add(inputPanel, 0, 1);

        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 35));

        // Delete button
        var btnDelete = new Button { Text = "Ausgewählten Eintrag löschen", Dock = DockStyle.Bottom, Height = 30 };
        btnDelete.Click += (s, e) => DeleteSelected();
        Controls.Add(btnDelete);
        Controls.Add(tlp);
    }

    private ListView CreateListView()
    {
        var lv = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false
        };
        lv.Columns.Add("Mitarbeiter", 150);
        lv.Columns.Add("Von", 100);
        lv.Columns.Add("Bis", 100);
        lv.Columns.Add("Notiz", 200);
        return lv;
    }

    private void LoadVacations()
    {
        _lvVacations.Items.Clear();
        foreach (var v in _db.GetAllVacations())
            _lvVacations.Items.Add(new ListViewItem(new[] { v.Employee?.FullName ?? "", v.StartDate.ToString("dd.MM.yyyy"), v.EndDate.ToString("dd.MM.yyyy"), v.Notes ?? "" }) { Tag = v });
    }

    private void LoadSickness()
    {
        _lvSickness.Items.Clear();
        foreach (var s in _db.GetAllSickness())
            _lvSickness.Items.Add(new ListViewItem(new[] { s.Employee?.FullName ?? "", s.StartDate.ToString("dd.MM.yyyy"), s.EndDate.ToString("dd.MM.yyyy"), s.Notes ?? "" }) { Tag = s });
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

    private void DeleteSelected()
    {
        if (_tabControl.SelectedIndex == 0)
        {
            if (_lvVacations.SelectedItems.Count == 0) return;
            if (_lvVacations.SelectedItems[0].Tag is Vacation v)
            {
                _db.DeleteVacation(v.Id);
                LoadVacations();
            }
        }
        else
        {
            if (_lvSickness.SelectedItems.Count == 0) return;
            if (_lvSickness.SelectedItems[0].Tag is Sickness s)
            {
                _db.DeleteSickness(s.Id);
                LoadSickness();
            }
        }
    }
}
