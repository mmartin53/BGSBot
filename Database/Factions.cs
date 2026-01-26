using BGSBot.Database;
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
        public ulong? JournalMessageID { get; set; }
    }

    [Table("Factions")]
    public class Faction : FactionBase
    {
        [ForeignKey(nameof(JournalMessageID))]
        public required EDSystem SystemID { get; set; }
    }

    [Table("ActiveFactions")]
    public class ActiveFaction : FactionBase
    {
        [ForeignKey(nameof(JournalMessageID))]
        public required ActiveEDSystem SystemID { get; set; }
    }
}