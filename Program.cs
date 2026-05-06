using Microsoft.Extensions.Configuration;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq; // Using Newtonsoft as requested
using System.Net.Http;

public class Program
{
    private DiscordSocketClient _client;
    private IConfiguration _config; // Moved to class level so all methods can see it
    private string _igdbToken;
    private readonly HttpClient _http = new HttpClient();

    public static Task Main(string[] args) => new Program().MainAsync();

    public async Task MainAsync()
    {
        // 1. Setup Configuration
        _config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        string token = _config["DiscordToken"];
        string igdbId = _config["IgdbClientId"];
        string igdbSecret = _config["IgdbClientSecret"];

        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("Error: No DiscordToken found in User Secrets!");
            return;
        }

        // 2. Authenticate with IGDB before the bot starts
        await GetIgdbTokenAsync(igdbId, igdbSecret);

        // 3. Setup Discord Client
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        });

        _client.Log += Log;
        _client.MessageReceived += HandleCommandAsync;

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        // Keep the app running
        await Task.Delay(-1);
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private async Task HandleCommandAsync(SocketMessage message)
    {
        if (message.Author.IsBot) return;

        var parts = message.Content.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string command = parts[0];

        // Use 'command' instead of the full message content
        if (command == "!xbox" || command == "!ps5" || command == "!switch")
        {
            // 1. Determine Platform ID
            int platformId = command switch
            {
                "!xbox" => 169,
                "!ps5" => 167,
                "!switch" => 130,
                _ => 6 // Default to PC if something goes wrong
            };

            // 2. Handle the Limit
            int limit = 5;
            if (parts.Length > 1 && int.TryParse(parts[1], out int userLimit))
            {
                limit = Math.Clamp(userLimit, 1, 15);
            }

            // 3. Fetch Data
            var rawJson = await GetUpcomingGamesWithLimitAsync(_config["IgdbClientId"], platformId, limit);
            var games = JArray.Parse(rawJson);
            string resultList = "";

            // 4. Build the List with Dates
            foreach (var game in games)
            {
                string name = game["name"]?.ToString();

                // Convert the Unix Timestamp to a readable date
                if (long.TryParse(game["first_release_date"]?.ToString(), out long timestamp))
                {
                    var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).ToString("MMM dd, yyyy");
                    resultList += $"- **{name}** — _{date}_\n";
                }
                else
                {
                    resultList += $"- **{name}**\n";
                }
            }

            if (string.IsNullOrEmpty(resultList)) resultList = "No upcoming releases found.";

            // Uppercase the platform name for the title
            string platformName = command.Replace("!", "").ToUpper();
            await message.Channel.SendMessageAsync($"**Upcoming {platformName} Games:**\n{resultList}");
        }
    }

    private async Task GetIgdbTokenAsync(string clientId, string clientSecret)
    {
        var url = $"https://id.twitch.tv/oauth2/token?client_id={clientId}&client_secret={clientSecret}&grant_type=client_credentials";
        var response = await _http.PostAsync(url, null);
        var content = await response.Content.ReadAsStringAsync();

        var json = JObject.Parse(content);
        _igdbToken = json["access_token"]?.ToString();

        Console.WriteLine("Successfully Authenticated with IGDB!");
    }

    private async Task<string> GetUpcomingGamesWithLimitAsync(string clientId, int platformId, int limit)
    {
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("Client-ID", clientId);
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_igdbToken}");

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string query = $"fields name, first_release_date; where platforms = ({platformId}) & first_release_date > {now}; sort first_release_date asc; limit {limit};";

        var response = await _http.PostAsync("https://api.igdb.com/v4/games", new StringContent(query));
        return await response.Content.ReadAsStringAsync();
    }
}