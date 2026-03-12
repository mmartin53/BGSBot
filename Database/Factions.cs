using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BGSBot.Database
{
    public abstract class FactionBase
    {
        [Column("ID")]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public ulong ID { get; set; }

        [Column("Name")]
        public required string Name { get; set; }

        [Column("FactionState")]
        public required string FactionState { get; set; }

        [Column("Influence")]
        public double Influence { get; set; }

        [Column("ActiveStates")]
        public string[]? ActiveStates { get; set; }

        [Column("SystemID")]
        public ulong SystemFK { get; set; }

        [Column("PendingStates")]
        public string[]? PendingStates { get; set; }

        [Column("Allegiance")]
        public string? Allegiance { get; set; }
    }

    [Table("Factions")]
    [Index(nameof(Name), nameof(SystemFK))]
    public class Faction : FactionBase
    {
        [ForeignKey(nameof(SystemFK))]
        public required EDSystem System { get; set; }
    }

    [Table("ActiveFactions")]
    [Index(nameof(Name), nameof(SystemFK))]
    public class ActiveFaction : FactionBase
    {
        [ForeignKey(nameof(SystemFK))]
        public required ActiveEDSystem System { get; set; }

        [Column("LastChecked")]
        public DateTime Timestamp { get; set; }
    }
}