using KOFplanner.Models;
using System.Globalization;
using System.Text;

namespace KOFplanner.Services;

// Erzeugt eine iCalendar-Datei (.ics) aus Einsätzen, damit MA sie in
// Outlook / Handy / Google Calendar importieren koennen.
public static class IcsExport
{
    public static string Build(List<Assignment> assignments, DateTime from, DateTime until)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//KOFplanner//DE");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");

        foreach (var a in assignments)
        {
            if (a.Date.Date < from.Date || a.Date.Date > until.Date) continue;
            var siteName = a.Site?.Name ?? "Einsatz";
            var loc = a.Site?.Address ?? "";
            var desc = new List<string>();
            if (a.Team != null) desc.Add("Team: " + a.Team.Name);
            if (a.Vehicle != null) desc.Add("Fahrzeug: " + a.Vehicle.VehicleNumber);
            if (a.Employee != null) desc.Add("Mitarbeiter: " + a.Employee.FullName);

            var dayEnd = a.Date.Date.AddHours(23).AddMinutes(59);
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine("UID:" + $"{a.Id}@{a.Date:yyyyMMdd}@kofplanner");
            sb.AppendLine("DTSTAMP:" + ToIcs(DateTime.Now));
            sb.AppendLine("DTSTART;VALUE=DATE:" + a.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            sb.AppendLine("DTEND;VALUE=DATE:" + a.Date.AddDays(1).ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            sb.AppendLine("SUMMARY:" + Escape(siteName));
            if (loc.Length > 0) sb.AppendLine("LOCATION:" + Escape(loc));
            if (desc.Count > 0) sb.AppendLine("DESCRIPTION:" + Escape(string.Join(" | ", desc)));
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string ToIcs(DateTime dt) => dt.ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");
}
