using System.Text.Json;

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

    public Result? Compute(string fromAddress, string toAddress)
    {
        if (string.IsNullOrWhiteSpace(fromAddress) || string.IsNullOrWhiteSpace(toAddress))
            return null;
        try
        {
            var from = Geocode(fromAddress);
            var to = Geocode(toAddress);
            if (from == null || to == null) return null;
            return Route(from.Value, to.Value);
        }
        catch
        {
            return null;
        }
    }

    private (double lat, double lon)? Geocode(string address)
    {
        var url = "https://nominatim.openstreetmap.org/search?format=json&limit=1&q=" + Uri.EscapeDataString(address);
        var json = _http.GetStringAsync(url).GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return null;
        var first = arr[0];
        var lat = first.GetProperty("lat").GetDouble();
        var lon = first.GetProperty("lon").GetDouble();
        return (lat, lon);
    }

    private Result? Route((double lat, double lon) from, (double lat, double lon) to)
    {
        var url = $"https://router.project-osrm.org/route/v1/driving/{from.lon},{from.lat};{to.lon},{to.lat}?overview=false";
        var json = _http.GetStringAsync(url).GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("code").GetString() != "Ok") return null;
        var route = root.GetProperty("routes")[0];
        var distanceMeters = route.GetProperty("distance").GetDouble();
        var durationSeconds = route.GetProperty("duration").GetDouble();
        return new Result
        {
            DistanceKm = Math.Round(distanceMeters / 1000.0, 1),
            DurationMinutes = (int)Math.Round(durationSeconds / 60.0)
        };
    }
}
