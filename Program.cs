using System.Net.Sockets;
using System.IO;
using Microsoft.Extensions.Configuration;
using Discord;
using Discord.WebSocket;

public class Program
{
    private DiscordSocketClient _client;

    public static Task Main(string[] args) => new Program().MainAsync();

    public async Task MainAsync()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>() // Looks in that hidden folder on your PC
            .Build();

        // Read the token from the file

        string token = config["DiscordToken"];

        var _client = new DiscordSocketClient();

        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("Error: No token found in User Secrets!");
            return;
        }

        // Use the variable 'token' instead of a hardcoded string
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        await Task.Delay(-1);
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private async Task HandleCommandAsync(SocketMessage message)
    {
        // Don't respond to other bots
        if (message.Author.IsBot) return;

        if (message.Content.ToLower() == "!xbox")
        {
            await message.Channel.SendMessageAsync("Fetching upcoming Xbox games for 2026...");
            // This is where we will hook into the IGDB API next
        }
    }

}
