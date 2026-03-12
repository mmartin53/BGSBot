using Newtonsoft.Json;

namespace BGSBot.Services
{
    public class EDDNDeserializer
    {
        public class Root
        {
            [JsonProperty("$schemaRef")]
            public required string SchemaRef { get; set; }
            public Header? Header { get; set; }
            public required JournalMessage Message { get; set; }
        }

        public class Header
        {
            public string? gamebuild { get; set; }
            public string? gameversion { get; set; }
            public DateTime gatewayTimestamp { get; set; }
            public string? softwareName { get; set; }
            public string? softwareVersion { get; set; }
            public string? uploaderID { get; set; }
        }

        public class JournalMessage
        {
            public string? Body { get; set; }
            public int BodyID { get; set; }
            public string? BodyType { get; set; }
            public Conflict[]? Conflicts { get; set; }
            public string? ControllingPower { get; set; }
            public required Faction[] Factions { get; set; }
            public long Population { get; set; }
            public string? PowerplayState { get; set; }
            public double PowerplayStateControlProgress { get; set; }
            public double PowerplayStateReinforcement { get; set; }
            public double PowerplayStateUndermining { get; set; }
            public string[]? Powers { get; set; }
            public required double[] StarPos { get; set; }
            public required string StarSystem { get; set; }
            public long SystemAddress { get; set; }
            public string? SystemAllegiance { get; set; }
            public string? SystemEconomy { get; set; }
            public SystemFaction? SystemFaction { get; set; }
            public string? SystemGovernment { get; set; }
            public string? SystemSecondEconomy { get; set; }
            public string? SystemSecurity { get; set; }
            [JsonProperty("event")]
            public string? Event { get; set; }
            public bool horizons { get; set; }
            public bool odyssey { get; set; }
            public DateTime timestamp { get; set; }
        }

        public class Conflict
        {
            public required ConflictFaction Faction1 { get; set; }
            public required ConflictFaction Faction2 { get; set; }
            public required string Status { get; set; }
            public required string WarType { get; set; }
        }

        public class ConflictFaction
        {
            public string? Name { get; set; }
            public required string Stake { get; set; }
            public int WonDays { get; set; }
        }

        public class Faction
        {
            public State[]? ActiveStates { get; set; }
            public required string Allegiance { get; set; }
            public required string FactionState { get; set; }
            public string? Government { get; set; }
            public string? Happiness { get; set; }
            public double Influence { get; set; }
            public required string Name { get; set; }

            public State[]? PendingStates { get; set; }
            public State[]? RecoveringStates { get; set; }
        }

        public class State
        {
            [JsonProperty("State")]
            public string? Name { get; set; }
            public int Trend { get; set; }
        }

        public class SystemFaction
        {
            public string? Name { get; set; }
        }

        public Root? Deserializer(string response)
        {
            return JsonConvert.DeserializeObject<Root>(response);
        }
    }
}
