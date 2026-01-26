using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BGSBot.Services
{
    public class FactionAutocompleteProvider : AutocompleteHandler
    {

        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
            IInteractionContext context,
            IAutocompleteInteraction interaction,
            IParameterInfo parameter,
            IServiceProvider services)
        {
            var database = services.GetRequiredService<DatabaseService>();
            var input = interaction.Data.Current.Value?.ToString() ?? "";
            using var db = database.NewDatabaseContext();

            var factionList = await db.Factions.Select(x => x.Name).Distinct().ToListAsync();
            var factions = factionList
                .Where(f => f != null && f.ToLower().StartsWith(input.ToLower()))
                .OrderBy(f => f)
                .Take(5)
                .Select(f => new AutocompleteResult(f, f));

            return AutocompletionResult.FromSuccess(factions);
        }
    }
}
