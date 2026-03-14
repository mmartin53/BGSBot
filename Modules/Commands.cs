using BGSBot.Database;
using BGSBot.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace BGSBot.Modules
{
    public class Commands : InteractionModuleBase
    {
        public required DatabaseService DatabaseService { get; set; }

        [SlashCommand("addfaction", "Add your faction to be tracked by the bot")]
        public async Task AddFactionAsync([Autocomplete(typeof(FactionAutocomplete))] string faction)
        {
            using var db = DatabaseService.NewDatabaseContext();
            var systemsPage = db.Factions.AsNoTracking()
                                         .Where(x => x.Name == faction)
                                         .Select(x => x.System.StarSystem)
                                         .OrderBy(x => x)
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
                                                .WithCustomId($"faction_select:{faction}:0")
                                                .WithPlaceholder("Choose a system...")
                                                .WithOptions(options))
                                                .WithButton("Next Page", $"faction_page:{faction}:1", ButtonStyle.Secondary, disabled: !hasNext);

            await RespondAsync(
                embed: new EmbedBuilder().WithTitle("Select a System")
                                         .WithDescription($"Select a system from the dropdown below")
                                         .Build(),
                components: builder.Build(), ephemeral: true);
        }
        
        

        [SlashCommand("deletealldata", "remove your server from the system")]
        public async Task RemoveAllDataAsync()
        {
            if (Context.User is not SocketGuildUser guildUser ||
                !(guildUser.GuildPermissions.Administrator || guildUser.GuildPermissions.ManageGuild || 
                  guildUser.GuildPermissions.ManageRoles || guildUser.GuildPermissions.ManageChannels))
            {
                await RespondAsync("You don't have the permission to delete data. \n(Administrator or \"Manage Server/Roles/Channels\"", ephemeral: true);
            }
            using var db = DatabaseService.NewDatabaseContext();
            var guilds = await db.Guilds.AsNoTracking().Where(x => x.GuildID == Context.Guild.Id.ToString()).ToListAsync();
            if (guilds.Count == 0)
            {
                await RespondAsync("No tracked entities found.", ephemeral: true);
                return;
            }
            var trackedSystems = guilds.SelectMany(x => x.Systems)
                                       .Distinct()
                                       .ToList();
            var systemsTBD = await db.ActiveEDSystems.AsNoTracking()
                                                     .Where(x => trackedSystems.Contains(x.StarSystem))
                                                     .ToListAsync();
            var otherGuildSystems = db.Guilds.AsNoTracking()
                                             .Where(x => x.GuildID != Context.Guild.Id.ToString())
                                             .AsEnumerable()
                                             .SelectMany(x => x.Systems)
                                             .ToHashSet();
            systemsTBD = systemsTBD.Where(x => !otherGuildSystems.Contains(x.StarSystem)).ToList();
            var systemsCount = systemsTBD.Count;

            var builder = new ComponentBuilder().WithButton("Delete", $"confirm_delete:{Context.Guild.Id.ToString()}", ButtonStyle.Danger)
                                                .WithButton("Cancel", "cancel_delete", ButtonStyle.Secondary);

            await RespondAsync(
                $"You are about to remove tracking for {systemsCount} system(s)\n" +
                "Click **Delete** to proceed, or **Cancel** to abort.",
                components: builder.Build(),
                ephemeral: true
            );
        }
    }
}
