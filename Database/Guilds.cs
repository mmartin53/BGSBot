using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BGSBot.Database {
    public class Guild
    {
        [Column("ID")]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public ulong ID { get; set; }

        [Column("GuildID")]
        public required string GuildID { get; set; }

        [Column("RoleID")]
        public required string RoleID { get; set; }

        [Column("TextChannelID")]
        public required string TextChannelID { get; set; }

        [Column("RelatedFaction")]
        public required string Faction { get; set; }

        [Column("RelatedSystem(s)")]
        public required string[] Systems { get; set; }
    }
}