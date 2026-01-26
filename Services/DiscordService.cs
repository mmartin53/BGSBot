using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Ionic.Zlib;
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
            await _interactionService.AddModulesGloballyAsync(true, modules.ToArray());
            EDDNCollector();
        }

        private async Task InteractionCreated(SocketInteraction arg)
        {
            await _interactionService.ExecuteCommandAsync(new SocketInteractionContext(_discord, arg), _services);
            try
            {
                if (arg is SocketMessageComponent component)
                {
                    if (component.Data.CustomId.StartsWith("faction_select|"))
                    {
                        var faction = component.Data.CustomId.Split('|')[1];
                        var systemName = component.Data.Values.First();
                        using var database = _database.NewDatabaseContext();
                        database.TrackSystem(faction, systemName);
                        await SendRoleSelectionEmbed(component, 0, faction, systemName);
                    }
                    if (component.Data.CustomId.StartsWith("role_select:"))
                    {
                        var faction = component.Data.CustomId.Split(':')[1];
                        var system = component.Data.CustomId.Split(':')[2];
                        var selectedRole = component.Data.Values.First();
                        await SendChannelSelectionEmbed(component, 0, faction, system, selectedRole);
                    }
                    if (component.Data.CustomId.StartsWith("channel_select:"))
                    {
                        var guildID = arg.GuildId.ToString();
                        var faction = component.Data.CustomId.Split(':')[1];
                        var system = component.Data.CustomId.Split(':')[2];
                        var roleID = component.Data.CustomId.Split(":")[3];
                        var channelID = component.Data.Values.First();
                        BGSBotCreator(guildID, faction, system, roleID, channelID);
                        await component.RespondAsync("Data added successfully.", ephemeral: true);

                    }
                    if (component.Data.CustomId.StartsWith("role_next:"))
                    {
                        int page = int.Parse(component.Data.CustomId.Split(':')[1]) + 1;
                        var faction = component.Data.CustomId.Split(':')[2];
                        var system = component.Data.CustomId.Split(':')[3];
                        await SendRoleSelectionEmbed(component, page, faction, system);
                    }
                    if (component.Data.CustomId.StartsWith("role_prev:"))
                    {
                        int page = int.Parse(component.Data.CustomId.Split(':')[1]) - 1;
                        var faction = component.Data.CustomId.Split(':')[2];
                        var system = component.Data.CustomId.Split(':')[3];
                        await SendRoleSelectionEmbed(component, page, faction, system);
                    }
                    if (component.Data.CustomId.StartsWith("channel_next:"))
                    {
                        int page = int.Parse(component.Data.CustomId.Split(':')[1]) + 1;
                        var faction = component.Data.CustomId.Split(':')[2];
                        var system = component.Data.CustomId.Split(':')[3];
                        var roleID = component.Data.CustomId.Split(":")[4];
                        await SendChannelSelectionEmbed(component, page, faction, system, roleID);
                    }
                    if (component.Data.CustomId.StartsWith("channel_prev:"))
                    {
                        int page = int.Parse(component.Data.CustomId.Split(':')[1]) - 1;
                        var faction = component.Data.CustomId.Split(':')[2];
                        var system = component.Data.CustomId.Split(':')[3];
                        var roleID = component.Data.CustomId.Split(":")[4];
                        await SendChannelSelectionEmbed(component, page, faction, system, roleID);
                    }

                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        public void EDDNCollector()
        {
            Task.Run(async () =>
            {
                var utf8 = new UTF8Encoding();
                using var database = _database.NewDatabaseContext();
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
                                        if (JSON != null) database.AddSystem(JSON);
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

        public async Task SendRoleSelectionEmbed(SocketMessageComponent component, int page, string faction, string system)
        {
            var guild = (component.User as SocketGuildUser)?.Guild;
            if (guild == null) return;

            var roles = guild.Roles.OrderBy(r => r.Name).ToList();

            int pageSize = 25;
            int skipCount = page * (pageSize - 1);

            var pageRoles = roles.Skip(skipCount).Take(page == 0 ? pageSize - 1 : pageSize).ToList();

            var options = new List<SelectMenuOptionBuilder>
            {
                new SelectMenuOptionBuilder()
                    .WithLabel("No role")
                    .WithValue("No role")
            };

            options.AddRange(pageRoles.Select(r => new SelectMenuOptionBuilder()
                .WithLabel(r.Name)
                .WithValue(r.Id.ToString())
            ));

            var roleMenu = new SelectMenuBuilder()
                .WithCustomId($"role_select:{faction}:{system}")
                .WithPlaceholder("Select a role to ping (if any)")
                .WithOptions(options);

            var builder = new ComponentBuilder()
                .WithSelectMenu(roleMenu);

            if (page > 0)
                builder.WithButton("Previous Page", $"role_prev:{page}:{faction}:{system}", ButtonStyle.Secondary);

            if ((page + 1) * 25 < roles.Count)
                builder.WithButton("Next Page", $"role_next:{page}:{faction}:{system}", ButtonStyle.Secondary);

            // Send ephemeral message in the guild channel
            await component.UpdateAsync(msg =>
            {
                msg.Embed = new EmbedBuilder()
                    .WithTitle("Select a Role")
                    .WithDescription($"Page {page + 1} of roles")
                    .Build();
                msg.Components = builder.Build();
            });
        }

        public async Task SendChannelSelectionEmbed(SocketMessageComponent component, int page, string faction, string system, string roleID)
        {
            var guild = (component.User as SocketGuildUser)?.Guild;
            if (guild == null) return;

            var channels = guild.TextChannels.OrderBy(x => x.Name);

            var pageRoles = channels.Skip(page * 25).Take(25).ToList();

            var options = pageRoles
                .Select(r => new SelectMenuOptionBuilder()
                .WithLabel(r.Name)
                .WithValue(r.Id.ToString()))
                .ToList();

            var roleMenu = new SelectMenuBuilder()
                .WithCustomId($"channel_select:{faction}:{system}:{roleID}")
                .WithPlaceholder("Select a text channel")
                .WithOptions(options);

            var builder = new ComponentBuilder()
                .WithSelectMenu(roleMenu);

            if (page > 0)
                builder.WithButton("Previous Page", $"channel_prev:{page}:{faction}:{system}:{roleID}", ButtonStyle.Secondary);

            if ((page + 1) * 25 < channels.Count())
                builder.WithButton("Next Page", $"channel_next:{page}:{faction}:{system}:{roleID}", ButtonStyle.Secondary);

            // Send ephemeral message in the guild channel
            await component.UpdateAsync(msg =>
            {
                msg.Embed = new EmbedBuilder()
                    .WithTitle("Select a Role")
                    .WithDescription($"Page {page + 1} of channels")
                    .Build();
                msg.Components = builder.Build();
            });
        }

        public void BGSBotCreator(string guildID, string faction, string system, string roleID, string channelID)
        {
            using var db = _database.NewDatabaseContext();
            db.AddGuild(guildID, faction, system, roleID, channelID);
        }
    }
}
