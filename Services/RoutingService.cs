using System.Text.Json;
using System.Threading;

namespace KOFplanner.Services;

// Bestimmt die Fahrstrecke und -dauer zwischen zwei Adressen über die
// OpenStreetMap-Dienste (Nominatim zur Geocodierung, OSRM für die Streckenberechnung).
public class RoutingService
{
    private static readonly HttpClient _http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "KOFplanner/1.0 (Baustellenplanung)" } }
    };

    public class Result
    {
        public double DistanceKm;
        public int DurationMinutes;
    }

    public Result? Compute(string fromAddress, string toAddress, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(fromAddress) || string.IsNullOrWhiteSpace(toAddress))
        {
            error = "Adresse(n) fehlen.";
            return null;
        }
        try
        {
            // Nominatim erlaubt max. 1 Anfrage/Sekunde -> kurze Pause zwischen den Calls.
            var from = Geocode(fromAddress, out error);
            if (from == null) return null;
            Thread.Sleep(1100);
            var to = Geocode(toAddress, out error);
            if (to == null) return null;
            return Route(from.Value, to.Value, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    private (double lat, double lon)? Geocode(string address, out string? error)
    {
        error = null;
        var url = "https://nominatim.openstreetmap.org/search?format=json&limit=1&q=" + Uri.EscapeDataString(address);
        try
        {
            var json = _http.GetStringAsync(url).GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement;
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            {
                error = $"Adresse nicht gefunden: {address}";
                return null;
            }
            var first = arr[0];
            var lat = double.Parse(first.GetProperty("lat").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            var lon = double.Parse(first.GetProperty("lon").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            return (lat, lon);
        }
        catch (Exception ex)
        {
            error = $"Geocodierung fehlgeschlagen: {ex.Message}";
            return null;
        }
    }

    private Result? Route((double lat, double lon) from, (double lat, double lon) to, out string? error)
    {
        error = null;
        var url = $"https://router.project-osrm.org/route/v1/driving/{from.lon},{from.lat};{to.lon},{to.lat}?overview=false";
        try
        {
            var json = _http.GetStringAsync(url).GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.GetProperty("code").GetString() != "Ok")
            {
                error = "Route konnte nicht berechnet werden.";
                return null;
            }
            var route = root.GetProperty("routes")[0];
            var distanceMeters = route.GetProperty("distance").GetDouble();
            var durationSeconds = route.GetProperty("duration").GetDouble();
            return new Result
            {
                DistanceKm = Math.Round(distanceMeters / 1000.0, 1),
                DurationMinutes = (int)Math.Round(durationSeconds / 60.0)
            };
        }
        catch (Exception ex)
        {
            error = $"Routenberechnung fehlgeschlagen: {ex.Message}";
            return null;
        }
    }
}
