using System.Text.Json;
using BGSBot.Services;
using Microsoft.EntityFrameworkCore;

namespace BGSBot.Database
{
    public class EDDNDatabaseContext : DatabaseContext
    {
        public EDDNDatabaseContext(string location) : base(location) { }    

        public DbSet<EDSystem> EDSystems => Set<EDSystem>();
        public DbSet<ActiveEDSystem> ActiveEDSystems => Set<ActiveEDSystem>();

        public DbSet<Faction> Factions => Set<Faction>();
        public DbSet<ActiveFaction> ActiveFactions => Set<ActiveFaction>();

        public DbSet<Conflict> Conflicts => Set<Conflict>();
        public DbSet<ActiveConflict> ActiveConflicts => Set<ActiveConflict>();

        public DbSet<Guild> Guilds => Set<Guild>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Faction>()
                .Property(x => x.ActiveStates)
                .HasConversion(
                    x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                    x => JsonSerializer.Deserialize<string[]>(x, (JsonSerializerOptions?)null)
                )
                .HasColumnType("TEXT");
            modelBuilder.Entity<ActiveFaction>()
                .Property(x => x.ActiveStates)
                .HasConversion(
                    x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                    x => JsonSerializer.Deserialize<string[]>(x, (JsonSerializerOptions?)null)
                )
                .HasColumnType("TEXT");
            modelBuilder.Entity<Guild>()
                .Property(x => x.Systems)
                .HasConversion(
                    x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                    x => JsonSerializer.Deserialize<string[]>(x, (JsonSerializerOptions?)null) ?? Array.Empty<string>()
                )
                .HasColumnType("TEXT");


