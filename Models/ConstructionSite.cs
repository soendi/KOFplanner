namespace KOFplanner.Models;

public class ConstructionSite
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public string DisplayText => string.IsNullOrEmpty(Address) ? Name : $"{Name} - {Address}";
}
