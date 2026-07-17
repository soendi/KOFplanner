namespace KOFplanner.Models;

// Wiederkehrender Einsatz: erzeugt fuer jeden passenden Wochentag im
// Zeitraum [StartDate, EndDate] einen Assignment auf der zugehoerigen Baustelle.
public class RecurringPattern
{
    public int Id { get; set; }
    public int ConstructionSiteId { get; set; }
    public int? TeamId { get; set; }
    public int? VehicleId { get; set; }
    public int? EmployeeId { get; set; }
    public HashSet<DayOfWeek> Weekdays { get; set; } = new();
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public string WeekdaysToken => string.Join(",", Weekdays.Select(d => (int)d).OrderBy(x => x));
}
