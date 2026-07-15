namespace KOFplanner.Models;

public class Assignment
{
    public int Id { get; set; }
    public int ConstructionSiteId { get; set; }
    public int? TeamId { get; set; }
    public int? VehicleId { get; set; }
    public int? EmployeeId { get; set; }
    public DateTime Date { get; set; }

    public ConstructionSite? Site { get; set; }
    public Team? Team { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Employee? Employee { get; set; }
}
