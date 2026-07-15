namespace KOFplanner.Models;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Employee> Members { get; set; } = new();

    public string MemberSummary => Members.Count > 0
        ? string.Join(", ", Members.Select(m => m.FullName))
        : "(keine Mitglieder)";
}
