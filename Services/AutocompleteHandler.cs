using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BGSBot.Services
{
    public class FactionAutocomplete : AutocompleteHandler
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

            var factions = await db.Factions.AsNoTracking()
                                            .Where(x => EF.Functions.Like(x.Name, $"{input}%"))
                                            .Select(x => x.Name)
                                            .Distinct()
                                            .OrderBy(x => x)
                                            .Take(5)
                                            .ToListAsync();
            var result = factions.Select(x => new AutocompleteResult(x, x));

            return AutocompletionResult.FromSuccess(result);
        }
    }
    
    public class SystemAutoComplete : AutocompleteHandler
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

            var systems = await db.EDSystems.AsNoTracking()
                                            .Where(x => EF.Functions.Like(x.StarSystem, $"{input}%"))
                                            .Select(x => x.StarSystem)
                                            .Distinct()
                                            .OrderBy(x => x)
                                            .Take(5)
                                            .ToListAsync();
            var result = systems.Select(x => new AutocompleteResult(x, x));

            return AutocompletionResult.FromSuccess(result);
        }
    }
}
