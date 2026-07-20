using Microsoft.Data.Sqlite;
using KOFplanner.Models;
using System.Linq;
using System.Text.Json;

namespace KOFplanner.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    public string DbPath { get; }

    public DatabaseService(string dbPath)
    {
        DbPath = dbPath;
        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private void Initialize()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Employees (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FirstName TEXT NOT NULL,
                LastName TEXT NOT NULL,
                HasDriversLicense INTEGER NOT NULL DEFAULT 0,
                VehicleCategory TEXT DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS Vehicles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Category TEXT NOT NULL,
                VehicleNumber TEXT NOT NULL,
                LicensePlate TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Teams (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS TeamMembers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TeamId INTEGER NOT NULL,
                EmployeeId INTEGER NOT NULL,
                FOREIGN KEY(TeamId) REFERENCES Teams(Id) ON DELETE CASCADE,
                FOREIGN KEY(EmployeeId) REFERENCES Employees(Id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS ConstructionSites (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Location TEXT DEFAULT '',
                StartDate TEXT NOT NULL,
                EndDate TEXT
            );
            CREATE TABLE IF NOT EXISTS Assignments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ConstructionSiteId INTEGER NOT NULL,
                TeamId INTEGER,
                VehicleId INTEGER,
                EmployeeId INTEGER,
                Date TEXT NOT NULL,
                FOREIGN KEY(ConstructionSiteId) REFERENCES ConstructionSites(Id) ON DELETE CASCADE,
                FOREIGN KEY(TeamId) REFERENCES Teams(Id) ON DELETE SET NULL,
                FOREIGN KEY(VehicleId) REFERENCES Vehicles(Id) ON DELETE SET NULL,
                FOREIGN KEY(EmployeeId) REFERENCES Employees(Id) ON DELETE SET NULL
            );
            CREATE TABLE IF NOT EXISTS Vacations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EmployeeId INTEGER NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                Notes TEXT,
                FOREIGN KEY(EmployeeId) REFERENCES Employees(Id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS Sickness (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EmployeeId INTEGER NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                Notes TEXT,
                FOREIGN KEY(EmployeeId) REFERENCES Employees(Id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS SchemaVersion (
                Version INTEGER NOT NULL
            );
        ";
        cmd.ExecuteNonQuery();

        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM SchemaVersion";
        if ((long)check.ExecuteScalar()! == 0)
        {
            using var ins = conn.CreateCommand();
            ins.CommandText = "INSERT INTO SchemaVersion (Version) VALUES (1)";
            ins.ExecuteNonQuery();
        }

        RunMigrations(conn);
    }

    private void RunMigrations(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT Version FROM SchemaVersion ORDER BY Version DESC LIMIT 1";
        var version = (long)check.ExecuteScalar()!;

        if (version < 2)
        {
            using var c1 = conn.CreateCommand();
            c1.CommandText = "ALTER TABLE Teams ADD COLUMN ColorArgb INTEGER NOT NULL DEFAULT 0";
            c1.ExecuteNonQuery();
            using var c2 = conn.CreateCommand();
            c2.CommandText = "ALTER TABLE Employees ADD COLUMN Email TEXT DEFAULT ''";
            c2.ExecuteNonQuery();
            using var c3 = conn.CreateCommand();
            c3.CommandText = "ALTER TABLE Teams ADD COLUMN PreferredVehicleId INTEGER";
            c3.ExecuteNonQuery();
            using var up = conn.CreateCommand();
            up.CommandText = "INSERT INTO SchemaVersion (Version) VALUES (2)";
            up.ExecuteNonQuery();
        }

        if (version < 3)
        {
            using var c4 = conn.CreateCommand();
            c4.CommandText = "ALTER TABLE Employees ADD COLUMN PaperPrint INTEGER NOT NULL DEFAULT 0";
            c4.ExecuteNonQuery();
            using var up = conn.CreateCommand();
            up.CommandText = "INSERT INTO SchemaVersion (Version) VALUES (3)";
            up.ExecuteNonQuery();
        }

        if (version < 4)
        {
            using var c5 = conn.CreateCommand();
            c5.CommandText = "ALTER TABLE Vehicles ADD COLUMN Seats INTEGER NOT NULL DEFAULT 0";
            c5.ExecuteNonQuery();
            using var up = conn.CreateCommand();
            up.CommandText = "INSERT INTO SchemaVersion (Version) VALUES (4)";
            up.ExecuteNonQuery();
        }

        if (version < 5)
        {
            using var c6 = conn.CreateCommand();
            c6.CommandText = "ALTER TABLE ConstructionSites ADD COLUMN DistanceKm REAL NOT NULL DEFAULT 0";
            c6.ExecuteNonQuery();
            using var c7 = conn.CreateCommand();
            c7.CommandText = "ALTER TABLE ConstructionSites ADD COLUMN DurationMinutes INTEGER NOT NULL DEFAULT 0";
            c7.ExecuteNonQuery();
            using var up = conn.CreateCommand();
            up.CommandText = "INSERT INTO SchemaVersion (Version) VALUES (5)";
            up.ExecuteNonQuery();
        }

        if (version < 6)
        {
            using var c8 = conn.CreateCommand();
            c8.CommandText = @"
                CREATE TABLE IF NOT EXISTS RecurringPatterns (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ConstructionSiteId INTEGER NOT NULL,
                    TeamId INTEGER,
                    VehicleId INTEGER,
                    EmployeeId INTEGER,
                    Weekdays TEXT NOT NULL,
                    StartDate TEXT NOT NULL,
                    EndDate TEXT,
                    FOREIGN KEY(ConstructionSiteId) REFERENCES ConstructionSites(Id) ON DELETE CASCADE,
                    FOREIGN KEY(TeamId) REFERENCES Teams(Id) ON DELETE SET NULL,
                    FOREIGN KEY(VehicleId) REFERENCES Vehicles(Id) ON DELETE SET NULL,
                    FOREIGN KEY(EmployeeId) REFERENCES Employees(Id) ON DELETE SET NULL
                )";
            c8.ExecuteNonQuery();
            using var up = conn.CreateCommand();
            up.CommandText = "INSERT INTO SchemaVersion (Version) VALUES (6)";
            up.ExecuteNonQuery();
        }

        if (version < 7)
        {
            using var c9 = conn.CreateCommand();
            c9.CommandText = "ALTER TABLE Assignments ADD COLUMN DayPart TEXT NOT NULL DEFAULT 'F'";
            c9.ExecuteNonQuery();
            using var up = conn.CreateCommand();
            up.CommandText = "INSERT INTO SchemaVersion (Version) VALUES (7)";
            up.ExecuteNonQuery();
        }
    }

    public SqliteConnection GetConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    // --- Employees ---
    public List<Employee> GetAllEmployees()
    {
        var list = new List<Employee>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Employees ORDER BY LastName, FirstName";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Employee
            {
                Id = r.GetInt32(0),
                FirstName = r.GetString(1),
                LastName = r.GetString(2),
                HasDriversLicense = r.GetInt32(3) == 1,
                LicenseCategories = r.IsDBNull(4) ? "" : r.GetString(4),
                Email = r.IsDBNull(5) ? "" : r.GetString(5),
                PaperPrint = !r.IsDBNull(6) && r.GetInt32(6) == 1
            });
        return list;
    }

    public void SaveEmployee(Employee e)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        if (e.Id == 0)
        {
            cmd.CommandText = "INSERT INTO Employees (FirstName, LastName, Email, HasDriversLicense, VehicleCategory, PaperPrint) VALUES (@fn, @ln, @em, @dl, @vc, @pp); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@fn", e.FirstName);
            cmd.Parameters.AddWithValue("@ln", e.LastName);
            cmd.Parameters.AddWithValue("@em", e.Email);
            cmd.Parameters.AddWithValue("@dl", e.HasDriversLicense ? 1 : 0);
            cmd.Parameters.AddWithValue("@vc", e.LicenseCategories);
            cmd.Parameters.AddWithValue("@pp", e.PaperPrint ? 1 : 0);
            e.Id = (int)(long)cmd.ExecuteScalar()!;
        }
        else
        {
            cmd.CommandText = "UPDATE Employees SET FirstName=@fn, LastName=@ln, Email=@em, HasDriversLicense=@dl, VehicleCategory=@vc, PaperPrint=@pp WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", e.Id);
            cmd.Parameters.AddWithValue("@fn", e.FirstName);
            cmd.Parameters.AddWithValue("@ln", e.LastName);
            cmd.Parameters.AddWithValue("@em", e.Email);
            cmd.Parameters.AddWithValue("@dl", e.HasDriversLicense ? 1 : 0);
            cmd.Parameters.AddWithValue("@vc", e.LicenseCategories);
            cmd.Parameters.AddWithValue("@pp", e.PaperPrint ? 1 : 0);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteEmployee(int id)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Employees WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    // --- Vehicles ---
    public List<Vehicle> GetAllVehicles()
    {
        var list = new List<Vehicle>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Category, VehicleNumber, LicensePlate, Seats FROM Vehicles ORDER BY Category, VehicleNumber";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Vehicle { Id = r.GetInt32(0), RequiredLicense = r.GetString(1), VehicleNumber = r.GetString(2), LicensePlate = r.GetString(3), Seats = r.IsDBNull(4) ? 0 : r.GetInt32(4) });
        return list;
    }

    public void SaveVehicle(Vehicle v)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        if (v.Id == 0)
        {
            cmd.CommandText = "INSERT INTO Vehicles (Category, VehicleNumber, LicensePlate, Seats) VALUES (@cat, @vn, @lp, @seats); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cat", v.RequiredLicense);
            cmd.Parameters.AddWithValue("@vn", v.VehicleNumber);
            cmd.Parameters.AddWithValue("@lp", v.LicensePlate);
            cmd.Parameters.AddWithValue("@seats", v.Seats);
            var idObj = cmd.ExecuteScalar();
            v.Id = idObj == null ? 0 : (int)(long)idObj;
        }
        else
        {
            cmd.CommandText = "UPDATE Vehicles SET Category=@cat, VehicleNumber=@vn, LicensePlate=@lp, Seats=@seats WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", v.Id);
            cmd.Parameters.AddWithValue("@cat", v.RequiredLicense);
            cmd.Parameters.AddWithValue("@vn", v.VehicleNumber);
            cmd.Parameters.AddWithValue("@lp", v.LicensePlate);
            cmd.Parameters.AddWithValue("@seats", v.Seats);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteVehicle(int id)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Vehicles WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    // --- Teams ---
    public List<Team> GetAllTeams()
    {
        var list = new List<Team>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Teams ORDER BY Name";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Team { Id = r.GetInt32(0), Name = r.GetString(1), ColorArgb = r.IsDBNull(2) ? 0 : r.GetInt32(2), PreferredVehicleId = r.IsDBNull(3) ? null : r.GetInt32(3) });

        foreach (var t in list)
            t.Members = GetTeamMembers(t.Id);
        return list;
    }

    public List<Employee> GetTeamMembers(int teamId)
    {
        var list = new List<Employee>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT e.* FROM TeamMembers tm JOIN Employees e ON tm.EmployeeId = e.Id WHERE tm.TeamId = @tid";
        cmd.Parameters.AddWithValue("@tid", teamId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Employee { Id = r.GetInt32(0), FirstName = r.GetString(1), LastName = r.GetString(2), Email = r.IsDBNull(5) ? "" : r.GetString(5), HasDriversLicense = r.GetInt32(3) == 1, LicenseCategories = r.IsDBNull(4) ? "" : r.GetString(4) });
        return list;
    }

    public void SaveTeam(Team t)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        if (t.Id == 0)
        {
            cmd.CommandText = "INSERT INTO Teams (Name, ColorArgb, PreferredVehicleId) VALUES (@n, @c, @pv); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@n", t.Name);
            cmd.Parameters.AddWithValue("@c", t.ColorArgb);
            cmd.Parameters.AddWithValue("@pv", (object?)t.PreferredVehicleId ?? DBNull.Value);
            t.Id = (int)(long)cmd.ExecuteScalar()!;
        }
        else
        {
            cmd.CommandText = "UPDATE Teams SET Name=@n, ColorArgb=@c, PreferredVehicleId=@pv WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", t.Id);
            cmd.Parameters.AddWithValue("@n", t.Name);
            cmd.Parameters.AddWithValue("@c", t.ColorArgb);
            cmd.Parameters.AddWithValue("@pv", (object?)t.PreferredVehicleId ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        SaveTeamMembers(t.Id, t.Members);
    }

    private void SaveTeamMembers(int teamId, List<Employee> members)
    {
        using var conn = GetConnection();
        using var del = conn.CreateCommand();
        del.CommandText = "DELETE FROM TeamMembers WHERE TeamId=@tid";
        del.Parameters.AddWithValue("@tid", teamId);
        del.ExecuteNonQuery();

        foreach (var m in members)
        {
            using var ins = conn.CreateCommand();
            ins.CommandText = "INSERT INTO TeamMembers (TeamId, EmployeeId) VALUES (@tid, @eid)";
            ins.Parameters.AddWithValue("@tid", teamId);
            ins.Parameters.AddWithValue("@eid", m.Id);
            ins.ExecuteNonQuery();
        }
    }

    public void DeleteTeam(int id)
    {
        using var conn = GetConnection();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Assignments WHERE TeamId=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Teams WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // --- Construction Sites ---
    public List<ConstructionSite> GetAllSites()
    {
        var list = new List<ConstructionSite>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ConstructionSites ORDER BY StartDate DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new ConstructionSite
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                Address = r.IsDBNull(2) ? "" : r.GetString(2),
                StartDate = DateTime.Parse(r.GetString(3)),
                EndDate = r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4)),
                DistanceKm = r.IsDBNull(5) ? 0 : r.GetDouble(5),
                DurationMinutes = r.IsDBNull(6) ? 0 : r.GetInt32(6)
            });
        return list;
    }

    public void SaveSite(ConstructionSite s)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        if (s.Id == 0)
        {
            cmd.CommandText = "INSERT INTO ConstructionSites (Name, Location, StartDate, EndDate, DistanceKm, DurationMinutes) VALUES (@n, @l, @sd, @ed, @km, @dur); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@n", s.Name);
            cmd.Parameters.AddWithValue("@l", s.Address);
            cmd.Parameters.AddWithValue("@sd", s.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@ed", s.EndDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@km", s.DistanceKm);
            cmd.Parameters.AddWithValue("@dur", s.DurationMinutes);
            s.Id = (int)(long)cmd.ExecuteScalar()!;
        }
        else
        {
            cmd.CommandText = "UPDATE ConstructionSites SET Name=@n, Location=@l, StartDate=@sd, EndDate=@ed, DistanceKm=@km, DurationMinutes=@dur WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", s.Id);
            cmd.Parameters.AddWithValue("@n", s.Name);
            cmd.Parameters.AddWithValue("@l", s.Address);
            cmd.Parameters.AddWithValue("@sd", s.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@ed", s.EndDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@km", s.DistanceKm);
            cmd.Parameters.AddWithValue("@dur", s.DurationMinutes);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteSite(int id)
    {
        using var conn = GetConnection();
        // Delete all calendar entries referencing this site. Teams stay intact
        // (they may be reused on other sites). Cascading foreign keys handle the rest.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Assignments WHERE ConstructionSiteId=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM ConstructionSites WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // --- Assignments ---
    public List<Assignment> GetAssignments(DateTime date)
    {
        var list = new List<Assignment>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.Id, a.ConstructionSiteId, a.TeamId, a.VehicleId, a.EmployeeId, a.Date,
                   cs.Name, cs.Location, cs.StartDate, cs.EndDate,
                   t.Name, v.Category, v.VehicleNumber, v.LicensePlate,
                   e.FirstName, e.LastName, cs.DistanceKm, cs.DurationMinutes,
                   a.DayPart
            FROM Assignments a
            JOIN ConstructionSites cs ON a.ConstructionSiteId = cs.Id
            LEFT JOIN Teams t ON a.TeamId = t.Id
            LEFT JOIN Vehicles v ON a.VehicleId = v.Id
            LEFT JOIN Employees e ON a.EmployeeId = e.Id
            WHERE a.Date = @d
            ORDER BY cs.Name";
        cmd.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var a = new Assignment
            {
                Id = r.GetInt32(0),
                ConstructionSiteId = r.GetInt32(1),
                TeamId = r.IsDBNull(2) ? null : r.GetInt32(2),
                VehicleId = r.IsDBNull(3) ? null : r.GetInt32(3),
                EmployeeId = r.IsDBNull(4) ? null : r.GetInt32(4),
                Date = DateTime.Parse(r.GetString(5)),
                Site = new ConstructionSite
                {
                    Id = r.GetInt32(1),
                    Name = r.GetString(6),
                    Address = r.IsDBNull(7) ? "" : r.GetString(7),
                    StartDate = DateTime.Parse(r.GetString(8)),
                    EndDate = r.IsDBNull(9) ? null : DateTime.Parse(r.GetString(9)),
                    DistanceKm = r.IsDBNull(16) ? 0 : r.GetDouble(16),
                    DurationMinutes = r.IsDBNull(17) ? 0 : r.GetInt32(17)
                }
            };
            if (!r.IsDBNull(10))
                a.Team = new Team { Id = a.TeamId ?? 0, Name = r.GetString(10) };
            if (!r.IsDBNull(11))
                a.Vehicle = new Vehicle { Id = a.VehicleId ?? 0, RequiredLicense = r.GetString(11), VehicleNumber = r.GetString(12), LicensePlate = r.GetString(13) };
            if (!r.IsDBNull(14))
                a.Employee = new Employee { Id = a.EmployeeId ?? 0, FirstName = r.GetString(14), LastName = r.GetString(15) };
            a.Part = r.IsDBNull(r.GetOrdinal("DayPart")) ? DayPart.Full : DayPartHelper.FromCode(r.GetString(r.GetOrdinal("DayPart")));
            list.Add(a);
        }
        return list;
    }

    public List<Assignment> GetMonthAssignments(DateTime month)
    {
        var list = new List<Assignment>();
        var start = new DateTime(month.Year, month.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.Id, a.ConstructionSiteId, a.TeamId, a.VehicleId, a.EmployeeId, a.Date,
                   cs.Name, cs.Location,
                   t.Name, v.Category, v.VehicleNumber, v.LicensePlate,
                   e.FirstName, e.LastName,
                   a.DayPart
            FROM Assignments a
            JOIN ConstructionSites cs ON a.ConstructionSiteId = cs.Id
            LEFT JOIN Teams t ON a.TeamId = t.Id
            LEFT JOIN Vehicles v ON a.VehicleId = v.Id
            LEFT JOIN Employees e ON a.EmployeeId = e.Id
            WHERE a.Date >= @s AND a.Date <= @e
            ORDER BY a.Date, cs.Name";
        cmd.Parameters.AddWithValue("@s", start.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@e", end.ToString("yyyy-MM-dd"));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var a = new Assignment
            {
                Id = r.GetInt32(0),
                ConstructionSiteId = r.GetInt32(1),
                TeamId = r.IsDBNull(2) ? null : r.GetInt32(2),
                VehicleId = r.IsDBNull(3) ? null : r.GetInt32(3),
                EmployeeId = r.IsDBNull(4) ? null : r.GetInt32(4),
                Date = DateTime.Parse(r.GetString(5)),
                Site = new ConstructionSite { Id = r.GetInt32(1), Name = r.GetString(6), Address = r.IsDBNull(7) ? "" : r.GetString(7) }
            };
            if (!r.IsDBNull(8))
                a.Team = new Team { Id = a.TeamId ?? 0, Name = r.GetString(8) };
            if (!r.IsDBNull(9))
                a.Vehicle = new Vehicle { Id = a.VehicleId ?? 0, RequiredLicense = r.GetString(9), VehicleNumber = r.GetString(10), LicensePlate = r.GetString(11) };
            if (!r.IsDBNull(12))
                a.Employee = new Employee { Id = a.EmployeeId ?? 0, FirstName = r.GetString(12), LastName = r.GetString(13) };
            a.Part = r.IsDBNull(r.GetOrdinal("DayPart")) ? DayPart.Full : DayPartHelper.FromCode(r.GetString(r.GetOrdinal("DayPart")));
            list.Add(a);
        }
        return list;
    }

    public List<Assignment> GetAssignmentsForEmployee(int employeeId, DateTime from, DateTime until)
    {
        var list = new List<Assignment>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.Id, a.ConstructionSiteId, a.TeamId, a.VehicleId, a.EmployeeId, a.Date,
                   cs.Name, cs.Location, cs.StartDate, cs.EndDate,
                   t.Name, v.Category, v.VehicleNumber, v.LicensePlate,
                   e.FirstName, e.LastName, cs.DistanceKm, cs.DurationMinutes,
                   a.DayPart
            FROM Assignments a
            JOIN ConstructionSites cs ON a.ConstructionSiteId = cs.Id
            LEFT JOIN Teams t ON a.TeamId = t.Id
            LEFT JOIN Vehicles v ON a.VehicleId = v.Id
            LEFT JOIN Employees e ON a.EmployeeId = e.Id
            WHERE a.EmployeeId = @eid AND a.Date >= @s AND a.Date <= @u
            ORDER BY a.Date, cs.Name";
        cmd.Parameters.AddWithValue("@eid", employeeId);
        cmd.Parameters.AddWithValue("@s", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@u", until.ToString("yyyy-MM-dd"));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var a = new Assignment
            {
                Id = r.GetInt32(0),
                ConstructionSiteId = r.GetInt32(1),
                TeamId = r.IsDBNull(2) ? null : r.GetInt32(2),
                VehicleId = r.IsDBNull(3) ? null : r.GetInt32(3),
                EmployeeId = r.IsDBNull(4) ? null : r.GetInt32(4),
                Date = DateTime.Parse(r.GetString(5)),
                Site = new ConstructionSite { Id = r.GetInt32(1), Name = r.GetString(6), Address = r.IsDBNull(7) ? "" : r.GetString(7), StartDate = DateTime.Parse(r.GetString(8)), EndDate = r.IsDBNull(9) ? null : DateTime.Parse(r.GetString(9)), DistanceKm = r.IsDBNull(16) ? 0 : r.GetDouble(16), DurationMinutes = r.IsDBNull(17) ? 0 : r.GetInt32(17) }
            };
            if (!r.IsDBNull(10))
                a.Team = new Team { Id = a.TeamId ?? 0, Name = r.GetString(10) };
            if (!r.IsDBNull(11))
                a.Vehicle = new Vehicle { Id = a.VehicleId ?? 0, RequiredLicense = r.GetString(11), VehicleNumber = r.GetString(12), LicensePlate = r.GetString(13) };
            if (!r.IsDBNull(14))
                a.Employee = new Employee { Id = a.EmployeeId ?? 0, FirstName = r.GetString(14), LastName = r.GetString(15) };
            a.Part = r.IsDBNull(r.GetOrdinal("DayPart")) ? DayPart.Full : DayPartHelper.FromCode(r.GetString(r.GetOrdinal("DayPart")));
            list.Add(a);
        }
        return list;
    }

    // All assignments that reference the given vehicle (any day).
    public List<Assignment> GetAssignmentsForVehicle(int vehicleId)
    {
        var list = new List<Assignment>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.Id, a.ConstructionSiteId, a.TeamId, a.VehicleId, a.EmployeeId, a.Date,
                   cs.Name, t.Name
            FROM Assignments a
            JOIN ConstructionSites cs ON a.ConstructionSiteId = cs.Id
            LEFT JOIN Teams t ON a.TeamId = t.Id
            WHERE a.VehicleId = @vid
            ORDER BY a.Date";
        cmd.Parameters.AddWithValue("@vid", vehicleId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Assignment
            {
                Id = r.GetInt32(0),
                ConstructionSiteId = r.GetInt32(1),
                TeamId = r.IsDBNull(2) ? null : r.GetInt32(2),
                VehicleId = r.IsDBNull(3) ? null : r.GetInt32(3),
                EmployeeId = r.IsDBNull(4) ? null : r.GetInt32(4),
                Date = DateTime.Parse(r.GetString(5)),
                Site = new ConstructionSite { Id = r.GetInt32(1), Name = r.GetString(6) },
                Team = r.IsDBNull(7) ? null : new Team { Id = r.GetInt32(2), Name = r.GetString(7) }
            });
        }
        return list;
    }

    // Assignments where the employee is a member of the assigned team (team-termine),
    // but NOT single entries that directly carry the employee (those are covered by
    // GetAssignmentsForEmployee). Returns the distinct team assignments.
    public List<Assignment> GetTeamAssignmentsForEmployee(int employeeId)
    {
        var list = new List<Assignment>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT DISTINCT a.Id, a.ConstructionSiteId, a.TeamId, a.VehicleId, a.EmployeeId, a.Date,
                   cs.Name, t.Name
            FROM Assignments a
            JOIN ConstructionSites cs ON a.ConstructionSiteId = cs.Id
            JOIN Teams t ON a.TeamId = t.Id
            JOIN TeamMembers tm ON tm.TeamId = t.Id
            WHERE tm.EmployeeId = @eid AND (a.EmployeeId IS NULL OR a.EmployeeId != @eid)
            ORDER BY a.Date";
        cmd.Parameters.AddWithValue("@eid", employeeId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Assignment
            {
                Id = r.GetInt32(0),
                ConstructionSiteId = r.GetInt32(1),
                TeamId = r.IsDBNull(2) ? null : r.GetInt32(2),
                VehicleId = r.IsDBNull(3) ? null : r.GetInt32(3),
                EmployeeId = r.IsDBNull(4) ? null : r.GetInt32(4),
                Date = DateTime.Parse(r.GetString(5)),
                Site = new ConstructionSite { Id = r.GetInt32(1), Name = r.GetString(6) },
                Team = r.IsDBNull(7) ? null : new Team { Id = r.GetInt32(2), Name = r.GetString(7) }
            });
        }
        return list;
    }

    public List<Assignment> GetAllAssignments(DateTime from, DateTime until)
    {
        var list = new List<Assignment>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.Id, a.ConstructionSiteId, a.TeamId, a.VehicleId, a.EmployeeId, a.Date,
                   cs.Name, cs.Location, cs.StartDate, cs.EndDate,
                   t.Name, v.Category, v.VehicleNumber, v.LicensePlate,
                   e.FirstName, e.LastName, cs.DistanceKm, cs.DurationMinutes,
                   a.DayPart
            FROM Assignments a
            JOIN ConstructionSites cs ON a.ConstructionSiteId = cs.Id
            LEFT JOIN Teams t ON a.TeamId = t.Id
            LEFT JOIN Vehicles v ON a.VehicleId = v.Id
            LEFT JOIN Employees e ON a.EmployeeId = e.Id
            WHERE a.Date >= @s AND a.Date <= @u
            ORDER BY a.Date, cs.Name";
        cmd.Parameters.AddWithValue("@s", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@u", until.ToString("yyyy-MM-dd"));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var a = new Assignment
            {
                Id = r.GetInt32(0),
                ConstructionSiteId = r.GetInt32(1),
                TeamId = r.IsDBNull(2) ? null : r.GetInt32(2),
                VehicleId = r.IsDBNull(3) ? null : r.GetInt32(3),
                EmployeeId = r.IsDBNull(4) ? null : r.GetInt32(4),
                Date = DateTime.Parse(r.GetString(5)),
                Site = new ConstructionSite { Id = r.GetInt32(1), Name = r.GetString(6), Address = r.IsDBNull(7) ? "" : r.GetString(7), StartDate = DateTime.Parse(r.GetString(8)), EndDate = r.IsDBNull(9) ? null : DateTime.Parse(r.GetString(9)), DistanceKm = r.IsDBNull(16) ? 0 : r.GetDouble(16), DurationMinutes = r.IsDBNull(17) ? 0 : r.GetInt32(17) }
            };
            if (!r.IsDBNull(10))
                a.Team = new Team { Id = a.TeamId ?? 0, Name = r.GetString(10) };
            if (!r.IsDBNull(11))
                a.Vehicle = new Vehicle { Id = a.VehicleId ?? 0, RequiredLicense = r.GetString(11), VehicleNumber = r.GetString(12), LicensePlate = r.GetString(13) };
            if (!r.IsDBNull(14))
                a.Employee = new Employee { Id = a.EmployeeId ?? 0, FirstName = r.GetString(14), LastName = r.GetString(15) };
            a.Part = r.IsDBNull(r.GetOrdinal("DayPart")) ? DayPart.Full : DayPartHelper.FromCode(r.GetString(r.GetOrdinal("DayPart")));
            list.Add(a);
        }

        // Wiederkehrende Muster expandieren (ohne Doppelbelegung zu bestehenden Einsätzen).
        var sitesById = GetAllSites().ToDictionary(s => s.Id, s => s);
        var existing = new HashSet<string>(list.Select(a =>
            $"{a.Date:yyyy-MM-dd}|{a.ConstructionSiteId}|{a.TeamId}|{a.EmployeeId}"));
        foreach (var ra in ExpandRecurring(from, until))
        {
            var key = $"{ra.Date:yyyy-MM-dd}|{ra.ConstructionSiteId}|{ra.TeamId}|{ra.EmployeeId}";
            if (existing.Contains(key)) continue;
            ra.Site = sitesById.TryGetValue(ra.ConstructionSiteId, out var s) ? s : null;
            list.Add(ra);
        }
        return list;
    }

    public void SaveAssignment(Assignment a)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        if (a.Id == 0)
        {
            cmd.CommandText = "INSERT INTO Assignments (ConstructionSiteId, TeamId, VehicleId, EmployeeId, Date, DayPart) VALUES (@cs, @t, @v, @e, @d, @p); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cs", a.ConstructionSiteId);
            cmd.Parameters.AddWithValue("@t", (object?)a.TeamId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@v", (object?)a.VehicleId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@e", (object?)a.EmployeeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@d", a.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@p", a.Part.ToCode());
            a.Id = (int)(long)cmd.ExecuteScalar()!;
        }
        else
        {
            cmd.CommandText = "UPDATE Assignments SET ConstructionSiteId=@cs, TeamId=@t, VehicleId=@v, EmployeeId=@e, Date=@d, DayPart=@p WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", a.Id);
            cmd.Parameters.AddWithValue("@cs", a.ConstructionSiteId);
            cmd.Parameters.AddWithValue("@t", (object?)a.TeamId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@v", (object?)a.VehicleId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@e", (object?)a.EmployeeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@d", a.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@p", a.Part.ToCode());
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteAssignment(int id)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Assignments WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    // --- Recurring patterns ---
    public List<RecurringPattern> GetAllRecurringPatterns()
    {
        var list = new List<RecurringPattern>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, ConstructionSiteId, TeamId, VehicleId, EmployeeId, Weekdays, StartDate, EndDate FROM RecurringPatterns";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var days = new HashSet<DayOfWeek>();
            foreach (var part in r.GetString(5).Split(','))
                if (int.TryParse(part, out var d) && d >= 0 && d <= 6) days.Add((DayOfWeek)d);
            list.Add(new RecurringPattern
            {
                Id = r.GetInt32(0),
                ConstructionSiteId = r.GetInt32(1),
                TeamId = r.IsDBNull(2) ? null : r.GetInt32(2),
                VehicleId = r.IsDBNull(3) ? null : r.GetInt32(3),
                EmployeeId = r.IsDBNull(4) ? null : r.GetInt32(4),
                Weekdays = days,
                StartDate = DateTime.Parse(r.GetString(6)),
                EndDate = r.IsDBNull(7) ? null : DateTime.Parse(r.GetString(7))
            });
        }
        return list;
    }

    public void SaveRecurringPattern(RecurringPattern p)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        if (p.Id == 0)
        {
            cmd.CommandText = "INSERT INTO RecurringPatterns (ConstructionSiteId, TeamId, VehicleId, EmployeeId, Weekdays, StartDate, EndDate) VALUES (@cs, @t, @v, @e, @w, @s, @u); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cs", p.ConstructionSiteId);
            cmd.Parameters.AddWithValue("@t", (object?)p.TeamId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@v", (object?)p.VehicleId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@e", (object?)p.EmployeeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@w", p.WeekdaysToken);
            cmd.Parameters.AddWithValue("@s", p.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@u", (object?)p.EndDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
            var idObj = cmd.ExecuteScalar();
            p.Id = idObj == null ? 0 : (int)(long)idObj;
        }
        else
        {
            cmd.CommandText = "UPDATE RecurringPatterns SET ConstructionSiteId=@cs, TeamId=@t, VehicleId=@v, EmployeeId=@e, Weekdays=@w, StartDate=@s, EndDate=@u WHERE Id=@id";
            cmd.Parameters.AddWithValue("@cs", p.ConstructionSiteId);
            cmd.Parameters.AddWithValue("@t", (object?)p.TeamId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@v", (object?)p.VehicleId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@e", (object?)p.EmployeeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@w", p.WeekdaysToken);
            cmd.Parameters.AddWithValue("@s", p.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@u", (object?)p.EndDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", p.Id);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteRecurringPattern(int id)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM RecurringPatterns WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    // Expandiert wiederkehrende Muster zu konkreten Assignments im Bereich.
    public List<Assignment> ExpandRecurring(DateTime from, DateTime until)
    {
        var result = new List<Assignment>();
        foreach (var p in GetAllRecurringPatterns())
        {
            var start = p.StartDate.Date > from ? p.StartDate.Date : from;
            var end = (p.EndDate?.Date ?? until) < until ? (p.EndDate?.Date ?? until) : until;
            for (var d = start; d <= end; d = d.AddDays(1))
            {
                if (p.Weekdays.Contains(d.DayOfWeek))
                {
                    result.Add(new Assignment
                    {
                        ConstructionSiteId = p.ConstructionSiteId,
                        TeamId = p.TeamId,
                        VehicleId = p.VehicleId,
                        EmployeeId = p.EmployeeId,
                        Date = d
                    });
                }
            }
        }
        return result;
    }

    // --- Conflict checks ---
    public bool IsVehicleAssigned(int vehicleId, DateTime date, int? excludeAssignmentId = null)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Assignments WHERE VehicleId=@vid AND Date=@d" +
            (excludeAssignmentId.HasValue ? " AND Id!=@eid" : "");
        cmd.Parameters.AddWithValue("@vid", vehicleId);
        cmd.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
        if (excludeAssignmentId.HasValue)
            cmd.Parameters.AddWithValue("@eid", excludeAssignmentId.Value);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    public bool IsVehicleAssigned(int vehicleId, DateTime date, List<int> excludeIds)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        var excl = excludeIds != null && excludeIds.Count > 0
            ? " AND Id NOT IN (" + string.Join(",", excludeIds) + ")" : "";
        cmd.CommandText = "SELECT COUNT(*) FROM Assignments WHERE VehicleId=@vid AND Date=@d" + excl;
        cmd.Parameters.AddWithValue("@vid", vehicleId);
        cmd.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
        return (long)cmd.ExecuteScalar()! > 0;
    }

    public bool IsTeamAssigned(int teamId, DateTime date, int? excludeAssignmentId = null)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Assignments WHERE TeamId=@tid AND Date=@d" +
            (excludeAssignmentId.HasValue ? " AND Id!=@eid" : "");
        cmd.Parameters.AddWithValue("@tid", teamId);
        cmd.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
        if (excludeAssignmentId.HasValue)
            cmd.Parameters.AddWithValue("@eid", excludeAssignmentId.Value);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    // Returns the assignment row linking the given team on the given day (any vehicle), or null.
    public Assignment? GetTeamAssignmentOnDay(int teamId, DateTime date)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, ConstructionSiteId, TeamId, VehicleId, EmployeeId, Date FROM Assignments WHERE TeamId=@tid AND Date=@d LIMIT 1";
        cmd.Parameters.AddWithValue("@tid", teamId);
        cmd.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Assignment
        {
            Id = r.GetInt32(0),
            ConstructionSiteId = r.GetInt32(1),
            TeamId = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
            VehicleId = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
            EmployeeId = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
            Date = DateTime.Parse(r.GetString(5))
        };
    }

    public bool IsEmployeeAssigned(int employeeId, DateTime date, int? excludeAssignmentId = null)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Assignments WHERE EmployeeId=@eid AND Date=@d" +
            (excludeAssignmentId.HasValue ? " AND Id!=@eid2" : "");
        cmd.Parameters.AddWithValue("@eid", employeeId);
        cmd.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
        if (excludeAssignmentId.HasValue)
            cmd.Parameters.AddWithValue("@eid2", excludeAssignmentId.Value);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    public bool IsSiteAssigned(int siteId, DateTime date, int? excludeAssignmentId = null)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Assignments WHERE ConstructionSiteId=@sid AND Date=@d" +
            (excludeAssignmentId.HasValue ? " AND Id!=@eid" : "");
        cmd.Parameters.AddWithValue("@sid", siteId);
        cmd.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
        if (excludeAssignmentId.HasValue)
            cmd.Parameters.AddWithValue("@eid", excludeAssignmentId.Value);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    // True only if an assignment with the SAME site, team, vehicle AND employee already
    // exists on that day. A separate (e.g. overlapping/longer) assignment with a different
    // team or employee is allowed and must NOT be suppressed.
    public bool IsDuplicateAssignment(int siteId, int? teamId, int? vehicleId, int? employeeId, DateTime date, DayPart part = DayPart.Full)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM Assignments
            WHERE ConstructionSiteId=@sid AND Date=@d
              AND (TeamId IS NULL OR TeamId=@tid)
              AND (VehicleId IS NULL OR VehicleId=@vid)
              AND (EmployeeId IS NULL OR EmployeeId=@eid)
              AND (DayPart='F' OR @dp='F' OR DayPart=@dp)";
        cmd.Parameters.AddWithValue("@sid", siteId);
        cmd.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@tid", (object?)teamId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@vid", (object?)vehicleId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@eid", (object?)employeeId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@dp", part.ToCode());
        return (long)cmd.ExecuteScalar()! > 0;
    }

    public List<int> GetEmployeeTeamIds(int employeeId)
    {
        var list = new List<int>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TeamId FROM TeamMembers WHERE EmployeeId=@eid";
        cmd.Parameters.AddWithValue("@eid", employeeId);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetInt32(0));
        return list;
    }

    // --- Vacation & Sickness ---
    public List<Vacation> GetAllVacations()
    {
        var list = new List<Vacation>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT v.*, e.FirstName, e.LastName FROM Vacations v JOIN Employees e ON v.EmployeeId = e.Id ORDER BY v.StartDate";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Vacation
            {
                Id = r.GetInt32(0),
                EmployeeId = r.GetInt32(1),
                StartDate = DateTime.Parse(r.GetString(2)),
                EndDate = DateTime.Parse(r.GetString(3)),
                Notes = r.IsDBNull(4) ? null : r.GetString(4),
                Employee = new Employee { Id = r.GetInt32(1), FirstName = r.GetString(5), LastName = r.GetString(6) }
            });
        return list;
    }

    public List<Sickness> GetAllSickness()
    {
        var list = new List<Sickness>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT s.*, e.FirstName, e.LastName FROM Sickness s JOIN Employees e ON s.EmployeeId = e.Id ORDER BY s.StartDate";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Sickness
            {
                Id = r.GetInt32(0),
                EmployeeId = r.GetInt32(1),
                StartDate = DateTime.Parse(r.GetString(2)),
                EndDate = DateTime.Parse(r.GetString(3)),
                Notes = r.IsDBNull(4) ? null : r.GetString(4),
                Employee = new Employee { Id = r.GetInt32(1), FirstName = r.GetString(5), LastName = r.GetString(6) }
            });
        return list;
    }

    public void SaveVacation(Vacation v)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        if (v.Id == 0)
        {
            cmd.CommandText = "INSERT INTO Vacations (EmployeeId, StartDate, EndDate, Notes) VALUES (@e, @sd, @ed, @n); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@e", v.EmployeeId);
            cmd.Parameters.AddWithValue("@sd", v.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@ed", v.EndDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@n", (object?)v.Notes ?? DBNull.Value);
            v.Id = (int)(long)cmd.ExecuteScalar()!;
        }
        else
        {
            cmd.CommandText = "UPDATE Vacations SET EmployeeId=@e, StartDate=@sd, EndDate=@ed, Notes=@n WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", v.Id);
            cmd.Parameters.AddWithValue("@e", v.EmployeeId);
            cmd.Parameters.AddWithValue("@sd", v.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@ed", v.EndDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@n", (object?)v.Notes ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public void SaveSickness(Sickness s)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        if (s.Id == 0)
        {
            cmd.CommandText = "INSERT INTO Sickness (EmployeeId, StartDate, EndDate, Notes) VALUES (@e, @sd, @ed, @n); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@e", s.EmployeeId);
            cmd.Parameters.AddWithValue("@sd", s.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@ed", s.EndDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@n", (object?)s.Notes ?? DBNull.Value);
            s.Id = (int)(long)cmd.ExecuteScalar()!;
        }
        else
        {
            cmd.CommandText = "UPDATE Sickness SET EmployeeId=@e, StartDate=@sd, EndDate=@ed, Notes=@n WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", s.Id);
            cmd.Parameters.AddWithValue("@e", s.EmployeeId);
            cmd.Parameters.AddWithValue("@sd", s.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@ed", s.EndDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@n", (object?)s.Notes ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteVacation(int id)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Vacations WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteSickness(int id)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Sickness WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public bool IsEmployeeOnVacationOrSick(int employeeId, DateTime date)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM Vacations WHERE EmployeeId=@eid AND @d >= StartDate AND @d <= EndDate
            UNION ALL
            SELECT COUNT(*) FROM Sickness WHERE EmployeeId=@eid AND @d >= StartDate AND @d <= EndDate";
        cmd.Parameters.AddWithValue("@eid", employeeId);
        cmd.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
        using var r = cmd.ExecuteReader();
        while (r.Read())
            if (r.GetInt32(0) > 0) return true;
        return false;
    }

    // --- Export / Import ---
    public string ExportAll()
    {
        var dto = new ExportDto
        {
            Employees = GetAllEmployees(),
            Teams = GetAllTeams(),
            Vehicles = GetAllVehicles(),
            Sites = GetAllSites(),
            Assignments = GetAllAssignments(DateTime.MinValue, DateTime.MaxValue),
            Recurring = GetAllRecurringPatterns(),
            Vacations = GetAllVacations(),
            Sickness = GetAllSickness()
        };
        return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
    }

    public void ImportAll(string json)
    {
        var dto = JsonSerializer.Deserialize<ExportDto>(json);
        if (dto == null) return;
        using var conn = GetConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            // Stammdaten ergaenzen (kein Loeschen bestehender Daten).
            foreach (var e in dto.Employees ?? new()) if (e.Id == 0 || GetEmployeeOrNull(e.Id) == null) SaveEmployee(e);
            foreach (var t in dto.Teams ?? new()) if (t.Id == 0 || GetTeamOrNull(t.Id) == null) SaveTeam(t);
            foreach (var v in dto.Vehicles ?? new()) if (v.Id == 0 || GetVehicleOrNull(v.Id) == null) SaveVehicle(v);
            foreach (var s in dto.Sites ?? new()) if (s.Id == 0 || GetSiteOrNull(s.Id) == null) SaveSite(s);
            foreach (var a in dto.Assignments ?? new()) if (a.Id == 0 || !AssignmentExists(a.Id)) SaveAssignment(a);
            foreach (var p in dto.Recurring ?? new()) if (p.Id == 0) SaveRecurringPattern(p);
            foreach (var v in dto.Vacations ?? new()) if (v.Id == 0) SaveVacation(v);
            foreach (var s in dto.Sickness ?? new()) if (s.Id == 0) SaveSickness(s);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private Employee? GetEmployeeOrNull(int id)
    {
        try { return GetAllEmployees().FirstOrDefault(e => e.Id == id); } catch { return null; }
    }
    private Team? GetTeamOrNull(int id)
    {
        try { return GetAllTeams().FirstOrDefault(t => t.Id == id); } catch { return null; }
    }
    private Vehicle? GetVehicleOrNull(int id)
    {
        try { return GetAllVehicles().FirstOrDefault(v => v.Id == id); } catch { return null; }
    }
    private ConstructionSite? GetSiteOrNull(int id)
    {
        try { return GetAllSites().FirstOrDefault(s => s.Id == id); } catch { return null; }
    }
    private bool AssignmentExists(int id)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Assignments WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        return (long)cmd.ExecuteScalar()! > 0;
    }
}

public class ExportDto
{
    public List<Employee>? Employees { get; set; }
    public List<Team>? Teams { get; set; }
    public List<Vehicle>? Vehicles { get; set; }
    public List<ConstructionSite>? Sites { get; set; }
    public List<Assignment>? Assignments { get; set; }
    public List<RecurringPattern>? Recurring { get; set; }
    public List<Vacation>? Vacations { get; set; }
    public List<Sickness>? Sickness { get; set; }
}
