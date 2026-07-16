namespace KOFplanner.Models;

public class Vehicle
{
    public int Id { get; set; }
    public string RequiredLicense { get; set; } = "";
    public string VehicleNumber { get; set; } = "";
    public string LicensePlate { get; set; } = "";
    public int Seats { get; set; }

    public override string ToString() => $"[{RequiredLicense}] {VehicleNumber} ({LicensePlate})";
}
