namespace KOFplanner.Models;

public class ConstructionSite
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public double DistanceKm { get; set; }
    public int DurationMinutes { get; set; }

    public string DurationText => DurationMinutes <= 0 ? "–" : $"{DurationMinutes / 60:D2}:{DurationMinutes % 60:D2}";

    public string DisplayText => string.IsNullOrEmpty(Address) ? Name : $"{Name} - {Address}";

    public override string ToString() => DisplayText;
}
