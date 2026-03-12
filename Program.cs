using BGSBot.Services;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace BGSBot
{
    class Program
    {
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var services = ConfigureServices();
            var client = services.GetRequiredService<DiscordSocketClient>();

            client.Log += LogAsync;
            services.GetRequiredService<CommandService>().Log += LogAsync;
            services.GetRequiredService<InteractionService>().Log += LogAsync;

            var sr = new StreamReader("C:\\Discord\\Token2.txt");
            var token = sr.ReadLine();
            sr.Close();
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            client.Ready += async () =>
            {
                await services.GetRequiredService<DiscordService>().InitializeAsync();
            };

            await Task.Delay(-1);
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }

        private IServiceProvider ConfigureServices()
        {
            var config = new DiscordSocketConfig
            {
                MessageCacheSize = 100,
                GatewayIntents = GatewayIntents.AllUnprivileged
            };
            return new ServiceCollection().AddSingleton(new DiscordSocketClient(config))
                                          .AddSingleton<CommandService>()
                                          .AddSingleton<DiscordService>()
                                          .AddSingleton<EDDNDeserializer>()
                                          .AddSingleton<DatabaseService>()
                                          .AddSingleton<AutocompleteHandler, FactionAutocomplete>()
                                          .AddSingleton<AutocompleteHandler, SystemAutoComplete>()
                                          .AddSingleton(provider => new InteractionService(provider.GetRequiredService<DiscordSocketClient>()))
                                          .BuildServiceProvider();
        }
    }
}
