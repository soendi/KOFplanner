using Microsoft.Data.Sqlite;
using KOFplanner.Models;

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
        cmd.CommandText = "SELECT * FROM Vehicles ORDER BY Category, VehicleNumber";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Vehicle { Id = r.GetInt32(0), RequiredLicense = r.GetString(1), VehicleNumber = r.GetString(2), LicensePlate = r.GetString(3) });
        return list;
    }

    public void SaveVehicle(Vehicle v)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        if (v.Id == 0)
        {
            cmd.CommandText = "INSERT INTO Vehicles (Category, VehicleNumber, LicensePlate) VALUES (@cat, @vn, @lp); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cat", v.RequiredLicense);
            cmd.Parameters.AddWithValue("@vn", v.VehicleNumber);
            cmd.Parameters.AddWithValue("@lp", v.LicensePlate);
            v.Id = (int)(long)cmd.ExecuteScalar()!;
        }
        else
        {
            cmd.CommandText = "UPDATE Vehicles SET Category=@cat, VehicleNumber=@vn, LicensePlate=@lp WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", v.Id);
            cmd.Parameters.AddWithValue("@cat", v.RequiredLicense);
            cmd.Parameters.AddWithValue("@vn", v.VehicleNumber);
            cmd.Parameters.AddWithValue("@lp", v.LicensePlate);
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
                EndDate = r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4))
            });
        return list;
    }

    public void SaveSite(ConstructionSite s)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        if (s.Id == 0)
        {
            cmd.CommandText = "INSERT INTO ConstructionSites (Name, Location, StartDate, EndDate) VALUES (@n, @l, @sd, @ed); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@n", s.Name);
            cmd.Parameters.AddWithValue("@l", s.Address);
            cmd.Parameters.AddWithValue("@sd", s.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@ed", s.EndDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
            s.Id = (int)(long)cmd.ExecuteScalar()!;
        }
        else
        {
            cmd.CommandText = "UPDATE ConstructionSites SET Name=@n, Location=@l, StartDate=@sd, EndDate=@ed WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", s.Id);
            cmd.Parameters.AddWithValue("@n", s.Name);
            cmd.Parameters.AddWithValue("@l", s.Address);
            cmd.Parameters.AddWithValue("@sd", s.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@ed", s.EndDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
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
            SELECT a.*, cs.Name, cs.Location, cs.StartDate, cs.EndDate,
                   t.Name, v.Category, v.VehicleNumber, v.LicensePlate,
                   e.FirstName, e.LastName
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
                    EndDate = r.IsDBNull(9) ? null : DateTime.Parse(r.GetString(9))
                }
            };
            if (!r.IsDBNull(10))
                a.Team = new Team { Id = a.TeamId ?? 0, Name = r.GetString(10) };
            if (!r.IsDBNull(11))
                a.Vehicle = new Vehicle { Id = a.VehicleId ?? 0, RequiredLicense = r.GetString(11), VehicleNumber = r.GetString(12), LicensePlate = r.GetString(13) };
            if (!r.IsDBNull(14))
                a.Employee = new Employee { Id = a.EmployeeId ?? 0, FirstName = r.GetString(14), LastName = r.GetString(15) };
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
            SELECT a.*, cs.Name, cs.Location,
                   t.Name, v.Category, v.VehicleNumber, v.LicensePlate,
                   e.FirstName, e.LastName
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
            SELECT a.*, cs.Name, cs.Location, cs.StartDate, cs.EndDate,
                   t.Name, v.Category, v.VehicleNumber, v.LicensePlate,
                   e.FirstName, e.LastName
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
                Site = new ConstructionSite { Id = r.GetInt32(1), Name = r.GetString(6), Address = r.IsDBNull(7) ? "" : r.GetString(7), StartDate = DateTime.Parse(r.GetString(8)), EndDate = r.IsDBNull(9) ? null : DateTime.Parse(r.GetString(9)) }
            };
            if (!r.IsDBNull(10))
                a.Team = new Team { Id = a.TeamId ?? 0, Name = r.GetString(10) };
            if (!r.IsDBNull(11))
                a.Vehicle = new Vehicle { Id = a.VehicleId ?? 0, RequiredLicense = r.GetString(11), VehicleNumber = r.GetString(12), LicensePlate = r.GetString(13) };
            if (!r.IsDBNull(14))
                a.Employee = new Employee { Id = a.EmployeeId ?? 0, FirstName = r.GetString(14), LastName = r.GetString(15) };
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
            SELECT a.*, cs.Name, cs.Location, cs.StartDate, cs.EndDate,
                   t.Name, v.Category, v.VehicleNumber, v.LicensePlate,
                   e.FirstName, e.LastName
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
                Site = new ConstructionSite { Id = r.GetInt32(1), Name = r.GetString(6), Address = r.IsDBNull(7) ? "" : r.GetString(7), StartDate = DateTime.Parse(r.GetString(8)), EndDate = r.IsDBNull(9) ? null : DateTime.Parse(r.GetString(9)) }
            };
            if (!r.IsDBNull(10))
                a.Team = new Team { Id = a.TeamId ?? 0, Name = r.GetString(10) };
            if (!r.IsDBNull(11))
                a.Vehicle = new Vehicle { Id = a.VehicleId ?? 0, RequiredLicense = r.GetString(11), VehicleNumber = r.GetString(12), LicensePlate = r.GetString(13) };
            if (!r.IsDBNull(14))
                a.Employee = new Employee { Id = a.EmployeeId ?? 0, FirstName = r.GetString(14), LastName = r.GetString(15) };
            list.Add(a);
        }
        return list;
    }

    public void SaveAssignment(Assignment a)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        if (a.Id == 0)
        {
            cmd.CommandText = "INSERT INTO Assignments (ConstructionSiteId, TeamId, VehicleId, EmployeeId, Date) VALUES (@cs, @t, @v, @e, @d); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@cs", a.ConstructionSiteId);
            cmd.Parameters.AddWithValue("@t", (object?)a.TeamId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@v", (object?)a.VehicleId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@e", (object?)a.EmployeeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@d", a.Date.ToString("yyyy-MM-dd"));
            a.Id = (int)(long)cmd.ExecuteScalar()!;
        }
        else
        {
            cmd.CommandText = "UPDATE Assignments SET ConstructionSiteId=@cs, TeamId=@t, VehicleId=@v, EmployeeId=@e, Date=@d WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", a.Id);
            cmd.Parameters.AddWithValue("@cs", a.ConstructionSiteId);
            cmd.Parameters.AddWithValue("@t", (object?)a.TeamId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@v", (object?)a.VehicleId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@e", (object?)a.EmployeeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@d", a.Date.ToString("yyyy-MM-dd"));
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
}
