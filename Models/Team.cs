namespace KOFplanner.Models;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int ColorArgb { get; set; } = Color.FromArgb(0x2E, 0x7D, 0x32).ToArgb();
    public int? PreferredVehicleId { get; set; }
    public List<Employee> Members { get; set; } = new();

    public Color Color => Color.FromArgb(ColorArgb);
    public string MemberSummary => Members.Count > 0
        ? string.Join(", ", Members.Select(m => m.FullName))
        : "(keine Mitglieder)";
}