            modelBuilder.Entity<EDSystem>()
                .HasMany(x => x.Factions)
                .WithOne(x => x.SystemID)
                .HasForeignKey(x => x.JournalMessageID)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ActiveEDSystem>()
                .HasMany(x => x.Factions)
                .WithOne(x => x.SystemID)
                .HasForeignKey(x => x.JournalMessageID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EDSystem>()
                .HasMany(x => x.Conflicts)
                .WithOne(x => x.SystemID)
                .HasForeignKey(x => x.JournalMessageID)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ActiveEDSystem>()
                .HasMany(x => x.Conflicts)
                .WithOne(x => x.SystemID)
                .HasForeignKey(x => x.JournalMessageID)
                .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<Conflict>()
                .HasOne(x => x.Faction1)
                .WithMany()
                .HasForeignKey(x => x.Faction1ID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Conflict>()
                .HasOne(x => x.Faction2)
                .WithMany()
                .HasForeignKey(x => x.Faction2ID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ActiveConflict>()
                .HasOne(x => x.Faction1)
                .WithMany()
                .HasForeignKey(x => x.Faction1ID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ActiveConflict>()
                .HasOne(x => x.Faction2)
                .WithMany()
                .HasForeignKey(x => x.Faction2ID)
                .OnDelete(DeleteBehavior.Restrict);
        }


        public void AddSystem(EDDNDeserializer.Root json)
        {
            var system = new EDSystem
            {
                StarSystem = json.Message.StarSystem,
                Timestamp = json.Message.timestamp
            };
            var existingEntry = EDSystems.FirstOrDefault(x => x.StarSystem == json.Message.StarSystem);
            if (existingEntry != null)
            {
                if (DateTime.Compare(existingEntry.Timestamp, system.Timestamp) >= 0) return;
                EDSystems.RemoveRange(existingEntry);
                foreach (var f in existingEntry.Factions) Factions.RemoveRange(f);
                if (existingEntry.Conflicts != null)
                {
                    foreach (var c in existingEntry.Conflicts) Conflicts.RemoveRange(c);
                }
                SaveChanges();
            }

            List<Faction> factionList = new List<Faction>();
            foreach (EDDNDeserializer.Faction f in json.Message.Factions)
            {
                var faction = new Faction
                {
                    Name = f.Name,
                    FactionState = f.FactionState,
                    Influence = f.Influence,
                    SystemID = system,
                    ActiveStates = f.ActiveStates?.Where(x => !string.IsNullOrEmpty(x.Name)).Select(x => x.Name!).ToArray() ?? Array.Empty<string>()
                };
                factionList.Add(faction);
                Factions.Add(faction);
            }

            if (json.Message.Conflicts != null)
            {
                foreach (EDDNDeserializer.Conflict c in json.Message.Conflicts)
                {
                    var f1 = factionList.FirstOrDefault(x => x.Name == c.Faction1.Name);
                    var f2 = factionList.FirstOrDefault(x => x.Name == c.Faction2.Name);
                    var conflict = new Conflict
                    {
                        Status = c.Status,
                        WarType = c.WarType,
                        SystemID = system,
                        Faction1 = f1,
                        Faction2 = f2,
                        F1Stake = c.Faction1.Stake,
                        F1WonDays = c.Faction1.WonDays,
                        F2Stake = c.Faction2.Stake,
                        F2WonDays = c.Faction2.WonDays
                    };
                    Conflicts.Add(conflict);
                }
            }
            EDSystems.Add(system);
            SaveChanges();
            if (existingEntry == null)
            {
                Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} Added System: {system.StarSystem}");
            }
            else Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} Updated System: {system.StarSystem}");

                var isActive = ActiveEDSystems.FirstOrDefault(x => x.StarSystem == system.StarSystem);
            if (isActive != null)
            {
                // do something to ping stuff
                UpdateData(system.StarSystem);
            }
        }

        public void TrackSystem(string faction, string system)
        {
            if (system == "All Systems")
            {
                List<string> systemList = new List<string>();
                foreach (Faction f in Factions.Include(x => x.SystemID))
                {
                    if (f.Name == faction) systemList.Add(f.SystemID.StarSystem);
                }
                foreach (string s in systemList)
                {
                    UpdateData(s);
                }
            }
            else
            {
                UpdateData(system);
            }
        }

        public void UpdateData(string system)
        {
            var existingEntry = ActiveEDSystems.FirstOrDefault(x => x.StarSystem == system);
            if (existingEntry != null)
            {
                ActiveEDSystems.RemoveRange(existingEntry);
                foreach (var f in existingEntry.Factions) ActiveFactions.RemoveRange(f);
                if (existingEntry.Conflicts != null)
                {
                    foreach (var c in existingEntry.Conflicts) ActiveConflicts.RemoveRange(c);
                }
                SaveChanges();
            }
            var dbSystem = EDSystems.FirstOrDefault(x => x.StarSystem == system);
            var dbFactions = new List<Faction>();
            var dbConflicts = new List<Conflict>();
            foreach (var f in Factions)
            {
                if (f.SystemID == dbSystem) dbFactions.Add(f);
            }
            foreach (var c in Conflicts)
            {
                if (c.Faction1.SystemID == dbSystem && c.Faction2.SystemID == dbSystem) dbConflicts.Add(c);
            }

            var trackSystem = new ActiveEDSystem
            {
                StarSystem = dbSystem.StarSystem,
                Timestamp = dbSystem.Timestamp
            };

            var factionList = new List<ActiveFaction>();
            foreach (Faction f in dbFactions)
            {
                var trackFaction = new ActiveFaction
                {
                    Name = f.Name,
                    FactionState = f.FactionState,
                    Influence = f.Influence,
                    SystemID = trackSystem,
                    ActiveStates = f.ActiveStates
                };
                ActiveFactions.Add(trackFaction);
                factionList.Add(trackFaction);
            }

            foreach (Conflict c in dbConflicts)
            {
                var f1 = factionList.FirstOrDefault(x => x.Name == c.Faction1.Name);
                var f2 = factionList.FirstOrDefault(x => x.Name == c.Faction2.Name);
                var conflict = new ActiveConflict
                {
                    Status = c.Status,
                    WarType = c.WarType,
                    SystemID = trackSystem,
                    Faction1 = f1,
                    Faction2 = f2,
                    F1Stake = c.F1Stake,
                    F1WonDays = c.F1WonDays,
                    F2Stake = c.F2Stake,
                    F2WonDays = c.F2WonDays
                };
                ActiveConflicts.Add(conflict);
            }
            ActiveEDSystems.Add(trackSystem);
            SaveChanges();
        }

        public void AddGuild(string GuildID, string faction, string system, string roleID, string channelID)
        {
            var systemList = new List<string>();
            var existingList = Guilds.Where(x => x.GuildID == GuildID);
            var existing = existingList.FirstOrDefault(x => x.Faction == faction);
            if (existing != null)
            {
                systemList = existing.Systems.ToList();
                if (system == "All Systems")
                {
                    foreach (ActiveFaction f in ActiveFactions.Include(x => x.SystemID))
                    {
                        if (f.Name == faction)
                        {
                            systemList.Add(f.SystemID.StarSystem);
                        }
                    }
                }
                else systemList.Add(system);
                var systemArray = systemList.ToArray();
                var updatedGuild = existing;
                updatedGuild.Systems = systemArray;
                Entry(existing).CurrentValues.SetValues(updatedGuild);
                SaveChanges();
                return;
            }
            
            if (system == "All Systems")
            {
                foreach (ActiveFaction f in ActiveFactions.Include(x => x.SystemID))
                {
                    if (f.Name == faction) 
                    {
                        systemList.Add(f.SystemID.StarSystem);
                    }
                }
            }
            else systemList.Add(system);
            var systemArray2 = systemList.ToArray();
            var guild2 = new Guild
            {
                GuildID = GuildID,
                RoleID = roleID,
                TextChannelID = channelID,
                Faction = faction,
                Systems = systemArray2
            };
            Guilds.Add(guild2);
            SaveChanges();
        }
    }
}
