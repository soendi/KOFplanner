using KOFplanner.Models;
using System.Globalization;
using System.Text;

namespace KOFplanner.Services;

// Erzeugt eine iCalendar-Datei (.ics) aus Einsaetzen, damit MA sie in
// Outlook / Handy / Google Calendar importieren koennen.
// RFC 5545: Zeilenende MUSS CRLF sein, lange Zeilen werden gefaltet.
public static class IcsExport
{
    public static string Build(List<Assignment> assignments, DateTime from, DateTime until)
    {
        var sb = new StringBuilder();
        AppendLine(sb, "BEGIN:VCALENDAR");
        AppendLine(sb, "VERSION:2.0");
        AppendLine(sb, "PRODID:-//KOFplanner//Baustellenplanung//DE");
        AppendLine(sb, "CALSCALE:GREGORIAN");
        AppendLine(sb, "METHOD:PUBLISH");

        // Mehrtaegige Einsaetze liegen als eine Zeile pro Tag vor und
        // werden zu zusammenhaengenden Bloecken zusammengefasst.
        var inRange = assignments
            .Where(a => a.Date.Date >= from.Date && a.Date.Date <= until.Date)
            .ToList();

        foreach (var b in AssignmentBlocks.Build(inRange))
        {
            WriteEvent(sb, b.Rep, b.First, b.Last);
        }

        AppendLine(sb, "END:VCALENDAR");
        return sb.ToString();
    }

    private static void WriteEvent(StringBuilder sb, Assignment a, DateTime first, DateTime last)
    {
        var siteName = a.Site?.Name ?? "Einsatz";
        var loc = a.Site?.Address ?? "";
        var desc = new List<string>();
        if (a.Team != null) desc.Add("Team: " + a.Team.Name);
        if (a.Vehicle != null) desc.Add("Fahrzeug: " + a.Vehicle.VehicleNumber);
        if (a.Employee != null) desc.Add("Mitarbeiter: " + a.Employee.FullName);

        // DTEND bei Ganztagsterminen = Tag NACH dem letzten Tag.
        var dtEnd = last.AddDays(1);

        AppendLine(sb, "BEGIN:VEVENT");
        AppendLine(sb, "UID:" + $"kofplanner-{a.Id}-{first:yyyyMMdd}");
        AppendLine(sb, "DTSTAMP:" + ToIcs(DateTime.Now));
        AppendLine(sb, "DTSTART;VALUE=DATE:" + first.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        AppendLine(sb, "DTEND;VALUE=DATE:" + dtEnd.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        AppendLine(sb, "SUMMARY:" + Fold(Escape(siteName)));
        if (loc.Length > 0) AppendLine(sb, "LOCATION:" + Fold(Escape(loc)));
        if (desc.Count > 0) AppendLine(sb, "DESCRIPTION:" + Fold(Escape(string.Join(" | ", desc))));
        AppendLine(sb, "END:VEVENT");
    }

    // Jede Zeile mit CRLF beenden (Google/L Outlook strikt).
    private static void AppendLine(StringBuilder sb, string line) => sb.Append(line).Append("\r\n");

    // Laengere Inhalte auf <= 75 Oktette falten (RFC 5545 3.1).
    private static string Fold(string content)
    {
        if (content.Length <= 75) return content;
        var result = new StringBuilder();
        result.Append(content, 0, 75);
        int i = 75;
        while (i < content.Length)
        {
            result.Append("\r\n ");
            int take = Math.Min(74, content.Length - i);
            result.Append(content, i, take);
            i += take;
        }
        return result.ToString();
    }

    private static string ToIcs(DateTime dt) => dt.ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("\\", "\\\\")
                .Replace(";", "\\;")
                .Replace(",", "\\,")
                .Replace("\r\n", "\\n")
                .Replace("\n", "\\n")
                .Replace("\r", "\\n");
    }
}
