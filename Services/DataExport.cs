using System.Text.Json;

namespace KOFplanner.Services;

// Hilfsklasse: wandelt das JSON-Exportformat in CSV (und zurueck).
public static class DataExport
{
    public static string ToCsv(string json)
    {
        var doc = JsonDocument.Parse(json);
        var sb = new System.Text.StringBuilder();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            sb.AppendLine($"# {prop.Name}");
            if (prop.Value.ValueKind != JsonValueKind.Array) continue;
            bool headerWritten = false;
            foreach (var item in prop.Value.EnumerateArray())
            {
                if (!headerWritten)
                {
                    sb.AppendLine(string.Join("\t", item.EnumerateObject().Select(p => p.Name)));
                    headerWritten = true;
                }
                sb.AppendLine(string.Join("\t", item.EnumerateObject().Select(p => Escape(p.Value.ToString()))));
            }
        }
        return sb.ToString();
    }

    public static string FromCsv(string csv)
    {
        var sections = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>>();
        string? current = null;
        string[]? headers = null;
        foreach (var raw in csv.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) continue;
            if (line.StartsWith("# "))
            {
                current = line.Substring(2).Trim();
                sections[current] = new();
                headers = null;
                continue;
            }
            if (current == null) continue;
            var cols = line.Split('\t');
            if (headers == null) { headers = cols; continue; }
            var row = new System.Collections.Generic.Dictionary<string, string>();
            for (int i = 0; i < headers.Length && i < cols.Length; i++) row[headers[i]] = Unescape(cols[i]);
            sections[current].Add(row);
        }

        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        foreach (var kv in sections)
        {
            writer.WritePropertyName(kv.Key);
            writer.WriteStartArray();
            foreach (var row in kv.Value)
            {
                writer.WriteStartObject();
                foreach (var f in row) { writer.WritePropertyName(f.Key); writer.WriteStringValue(f.Value); }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string Escape(string s) => s.Replace("\t", " ").Replace("\n", " ").Replace("\\", "\\\\");
    private static string Unescape(string s) => s.Replace("\\\\", "\\");
}
