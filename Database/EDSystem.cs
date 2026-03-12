using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace BGSBot.Database
{
    [Table("EDSystems")]
    [Index(nameof(StarSystem), IsUnique = true)]
    public class EDSystem
    {
        public ulong ID { get; set; }
        public required string StarSystem { get; set; }
        public DateTime Timestamp { get; set; }

        public ICollection<Faction> Factions { get; set; } = new List<Faction>();
        public ICollection<Conflict> Conflicts { get; set; } = new List<Conflict>();

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    [Table("ActiveEDSystems")]
    [Index(nameof(StarSystem), IsUnique = true)]
    public class ActiveEDSystem
    {
        public ulong ID { get; set; }
        public required string StarSystem { get; set; }
        public DateTime Timestamp { get; set; }

        public ICollection<ActiveFaction> Factions { get; set; } = new List<ActiveFaction>();
        public ICollection<ActiveConflict> Conflicts { get; set; } = new List<ActiveConflict>();

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
}
