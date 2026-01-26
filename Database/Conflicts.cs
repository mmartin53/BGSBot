using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BGSBot.Database
{
    public abstract class ConflictBase
    {
        [Column("ID")]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public ulong ID { get; set; }

        [Column("Status")]
        public required string Status { get; set; }

        [Column("WarType")]
        public required string WarType { get; set; }

        [Column("SystemID")]
        public ulong JournalMessageID { get; set; }

        [Column("Faction1ID")]
        public ulong Faction1ID { get; set; }

        [Column("Faction2ID")]
        public ulong Faction2ID { get; set; }

        [Column("Faction1Stake")]
        public required string F1Stake { get; set; }

        [Column("Faction1WonDays")]
        public int F1WonDays { get; set; }

        [Column("Faction2Stake")]
        public required string F2Stake { get; set; }

        [Column("Faction2WonDays")]
        public int F2WonDays { get; set; }
    }

    [Table("Conflicts")]
    public class Conflict : ConflictBase
    {
        [ForeignKey(nameof(JournalMessageID))]
        public EDSystem SystemID { get; set; } = null!;

        [ForeignKey(nameof(Faction1ID))]
        public required Faction Faction1 { get; set; }

        [ForeignKey(nameof(Faction2ID))]
        public required Faction Faction2 { get; set; }
    }

    [Table("ActiveConflicts")]
    public class ActiveConflict : ConflictBase
    {
        [ForeignKey(nameof(JournalMessageID))]
        public ActiveEDSystem SystemID { get; set; } = null!;

        [ForeignKey(nameof(Faction1ID))]
        public required ActiveFaction Faction1 { get; set; }

        [ForeignKey(nameof(Faction2ID))]
        public required ActiveFaction Faction2 { get; set; }
    }
}