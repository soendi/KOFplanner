namespace KOFplanner.Models;

public class Employee
{
    public static readonly string[] AllLicenseCategories = { "PKW", "3.5t", "7.5t", "LKW", "Anhänger" };

    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public bool HasDriversLicense { get; set; }
    public string LicenseCategories { get; set; } = "";

    public string FullName => $"{FirstName} {LastName}";

    public string[] GetLicenseList() =>
        string.IsNullOrEmpty(LicenseCategories) ? Array.Empty<string>() :
        LicenseCategories.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public void SetLicenseList(string[] cats) =>
        LicenseCategories = string.Join(",", cats);
}
