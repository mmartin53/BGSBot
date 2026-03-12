using BGSBot.Database;
using Discord;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Ionic.Zlib;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetMQ;
using NetMQ.Sockets;
using System.Reflection;
using System.Text;

namespace BGSBot.Services
{
    public class DiscordService
    {
        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _services;
        private readonly InteractionService _interactionService;
        private readonly EDDNDeserializer _EDDND;
        private readonly DatabaseService _database;

        public DiscordService(IServiceProvider services)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _interactionService = services.GetRequiredService<InteractionService>();
            _EDDND = services.GetRequiredService<EDDNDeserializer>();
            _database = services.GetRequiredService<DatabaseService>();
            _services = services;

            _discord.InteractionCreated += InteractionCreated;
        }

        public async Task InitializeAsync()
        {
            var modules = await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            try
            {
                await _interactionService.AddModulesGloballyAsync(true, modules.ToArray());
            }
            catch (HttpException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            EDDNCollector();
        }

        public void EDDNCollector()
        {
            Task.Run(async () =>
            {
                var utf8 = new UTF8Encoding();
                while (true)
                {
                    try
                    {
                        using (var client = new SubscriberSocket())
                        {
                            client.Options.ReceiveHighWatermark = 1000;
                            client.Connect("tcp://eddn.edcd.io:9500");
                            client.SubscribeToAnyTopic();
                            while (true)
                            {
                                if (client.TryReceiveFrameBytes(TimeSpan.FromSeconds(5), out var bytes))
                                {
                                    var uncompressed = ZlibStream.UncompressBuffer(bytes);
                                    var result = utf8.GetString(uncompressed);
                                    if (result.Contains("https://eddn.edcd.io/schemas/journal/1")
                                    && (result.Contains("\"event\": \"FSDJump\"")
                                     || result.Contains("\"event\": \"CarrierJump\""))
                                     && result.Contains("Factions"))
                                    {
                                        var JSON = _EDDND.Deserializer(result);
                                        using var db = _database.NewDatabaseContext();
                                        if (JSON != null) db.AddSystem(JSON, BGSResponder);
                                    }
                                }
                                else break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(e.StackTrace);
                        await Task.Delay(5000);
                    }
                }
            });
        }

        private async Task InteractionCreated(SocketInteraction arg)
        {
            await _interactionService.ExecuteCommandAsync(new SocketInteractionContext(_discord, arg), _services);
            try
            {
                if (arg is SocketMessageComponent component)
                {
                    if (component.Data.CustomId.StartsWith("faction_select:"))
                    {
                        var faction = component.Data.CustomId.Split(':')[1];
                        var systemName = component.Data.Values.First();
                        await SendRolePage(component, 0, faction, systemName);
                    }

                    if (component.Data.CustomId.StartsWith("role_select:"))
                    {
                        var faction = component.Data.CustomId.Split(':')[1];
                        var system = component.Data.CustomId.Split(':')[2];
                        var selectedRole = component.Data.Values.First();
                        await SendChannelPage(component, 0, faction, system, selectedRole);
                    }

                    if (component.Data.CustomId.StartsWith("channel_select:"))
                    {
                        var guildID = arg.GuildId.ToString();
                        var faction = component.Data.CustomId.Split(':')[1];
                        var system = component.Data.CustomId.Split(':')[2];
                        var roleID = component.Data.CustomId.Split(":")[3];
                        var channelID = component.Data.Values.First();
                        using var db = _database.NewDatabaseContext();
                        db.AddGuild(guildID, faction, system, roleID, channelID);
                        await component.UpdateAsync(msg =>
                        {
                            msg.Content = "Data added successfully.";
                            msg.Embeds = Array.Empty<Embed>();
                            msg.Components = new ComponentBuilder().Build();
                        });
                    }

                    if (component.Data.CustomId.StartsWith("confirm_delete:"))
                    {
                        using var db = _database.NewDatabaseContext();
                        var guildID = component.Data.CustomId.Split(':')[1];
                        var guilds = await db.Guilds.Where(x => x.GuildID == guildID).ToListAsync();
                        var trackedSystems = guilds.SelectMany(x => x.Systems).Distinct().ToList();
                        var systemsTBD = await db.ActiveEDSystems.Include(x => x.Factions)
                                                                 .Include(x => x.Conflicts)
                                                                 .Where(x => trackedSystems.Contains(x.StarSystem))
                                                                 .ToListAsync();
                        var otherGuildSystems = db.Guilds.Where(x => x.GuildID != guildID)
                                                         .AsEnumerable()
                                                         .SelectMany(x => x.Systems)
                                                         .ToHashSet();
                        systemsTBD = systemsTBD.Where(x => !otherGuildSystems.Contains(x.StarSystem)).ToList();
                        db.ActiveConflicts.RemoveRange(systemsTBD.SelectMany(x => x.Conflicts));
                        db.ActiveFactions.RemoveRange(systemsTBD.SelectMany(x => x.Factions));
                        db.ActiveEDSystems.RemoveRange(systemsTBD);
                        db.Guilds.RemoveRange(guilds);
                        await db.SaveChangesAsync();
                        await component.UpdateAsync(msg =>
                        {
                            msg.Content = $"Deleted all data.";
                            msg.Components = null;
                        });
                    }
                    if (component.Data.CustomId == "cancel_delete")
                    {
                        await component.UpdateAsync(msg =>
                        {
                            msg.Content = "Deletion cancelled.";
                            msg.Components = null;
                        });
                    }

                    if (component.Data.CustomId.StartsWith("faction_page:"))
                    {
                        var faction = component.Data.CustomId.Split(':')[1];
                        int page = int.Parse(component.Data.CustomId.Split(':')[2]);
                        await SendSystemPage(faction, page, component);
                    }

                    if (component.Data.CustomId.StartsWith("role_page:"))
                    {
                        int page = int.Parse(component.Data.CustomId.Split(':')[1]);
                        var faction = component.Data.CustomId.Split(':')[2];
                        var system = component.Data.CustomId.Split(':')[3];
                        await SendRolePage(component, page, faction, system);
                    }

                    if (component.Data.CustomId.StartsWith("channel_page:"))
                    {
                        int page = int.Parse(component.Data.CustomId.Split(':')[1]);
                        var faction = component.Data.CustomId.Split(':')[2];
                        var system = component.Data.CustomId.Split(':')[3];
                        var roleID = component.Data.CustomId.Split(":")[4];
                        await SendChannelPage(component, page, faction, system, roleID);
                    }

                    

                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private async Task SendSystemPage(string faction, int page, SocketInteraction interaction)
        {
            using var db = _database.NewDatabaseContext();
            var systemsPage = db.Factions.AsNoTracking()
                                         .Where(x => x.Name == faction)
                                         .Select(x => x.System.StarSystem)
                                         .OrderBy(x => x)
                                         .Skip(page * 24)
                                         .Take(25)
                                         .ToList();
            bool hasNext = systemsPage.Count > 24;
            if (hasNext) systemsPage.RemoveAt(24);
            systemsPage.Insert(0, "All Systems");

            var options = systemsPage
            .Select(x => new SelectMenuOptionBuilder()
            .WithLabel(x)
            .WithValue(x))
            .ToList();

            var builder = new ComponentBuilder().WithSelectMenu(new SelectMenuBuilder()
                                                .WithCustomId($"faction_select:{faction}:{page}")
                                                .WithPlaceholder("Choose a system...")
                                                .WithOptions(options));
            if (page != 0) builder.WithButton("Previous Page", $"faction_page:{faction}:{page - 1}", ButtonStyle.Secondary);
            if (hasNext) builder.WithButton("Next Page", $"faction_page:{faction}:{page + 1}", ButtonStyle.Secondary);

            await ((SocketMessageComponent)interaction).UpdateAsync(msg =>
            {
                msg.Components = builder.Build();
                msg.Embed = new EmbedBuilder().WithTitle("Select a System")
                                              .WithDescription("Select a system from the dropdown below")
                                              .Build();
            });
        }

        public async Task SendRolePage(SocketMessageComponent component, int page, string faction, string system)
        {
            var guild = (component.User as SocketGuildUser)?.Guild;
            if (guild == null)
            {
                await component.UpdateAsync(msg =>
                {
                    msg.Content = "This command must be performed in a discord server.";
                    msg.Embeds = Array.Empty<Embed>();
                    msg.Components = new ComponentBuilder().Build();
                });
                return;
            }

            var roles = guild.Roles.OrderBy(r => r.Name).ToList();
            var pageRoles = roles.Skip(page * 24).Take(24).ToList();

            var options = new List<SelectMenuOptionBuilder>
            {
                new SelectMenuOptionBuilder().WithLabel("No role")
                                             .WithValue("No role")
            };

            options.AddRange(pageRoles.Select(r => new SelectMenuOptionBuilder()
                   .WithLabel(r.Name)
                   .WithValue(r.Id.ToString())));

            var roleMenu = new SelectMenuBuilder().WithCustomId($"role_select:{faction}:{system}")
                                                  .WithPlaceholder("Select a role to ping (if any)")
                                                  .WithOptions(options);

            var builder = new ComponentBuilder().WithSelectMenu(roleMenu);

            if (page > 0) builder.WithButton("Previous Page", $"role_page:{page - 1}:{faction}:{system}", ButtonStyle.Secondary);

            if ((page + 1) * 25 < roles.Count) builder.WithButton("Next Page", $"role_page:{page + 1}:{faction}:{system}", ButtonStyle.Secondary);

            await component.UpdateAsync(msg =>
            {
                msg.Embed = new EmbedBuilder()
                    .WithTitle("Select a Role")
                    .WithDescription($"Page {page + 1} of roles")
                    .Build();
                msg.Components = builder.Build();
            });
        }

        public async Task SendChannelPage(SocketMessageComponent component, int page, string faction, string system, string roleID)
        {
            var guild = (component.User as SocketGuildUser)?.Guild;
            if (guild == null) 
            {
                await component.UpdateAsync(msg =>
                {
                    msg.Content = "This command must be performed in a discord server.";
                    msg.Embeds = Array.Empty<Embed>();
                    msg.Components = new ComponentBuilder().Build();
                });
                return;
            }

            var channels = guild.TextChannels.OrderBy(x => x.Name);
            var pageChannels = channels.Skip(page * 25).Take(25).ToList();
            var options = pageChannels.Select(r => new SelectMenuOptionBuilder()
                                      .WithLabel(r.Name)
                                      .WithValue(r.Id.ToString()))
                                      .ToList();
            var roleMenu = new SelectMenuBuilder().WithCustomId($"channel_select:{faction}:{system}:{roleID}")
                                                  .WithPlaceholder("Select a text channel")
                                                  .WithOptions(options);
            var builder = new ComponentBuilder().WithSelectMenu(roleMenu);

            if (page > 0) builder.WithButton("Previous Page", $"channel_page:{page - 1}:{faction}:{system}:{roleID}", ButtonStyle.Secondary);

            if ((page + 1) * 25 < channels.Count()) builder.WithButton("Next Page", $"channel_page:{page + 1}:{faction}:{system}:{roleID}", ButtonStyle.Secondary);

            await component.UpdateAsync(msg =>
            {
                msg.Embed = new EmbedBuilder().WithTitle("Select a Role")
                                              .WithDescription($"Page {page + 1} of channels")
                                              .Build();
                msg.Components = builder.Build();
            });
        }

        public void BGSResponder(ActiveEDSystem oldSystem, Guild dbGuild, EDSystem newSystem)
        {
            Task.Run(async() =>
            {
                var conflictStates = new Dictionary<string, string>
                {
                    { "War", "War" },
                    { "Election", "Election" },
                    { "CivilWar", "Civil war" },
                };
                var stateNames = new Dictionary<string, string>
                {
                    { "Blight", "Blight" },
                    { "Boom", "Boom" },
                    { "Bust", "Bust" },
                    { "CivilLiberty", "Civil liberty" },
                    { "CivilUnrest", "Civil unrest" },
                    { "CivilWar", "Civil war" },
                    { "Drought", "Drought" },
                    { "Election", "Election" },
                    { "Expansion", "Expansion" },
                    { "Famine", "Famine" },
                    { "InfrastructureFailure", "Infrastructure failure" },
                    { "Investment", "Investment" },
                    { "Lockdown", "Lockdown" },
                    { "NaturalDisaster", "Natural disaster" },
                    { "Outbreak", "Outbreak" },
                    { "PirateAttack", "Pirate attack" },
                    { "PublicHoliday", "Public holiday" },
                    { "Retreat", "Retreat" },
                    { "Terrorism", "Terrorist attack" },
                    { "War", "War" },
                };
                var oldFaction = oldSystem.Factions.First(x => x.Name == dbGuild.Faction);
                var newFaction = newSystem.Factions.First(x => x.Name == dbGuild.Faction);
                bool oldIsConflict = conflictStates.ContainsKey(oldFaction.FactionState);
                bool newIsConflict = conflictStates.ContainsKey(newFaction.FactionState);
                string output = "";
                if (dbGuild.RoleID != "No role") output = $"<@&{dbGuild.RoleID}>";
                if (oldIsConflict || newIsConflict)
                {
                    if (oldIsConflict && newIsConflict) // conflict ongoing
                    {
                        var oldConflict = oldSystem.Conflicts.First(x => x.Faction1.Name == oldFaction.Name || x.Faction2.Name == oldFaction.Name);
                        var newConflict = newSystem.Conflicts.First(x => x.Faction1.Name == newFaction.Name || x.Faction2.Name == newFaction.Name);
                        if (oldConflict.F1WonDays == newConflict.F1WonDays && oldConflict.F2WonDays == newConflict.F2WonDays) goto Next;

                        bool isfaction1 = false;
                        if (oldConflict.Faction1.Name == oldFaction.Name) isfaction1 = true;
                        var oppFaction = isfaction1 ? newConflict.Faction2 : newConflict.Faction1;
                        var oldAllyWonDays = isfaction1 ? oldConflict.F1WonDays : oldConflict.F2WonDays;
                        var newAllyWonDays = isfaction1 ? newConflict.F1WonDays : newConflict.F2WonDays;
                        var newOppWonDays = isfaction1 ? newConflict.F2WonDays : newConflict.F1WonDays;
                        bool weWon = (newAllyWonDays > oldAllyWonDays);

                        output += $"\nDay {newAllyWonDays + newOppWonDays}/7 of {conflictStates[newFaction.FactionState].ToLower()} in {newConflict.SystemID.StarSystem}:\n";
                        if (weWon) output += $"{newFaction.Name} has won the day. The score is now {newAllyWonDays}:{newOppWonDays}";
                        if (!weWon) output += $"{oppFaction.Name} has won the day. The score is now {newAllyWonDays}:{newOppWonDays}";
                    }

                    if (!oldIsConflict && newIsConflict) // conflict has started
                    {
                        var conflict = newSystem.Conflicts.First(x => x.Faction1.Name == newFaction.Name || x.Faction2.Name == newFaction.Name);
                        bool isfaction1 = false;
                        if (conflict.Faction1.Name == newFaction.Name) isfaction1 = true;
                        var oppFaction = isfaction1 ? conflict.Faction2 : conflict.Faction1;
                        string fa1Stake = conflict.F1Stake, fa2Stake = conflict.F2Stake;
                        if (fa1Stake == "") fa1Stake = "nothing";
                        if (fa2Stake == "") fa2Stake = "nothing";

                        output += $"\n{conflictStates[newFaction.FactionState]} has begun between {newFaction.Name} and {oppFaction.Name} in {conflict.SystemID.StarSystem}." +
                                  $"\nYour Faction is staking {(isfaction1 ? fa1Stake : fa2Stake)} " + 
                                  $"while the enemy is staking {(isfaction1 ? fa2Stake : fa1Stake)}";
                    }

                    if (oldIsConflict && !newIsConflict) // conflict has ended
                    {
                        var conflict = oldSystem.Conflicts.First(x => x.Faction1.Name == oldFaction.Name || x.Faction2.Name == oldFaction.Name);
                        bool isfaction1 = false;
                        if (conflict.Faction1.Name == oldFaction.Name) isfaction1 = true;
                        var oppFaction = isfaction1 ? conflict.Faction2 : conflict.Faction1;
                        var allyWonDays = isfaction1 ? conflict.F1WonDays : conflict.F2WonDays;
                        var oppWonDays = isfaction1 ? conflict.F2WonDays : conflict.F1WonDays;
                        string fa1Stake = conflict.F1Stake, fa2Stake = conflict.F2Stake;
                        if (fa1Stake == "") fa1Stake = "nothing";
                        if (fa2Stake == "") fa2Stake = "nothing";
                        bool weWon = (newFaction.Influence > oldFaction.Influence);
                        output += $"\nThe {conflictStates[oldFaction.FactionState].ToLower()} has ended in {conflict.SystemID.StarSystem}.\n";
                        if (weWon)
                        {
                            var stake = isfaction1 ? fa2Stake : fa1Stake;
                            if (stake == "nothing")
                            {
                                output += $"{oldFaction.Name} has achieved victory over {oppFaction.Name}";
                            }
                            else output += $"{oldFaction.Name} has achieved victory over {oppFaction.Name} and has taken control of {stake}.";
                        }
                        else
                        {
                            var stake = isfaction1 ? fa1Stake : fa2Stake;
                            if (stake == "nothing")
                            {
                                output += $"{oppFaction.Name} has achieved victory over {oldFaction.Name}";
                            }
                            else output += $"{oppFaction.Name} has achieved victory over {oldFaction.Name} and has taken control of {(isfaction1 ? conflict.F1Stake : conflict.F2Stake)}.";
                        }
                    }
                }

                Next:
                if (newFaction.Influence < 0.1 && (newSystem.Timestamp - oldFaction.Timestamp).TotalDays > 1 && !newIsConflict) // low influence ping, 1/day
                {
                    using var db = _database.NewDatabaseContext();
                    var faction = db.ActiveFactions.First(x => x.Name == newFaction.Name && x.System.StarSystem == newSystem.StarSystem);
                    faction.Timestamp = newSystem.Timestamp;
                    db.SaveChanges();
                    output += $"\n{newFaction.Name}'s influence is getting low in {newSystem.StarSystem}. Curently at: {newFaction.Influence:P}";
                }

                if (oldFaction.FactionState != newFaction.FactionState && !newIsConflict) // General state change message
                {
                    var stateString = stateNames.ContainsKey(newFaction.FactionState) ?
                                      stateNames[newFaction.FactionState] : newFaction.FactionState;
                    output += $"\n{newFaction.Name} has entered into a new state in {newSystem.StarSystem}.\nNew state is: {stateString}";
                }

                if (newFaction.PendingStates != null && newFaction.PendingStates.Length > 0) // pending states
                {
                    var oldPending = oldFaction.PendingStates ?? Array.Empty<string>();
                    var newPending = newFaction.PendingStates ?? Array.Empty<string>();
                    bool stateChange = newPending.Any(s => !oldPending.Contains(s));

                    if (stateChange)
                    {
                        output += $"\nA new state is pending in {newSystem.StarSystem}: ";
                        foreach (string s in newPending) 
                        {
                            var stateString = stateNames.ContainsKey(s) ? stateNames[s] : s;
                            output += $"{stateString} ";
                        }
                    }
                }
                
                var textchannel = _discord.GetGuild(Convert.ToUInt64(dbGuild.GuildID))
                                          .GetTextChannel(Convert.ToUInt64(dbGuild.TextChannelID));
                if (output != $"<@&{dbGuild.RoleID}>" && output != "") await textchannel.SendMessageAsync(output);
            });
        }
    }
}
