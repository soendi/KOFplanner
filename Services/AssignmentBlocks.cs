using KOFplanner.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KOFplanner.Services;

// Fasst Einsatz-Tageszeilen zu zusammenhaengenden Bloecken zusammen.
// Mehrere Tageszeilen mit gleicher Baustelle + Team + Fahrzeug +
// Mitarbeiter an lueckenlos aufeinanderfolgenden Tagen = EIN Einsatz.
public static class AssignmentBlocks
{
    public sealed class Block
    {
        public Assignment Rep { get; init; } = null!;
        public DateTime First { get; init; }
        public DateTime Last { get; init; }
        public int Days => (Last - First).Days + 1;
    }

    public static List<Block> Build(List<Assignment> assignments)
    {
        var blocks = new List<Block>();
        var groups = assignments
            .GroupBy(a => (a.ConstructionSiteId, a.TeamId, a.VehicleId, a.EmployeeId));

        foreach (var g in groups)
        {
            var days = g.Select(a => a.Date.Date).Distinct().OrderBy(d => d).ToList();
            int i = 0;
            while (i < days.Count)
            {
                int start = i;
                while (i + 1 < days.Count && days[i + 1] == days[i].AddDays(1)) i++;
                var first = days[start];
                var last = days[i];
                var rep = g.First(a => a.Date.Date == first);
                blocks.Add(new Block { Rep = rep, First = first, Last = last });
                i++;
            }
        }
        return blocks;
    }
}
