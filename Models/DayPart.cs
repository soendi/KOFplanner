namespace KOFplanner.Models;

// Halbtages-Granularitaet eines Einsatzes: Ganztag, Vormittag (bis Mittag)
// oder Nachmittag (bis Abend). Wird in der DB als "F"/"M"/"A" gespeichert.
public enum DayPart
{
    Full = 0,
    Morning = 1,
    Afternoon = 2
}

public static class DayPartHelper
{
    public static string ToCode(this DayPart p) => p switch
    {
        DayPart.Morning => "M",
        DayPart.Afternoon => "A",
        _ => "F"
    };

    public static DayPart FromCode(string? s) => s switch
    {
        "M" => DayPart.Morning,
        "A" => DayPart.Afternoon,
        _ => DayPart.Full
    };

    public static string Label(this DayPart p) => p switch
    {
        DayPart.Morning => "Vormittag",
        DayPart.Afternoon => "Nachmittag",
        _ => "Ganztag"
    };

    // Zwei Tages-Anteile kollidieren, wenn sie sich zeitlich ueberschneiden.
    // Ein Ganztag ueberschneidet alles; Vormittag und Nachmittag schliessen
    // sich gegenseitig aus.
    public static bool Overlaps(DayPart a, DayPart b)
    {
        if (a == DayPart.Full || b == DayPart.Full) return true;
        return a == b;
    }
}
