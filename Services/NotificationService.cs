using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Net;
using System.Net.Mail;
using System.Text;
using KOFplanner.Models;

namespace KOFplanner.Services;

public class NotificationService
{
    private readonly string _baseDir;
    private readonly SettingsService _settings;

    public NotificationService(string baseDir, SettingsService settings)
    {
        _baseDir = baseDir;
        _settings = settings;
    }

    public string GeneratePdf(Employee emp, DateTime from, DateTime until, List<Assignment> assignments)
    {
        var dir = Path.Combine(_baseDir, "Ausdrucke");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"Einsatzplan_{emp.Id}_{from:yyyyMMdd}_{until:yyyyMMdd}.pdf");
        var bmp = RenderBitmap(emp, from, until, assignments);
        var jpg = Path.Combine(Path.GetTempPath(), $"kof_{Guid.NewGuid():N}.jpg");
        bmp.Save(jpg, System.Drawing.Imaging.ImageFormat.Jpeg);
        WritePdf(path, jpg, bmp.Width, bmp.Height);
        bmp.Dispose();
        File.Delete(jpg);
        return path;
    }

    private Bitmap RenderBitmap(Employee emp, DateTime from, DateTime until, List<Assignment> assignments)
    {
        const int w = 827, h = 1169;
        var bmp = new Bitmap(w, h);
        bmp.SetResolution(96, 96);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.White);

        // Header
        using var title = new Font("Segoe UI", 20, FontStyle.Bold);
        using var bold = new Font("Segoe UI", 11, FontStyle.Bold);
        using var norm = new Font("Segoe UI", 10);
        using var small = new Font("Segoe UI", 9);
        g.DrawString("Einsatzplan", title, Brushes.Black, 40, 30);
        g.DrawString($"{emp.FullName}", bold, Brushes.Black, 40, 64);
        g.DrawString($"{from:dd.MM.yyyy} bis {until:dd.MM.yyyy}", norm, Brushes.Black, 40, 84);
        g.DrawLine(Pens.Black, 40, 110, w - 40, 110);

        // ----- Top half: calendar -----
        var calTop = 130;
        var calH = 480;
        DrawMiniCalendar(g, emp, from, until, assignments, 40, calTop, w - 80, calH);

        // ----- Bottom half: table -----
        var tableTop = calTop + calH + 30;
        g.DrawString("Einsatze im Detail", bold, Brushes.Black, 40, tableTop);
        var y = tableTop + 30;
        var cols = new[] { (40, 70, "Datum"), (110, 100, "Baustelle"), (210, 230, "Adresse"), (440, 70, "km"), (510, 90, "Fahrzeit"), (600, 187, "Team/Fahrzeug") };
        using var headBg = new SolidBrush(Color.FromArgb(0x2E, 0x7D, 0x32));
        g.FillRectangle(headBg, 40, y - 4, w - 80, 22);
        foreach (var (x, wid, txt) in cols)
            g.DrawString(txt, bold, Brushes.White, x, y);
        y += 26;
        var byDate = assignments.OrderBy(a => a.Date).ThenBy(a => a.Site?.Name).ToList();
        using var linePen = new Pen(Color.LightGray);
        for (var d = from.Date; d <= until.Date; d = d.AddDays(1))
        {
            if (y > h - 60) break;
            var dayItems = byDate.Where(a => a.Date.Date == d).ToList();
            if (dayItems.Count == 0)
            {
                g.DrawString(d.ToString("ddd dd.MM."), small, Brushes.Gray, cols[0].Item1, y);
                g.DrawString("keine Einsatze", small, Brushes.Gray, cols[1].Item1, y);
                g.DrawLine(linePen, 40, y + 14, w - 40, y + 14);
                y += 20;
                continue;
            }
            foreach (var a in dayItems)
            {
                if (y > h - 60) break;
                var site = a.Site;
                var datum = a.Date.ToString("ddd dd.MM.");
                var baustelle = site?.Name ?? "";
                var adresse = site?.Address ?? "";
                var km = site != null && site.DistanceKm > 0 ? $"{site.DistanceKm:0.0}" : "–";
                var fahrzeit = site != null && site.DurationMinutes > 0 ? site.DurationText : "–";
                var info = "";
                if (a.Team != null) info += a.Team.Name;
                if (a.Vehicle != null) info += (info.Length > 0 ? " / " : "") + a.Vehicle.VehicleNumber;
                if (a.Employee != null && a.Team == null) info += a.Employee.FullName;
                g.DrawString(datum, small, Brushes.Black, cols[0].Item1, y);
                g.DrawString(Truncate(baustelle, 13), small, Brushes.Black, cols[1].Item1, y);
                g.DrawString(Truncate(adresse, 34), small, Brushes.Black, cols[2].Item1, y);
                g.DrawString(km, small, Brushes.Black, cols[3].Item1, y);
                g.DrawString(fahrzeit, small, Brushes.Black, cols[4].Item1, y);
                g.DrawString(Truncate(info, 26), small, Brushes.Black, cols[5].Item1, y);
                g.DrawLine(linePen, 40, y + 14, w - 40, y + 14);
                y += 20;
            }
        }
        if (byDate.Count == 0)
            g.DrawString("Keine Einsatze im gewahlten Zeitraum.", small, Brushes.Gray, 40, y);

        return bmp;
    }

    private void DrawMiniCalendar(Graphics g, Employee emp, DateTime from, DateTime until, List<Assignment> assignments, int x, int y, int width, int height)
    {
        var start = from.AddDays(-(int)from.DayOfWeek + (from.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
        if (start < from) start = start.AddDays(7);
        var weeks = (int)Math.Ceiling((until - start).TotalDays / 7.0) + 1;
        weeks = Math.Max(1, Math.Min(weeks, 6));
        var cellW = width / 7;
        var cellH = height / weeks;

        using var bold = new Font("Segoe UI", 9, FontStyle.Bold);
        using var dayF = new Font("Segoe UI", 8);
        var names = new[] { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };
        for (int i = 0; i < 7; i++)
            g.DrawString(names[i], bold, Brushes.Black, x + i * cellW + 3, y - 16);

        for (int r = 0; r < weeks; r++)
        {
            for (int c = 0; c < 7; c++)
            {
                var d = start.AddDays(r * 7 + c);
                if (d > until) continue;
                var cx = x + c * cellW;
                var cy = y + r * cellH;
                var rect = new Rectangle(cx, cy, cellW - 1, cellH - 1);
                var has = assignments.Any(a => a.Date == d);
                using var bb = new SolidBrush(has ? Color.FromArgb(0x2E, 0x7D, 0x32) : Color.White);
                g.FillRectangle(bb, rect);
                g.DrawRectangle(Pens.LightGray, rect);
                g.DrawString(d.Day.ToString(), dayF, has ? Brushes.White : Brushes.Black, cx + 3, cy + 2);
                if (has)
                {
                    var dayItems = assignments.Where(xx => xx.Date.Date == d).Take(6).ToList();
                    int ly = cy + 13;
                    foreach (var a in dayItems)
                    {
                        var txt = a.Site?.Name ?? (a.Team?.Name ?? "?");
                        g.DrawString(Truncate(txt, 14), dayF, Brushes.White, cx + 3, ly);
                        ly += 11;
                        if (ly > cy + cellH - 2) break;
                    }
                }
            }
        }
    }

    private static string Truncate(string s, int n)
    {
        s ??= "";
        return s.Length > n ? s[..n] + ".." : s;
    }

    private static void WritePdf(string pdfPath, string jpgPath, int imgW, int imgH)
    {
        var bytes = File.ReadAllBytes(jpgPath);
        var content = BuildContentBytes();
        RebuildPdf(pdfPath, bytes, imgW, imgH, content, 595.28, 841.89);
    }

    private static byte[] BuildContentBytes()
    {
        var content = "q 595.28 0 0 841.89 0 0 cm /Im0 Do Q";
        return Encoding.ASCII.GetBytes(content);
    }

    private static void RebuildPdf(string pdfPath, byte[] imgBytes, int imgW, int imgH, byte[] contentBytes, double ptW, double ptH)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        var enc = Encoding.ASCII;
        byte[] B(string s) => enc.GetBytes(s);

        var header = enc.GetBytes("%PDF-1.4\n");
        w.Write(header);

        long catPos = header.Length;
        w.Write(B("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n"));
        long pagesPos = w.BaseStream.Position;
        w.Write(B("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n"));
        long pagePos = w.BaseStream.Position;
        var page = $"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {ptW:0.##} {ptH:0.##}] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>\nendobj\n";
        w.Write(B(page));
        long imgPos = w.BaseStream.Position;
        var imgHeader = $"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {imgW} /Height {imgH} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {imgBytes.Length} >>\nstream\n";
        w.Write(B(imgHeader));
        w.Write(imgBytes);
        w.Write(B("\nendstream\nendobj\n"));
        long contentPos = w.BaseStream.Position;
        var contentHeader = $"5 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n";
        w.Write(B(contentHeader));
        w.Write(contentBytes);
        w.Write(B("\nendstream\nendobj\n"));

        long xrefPos = w.BaseStream.Position;
        var sb = new StringBuilder();
        sb.Append("xref\n0 6\n");
        sb.Append("0000000000 65535 f \n");
        sb.Append($"{catPos,10:0000000000} 00000 n \n");
        sb.Append($"{pagesPos,10:0000000000} 00000 n \n");
        sb.Append($"{pagePos,10:0000000000} 00000 n \n");
        sb.Append($"{imgPos,10:0000000000} 00000 n \n");
        sb.Append($"{contentPos,10:0000000000} 00000 n \n");
        sb.Append($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF");
        w.Write(enc.GetBytes(sb.ToString()));
        File.WriteAllBytes(pdfPath, ms.ToArray());
    }

    public void PrintPdf(string pdfPath, string? printerName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c print " + (string.IsNullOrEmpty(printerName) ? "" : $"/D:\"{printerName}\" ") + $"\"{pdfPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };
        using var p = Process.Start(psi);
        p?.WaitForExit(30000);
    }

    public bool SendEmail(string pdfPath, Employee emp, AppSettings settings, string? icsPath = null)
    {
        var es = settings.Email;
        if (string.IsNullOrWhiteSpace(es.Server) || string.IsNullOrWhiteSpace(emp.Email)) return false;
        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(es.Sender, "KOFplanner"),
                Subject = "Ihr Einsatzplan",
                Body = "Anbei erhalten Sie Ihren aktuellen Einsatzplan.\n\nMit freundlichen Grussen\nKOFplanner"
            };
            mail.To.Add(emp.Email);
            mail.Attachments.Add(new Attachment(pdfPath));
            if (icsPath != null && File.Exists(icsPath))
                mail.Attachments.Add(new Attachment(icsPath));
            using var smtp = new SmtpClient(es.Server, es.Port)
            {
                EnableSsl = es.UseSsl,
                Credentials = new NetworkCredential(es.Username, es.Password)
            };
            smtp.Send(mail);
            return true;
        }
        catch { return false; }
    }

    public List<string> GetPrinters() => PrinterSettings.InstalledPrinters.Cast<string>().ToList();
}
