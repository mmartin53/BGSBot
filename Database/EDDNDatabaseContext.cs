using BGSBot.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

            modelBuilder.Entity<Faction>().Property(x => x.ActiveStates)
                                          .HasConversion(
                                              x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                                              x => JsonSerializer.Deserialize<string[]>(x, (JsonSerializerOptions?)null)
                                          )
                                          .HasColumnType("TEXT");

            modelBuilder.Entity<ActiveFaction>().Property(x => x.ActiveStates)
                                                .HasConversion(
                                                    x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                                                    x => JsonSerializer.Deserialize<string[]>(x, (JsonSerializerOptions?)null)
                                                )
                                                .HasColumnType("TEXT");

            modelBuilder.Entity<Faction>().Property(x => x.PendingStates)
                                          .HasConversion(
                                              x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                                              x => JsonSerializer.Deserialize<string[]>(x, (JsonSerializerOptions?)null)
                                          )
                                          .HasColumnType("TEXT");

            modelBuilder.Entity<ActiveFaction>().Property(x => x.PendingStates)
                                                .HasConversion(
                                                    x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                                                    x => JsonSerializer.Deserialize<string[]>(x, (JsonSerializerOptions?)null)
                                                )
                                                .HasColumnType("TEXT");

            modelBuilder.Entity<Guild>().Property(x => x.Systems)
                                        .HasConversion(
                                            x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                                            x => JsonSerializer.Deserialize<string[]>(x, (JsonSerializerOptions?)null) ?? Array.Empty<string>()
                                        )
                                        .HasColumnType("TEXT");


            modelBuilder.Entity<EDSystem>().HasMany(x => x.Factions)
                                           .WithOne(x => x.System)
                                           .HasForeignKey(x => x.SystemFK)
                                           .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ActiveEDSystem>().HasMany(x => x.Factions)
                                                 .WithOne(x => x.System)
                                                 .HasForeignKey(x => x.SystemFK)
                                                 .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EDSystem>().HasMany(x => x.Conflicts)
                                           .WithOne(x => x.SystemID)
                                           .HasForeignKey(x => x.JournalMessageID)
                                           .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ActiveEDSystem>().HasMany(x => x.Conflicts)
                                                 .WithOne(x => x.SystemID)
                                                 .HasForeignKey(x => x.JournalMessageID)
                                                 .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<Conflict>().HasOne(x => x.Faction1)
                                           .WithMany()
                                           .HasForeignKey(x => x.Faction1ID)
                                           .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Conflict>().HasOne(x => x.Faction2)
                                           .WithMany()
                                           .HasForeignKey(x => x.Faction2ID)
                                           .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ActiveConflict>().HasOne(x => x.Faction1)
                                                 .WithMany()
                                                 .HasForeignKey(x => x.Faction1ID)
                                                 .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ActiveConflict>().HasOne(x => x.Faction2)
                                                 .WithMany()
                                                 .HasForeignKey(x => x.Faction2ID)
                                                 .OnDelete(DeleteBehavior.Restrict);
        }

        public void AddSystem(EDDNDeserializer.Root json, Action<ActiveEDSystem, Guild, EDSystem> responder)
        {
            var system = new EDSystem
            {
                StarSystem = json.Message.StarSystem,
                Timestamp = json.Message.timestamp,
                X = json.Message.StarPos[0],
                Y = json.Message.StarPos[1],
                Z = json.Message.StarPos[2]
            };
            var existingEntry = EDSystems.Include(x => x.Factions)   
                                         .Include(x => x.Conflicts) 
                                         .FirstOrDefault(x => x.StarSystem == json.Message.StarSystem);
            if (existingEntry != null)
            {
                if (DateTime.Compare(existingEntry.Timestamp, system.Timestamp) >= 0) return;
                if (existingEntry.Conflicts.Count > 0) Conflicts.RemoveRange(existingEntry.Conflicts);
                existingEntry.Timestamp = json.Message.timestamp;
                existingEntry.X = json.Message.StarPos[0];
                existingEntry.Y = json.Message.StarPos[1];
                existingEntry.Z = json.Message.StarPos[2];
                system = existingEntry;
            }

            var factionList = new List<Faction>();
            var systemFactions = existingEntry?.Factions.ToDictionary(x => x.Name) ?? [];
            foreach (EDDNDeserializer.Faction f in json.Message.Factions)
            {
                
                if (systemFactions.TryGetValue(f.Name, out var existingF))
                {
                    existingF.FactionState = f.FactionState;
                    existingF.Influence = f.Influence;
                    existingF.ActiveStates = f.ActiveStates?.Where(x => !string
                                                            .IsNullOrEmpty(x.Name))
                                                            .Select(x => x.Name!)
                                                            .ToArray() ?? Array.Empty<string>();
                    existingF.PendingStates = f.PendingStates?.Where(x => !string
                                                              .IsNullOrEmpty(x.Name))
                                                              .Select(x => x.Name!)
                                                              .ToArray() ?? Array.Empty<string>();
                    existingF.Allegiance = f.Allegiance;
                    factionList.Add(existingF);
                }
                else
                {
                    var faction = new Faction
                    {
                        Name = f.Name,
                        FactionState = f.FactionState,
                        Influence = f.Influence,
                        System = system,
                        Allegiance = f.Allegiance,
                        ActiveStates = f.ActiveStates?.Where(x => !string
                                                      .IsNullOrEmpty(x.Name))
                                                      .Select(x => x.Name!)
                                                      .ToArray() ?? Array.Empty<string>(),
                        PendingStates = f.PendingStates?.Where(x => !string
                                                        .IsNullOrEmpty(x.Name))
                                                        .Select(x => x.Name!)
                                                        .ToArray() ?? Array.Empty<string>()
                    };
                    factionList.Add(faction);
                    Factions.Add(faction);
                }
                
            }
            if (existingEntry != null)
            {
                foreach (Faction f in existingEntry.Factions)
                {
                    if (!factionList.Contains(f)) Factions.RemoveRange(f);
                }
            }

            if (json.Message.Conflicts != null)
            {
                foreach (EDDNDeserializer.Conflict c in json.Message.Conflicts)
                {
                    var f1 = factionList.First(x => x.Name == c.Faction1.Name);
                    var f2 = factionList.First(x => x.Name == c.Faction2.Name);
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
            if (existingEntry == null) EDSystems.Add(system);
            SaveChanges();
            if (existingEntry == null) Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} Added System: {system.StarSystem}");
            else Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} Updated System: {existingEntry.StarSystem}");
            
            var isActive = ActiveEDSystems.Include(x => x.Factions)
                                          .Include(x => x.Conflicts)
                                          .FirstOrDefault(x => x.StarSystem == json.Message.StarSystem);
            if (isActive != null)
            {
                var guilds = Guilds.AsNoTracking().ToList().Where(x => x.Systems.Contains(isActive.StarSystem));
                foreach (Guild guild in guilds)
                {
                    responder(isActive, guild, system);
                }
                UpdateData(system.StarSystem);
            }
        }

        public void TrackSystem(string faction, string system)
        {
            List<string> systemList;
            if (system == "All Systems")
            {
                systemList = Factions.Where(x => x.Name == faction)
                                     .Select(x => x.System.StarSystem)
                                     .ToList();
            }
            else systemList = new List<string> { system };
            foreach (var s in systemList)
            {
                UpdateData(s);
            }
        }

        public void UpdateData(string system)
        {
            var existingEntry = ActiveEDSystems.Include(x => x.Factions)
                                               .Include(x => x.Conflicts)
                                               .FirstOrDefault(x => x.StarSystem == system);
            if (existingEntry != null && existingEntry.Conflicts.Count > 0)
            {
                ActiveConflicts.RemoveRange(existingEntry.Conflicts);
            }
            var dbSystem = EDSystems.Include(x => x.Factions)
                                    .Include(x => x.Conflicts)
                                    .First(x => x.StarSystem == system);

            var trackSystem = new ActiveEDSystem
            {
                StarSystem = dbSystem.StarSystem,
                Timestamp = dbSystem.Timestamp,
                X = dbSystem.X,
                Y = dbSystem.Y,
                Z = dbSystem.Z
            };
            if (existingEntry != null)
            {
                existingEntry.Timestamp = trackSystem.Timestamp;
                existingEntry.X = dbSystem.X;
                existingEntry.Y = dbSystem.Y;
                existingEntry.Z = dbSystem.Z;
            }

            var existingFactions = existingEntry?.Factions.ToDictionary(f => f.Name) ?? new Dictionary<string, ActiveFaction>();
            var factionList = new List<ActiveFaction>();
            
            foreach (Faction f in dbSystem.Factions)
            {
                if (existingFactions.TryGetValue(f.Name, out var existingF))
                {
                    existingF.FactionState = f.FactionState;
                    existingF.Influence = f.Influence;
                    existingF.ActiveStates = f.ActiveStates;
                    existingF.PendingStates = f.PendingStates;
                    existingF.Allegiance = f.Allegiance;
                    factionList.Add(existingF);
                }
                else
                {
                    var trackFaction = new ActiveFaction
                    {
                        Name = f.Name,
                        FactionState = f.FactionState,
                        Influence = f.Influence,
                        System = trackSystem,
                        ActiveStates = f.ActiveStates,
                        PendingStates = f.PendingStates,
                        Timestamp = trackSystem.Timestamp,
                        Allegiance = f.Allegiance
                    };
                    ActiveFactions.Add(trackFaction);
                    factionList.Add(trackFaction);
                }
            }    
            if (existingEntry != null)
            {
                foreach (ActiveFaction f in existingEntry.Factions)
                {
                    if (!factionList.Contains(f)) ActiveFactions.RemoveRange(f);
                }
            }

            foreach (Conflict c in dbSystem.Conflicts)
            {
                var f1 = factionList.First(x => x.Name == c.Faction1.Name);
                var f2 = factionList.First(x => x.Name == c.Faction2.Name);
                var conflict = new ActiveConflict
                {
                    Status = c.Status,
                    WarType = c.WarType,
                    SystemID = existingEntry ?? trackSystem,
                    Faction1 = f1,
                    Faction2 = f2,
                    F1Stake = c.F1Stake,
                    F1WonDays = c.F1WonDays,
                    F2Stake = c.F2Stake,
                    F2WonDays = c.F2WonDays
                };
                ActiveConflicts.Add(conflict);
            }
            if (existingEntry == null) ActiveEDSystems.Add(trackSystem);
            SaveChanges();
        }

        public void AddGuild(string GuildID, string faction, string system, string roleID, string channelID)
        {
            TrackSystem(faction, system);
            var existing = Guilds.FirstOrDefault(x => x.GuildID == GuildID && x.Faction == faction);
            HashSet<string> systemList = existing != null ? existing.Systems.ToHashSet() : new HashSet<string>();

            if (system == "All Systems")
            {
                var activeSystems = ActiveFactions.Where(f => f.Name == faction).Select(f => f.System.StarSystem);
                foreach (var s in activeSystems) systemList.Add(s);
            }
            else
            {
                systemList.Add(system);
            }

            if (existing != null)
            {
                existing.RoleID = roleID;
                existing.TextChannelID = channelID;
                existing.Systems = systemList.ToArray();
            }
            else
            {
                var guild = new Guild
                { 
                    GuildID = GuildID,
                    RoleID = roleID,
                    TextChannelID = channelID,
                    Faction = faction,
                    Systems = systemList.ToArray()
                };
                Guilds.Add(guild);
            }
            SaveChanges();
        }
    }
}
