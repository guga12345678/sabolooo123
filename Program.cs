using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace TutorialBot
{
    class Program
    {
        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _services;

        // Auto-delete and Safe Channel IDs
        private readonly List<ulong> AutoDeleteChannelIds = new List<ulong>
        {
            1466907225049010289,
            1466901460532072468
        };

        private readonly List<ulong> SafeChannelIds = new List<ulong>
        {
            1466537673505378548
        };

        // Number guessing game settings
        private const ulong NumberGuessChannelId = 1468014231856353340;
        private const ulong GameStarterRoleId = 1466924908217892892;
        private const ulong WinnerRoleId = 1466804645518246042;

        private bool _gameActive = false;
        private int _currentRandomNumber;
        private readonly Random _rng = new Random();
        private bool _channelLocked = false;

        static void Main(string[] args)
        {
            // 1. Load variables from .env file manually to avoid DLL errors
            LoadEnv(".env");

            new Program().RunBotAsync().GetAwaiter().GetResult();
        }

        private static void LoadEnv(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("Warning: .env file not found. Ensure it is named exactly '.env' and set to 'Copy if Newer'.");
                return;
            }

            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                Environment.SetEnvironmentVariable(key, value);
            }
            Console.WriteLine("Environment variables loaded successfully.");
        }

        public async Task RunBotAsync()
        {
            _client = new DiscordSocketClient();
            _commands = new CommandService();

            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .BuildServiceProvider();

            _client.Log += LogAsync;

            await RegisterCommandsAsync();

            // 2. Retrieve token from environment variable
            string token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Error: DISCORD_TOKEN is missing from .env file!");
                return;
            }

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log);
            return Task.CompletedTask;
        }

        public async Task RegisterCommandsAsync()
        {
            _client.MessageReceived += HandleMessageAsync;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        private async Task HandleMessageAsync(SocketMessage arg)
        {
            if (!(arg is SocketUserMessage message)) return;
            if (message.Author.IsBot) return;

            var channel = message.Channel as SocketTextChannel;

            if (SafeChannelIds.Contains(message.Channel.Id))
                return;

            // Number guessing game logic
            if (message.Channel.Id == NumberGuessChannelId)
            {
                var authorUser = message.Author as SocketGuildUser;

                if (_channelLocked)
                {
                    await message.DeleteAsync();
                    return;
                }

                if (!_gameActive && authorUser.Roles.Any(r => r.Id == GameStarterRoleId))
                {
                    _currentRandomNumber = _rng.Next(1, 201);
                    _gameActive = true;
                    _channelLocked = false;
                    await message.Channel.SendMessageAsync("ricxvis gamocnobis tamashi daiwyo! gamoicani ricxvi 1_dan 200_mde.");
                    return;
                }

                if (!_gameActive)
                {
                    await message.DeleteAsync();
                    return;
                }

                bool hasLetter = message.Content.Any(char.IsLetter);
                if (hasLetter)
                {
                    await message.DeleteAsync();
                    return;
                }

                if (int.TryParse(message.Content, out int guess))
                {
                    if (guess == _currentRandomNumber)
                    {
                        if (authorUser != null)
                        {
                            var role = channel.Guild.GetRole(WinnerRoleId);
                            if (role != null)
                            {
                                await authorUser.AddRoleAsync(role);
                                await message.Channel.SendMessageAsync($"{authorUser.Mention} gamoicno swori ricxvi da moigo VIP!");
                            }
                        }

                        _gameActive = false;
                        _channelLocked = true;

                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(60000);
                            _channelLocked = false;
                            await message.Channel.SendMessageAsync("chati mzataa axali tamashis dasawyebad!");
                        });

                        return;
                    }
                }
                return;
            }

            // Auto-delete logic
            if (AutoDeleteChannelIds.Contains(message.Channel.Id))
            {
                if (message.Attachments.Count == 0 && message.Embeds.Count == 0)
                {
                    await message.DeleteAsync();

                    string safeMentions = string.Join(", ", SafeChannelIds.Select(id => $"<#{id}>"));
                    var warningMessage = await message.Channel.SendMessageAsync($"aq nudebi chayaret mesijebistvis gamoiyenet {safeMentions}");

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(15000);
                        await warningMessage.DeleteAsync();
                    });
                    return;
                }
            }

            // Command handling
            int argPos = 0;
            if (message.HasStringPrefix("!", ref argPos))
            {
                var context = new SocketCommandContext(_client, message);
                var result = await _commands.ExecuteAsync(context, argPos, _services);

                if (!result.IsSuccess)
                    Console.WriteLine(result.ErrorReason);
            }
        }
    }
}