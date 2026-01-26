using BGSBot.Database;
using BGSBot.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace BGSBot.Modules
{
    public class Commands : InteractionModuleBase
    {
        public required DatabaseService DatabaseService { get; set; }

        [SlashCommand("addfaction", "Add your faction to be tracked by the bot")]
        public async Task AddFactionAsync([Autocomplete(typeof(FactionAutocompleteProvider))]string faction)
        {
            List<string> systemOptions = new List<string>();
            List<ulong?> IDList = new List<ulong?>();
            systemOptions.Add("All Systems");
            using var db = DatabaseService.NewDatabaseContext();
            foreach (Faction f in db.Factions)
            {
                if (faction == f.Name) IDList.Add(f.JournalMessageID);
            }
            foreach (ulong? id in IDList)
            {
                var system = db.EDSystems.FirstOrDefault(x => x.ID == id);
                systemOptions.Add(system.StarSystem);
            }
            var sortedSystems = new List<string> { systemOptions[0] }
            .Concat(systemOptions.Skip(1).OrderBy(x => x)).ToList();

            var options = sortedSystems
            .Take(25)
            .Select(f => new SelectMenuOptionBuilder()
                .WithLabel(f)
                .WithValue(f)
            )
            .ToList();

            var builder = new ComponentBuilder();
            builder.WithSelectMenu(new SelectMenuBuilder()
                   .WithCustomId($"faction_select|{faction}")
                   .WithPlaceholder("Choose a system...")
                   .WithOptions(options));

            await Context.Interaction.RespondAsync(
               embed : new EmbedBuilder()
                    .WithTitle("Select a System")
                    .WithDescription("Select a system from the dropdown below")
                    .Build(),
                components: builder.Build(), ephemeral: true
            );
        }
    }
}
