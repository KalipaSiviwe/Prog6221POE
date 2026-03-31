using CyberBuddy.Services;
using System.Reflection;

namespace CyberBuddy.Models
{
    public class Chatbot
    {
        private readonly ResponseService _responseService;
        private readonly ConsoleStyler _styler;
        private string _userName = "User";

        public Chatbot()
        {
            _responseService = new ResponseService();
            _styler = new ConsoleStyler();
        }

        public void Start()
        {
            Console.Title = "CyberBuddy";
            _styler.PrintHeader();
            PlayVoiceGreeting();
            AskUserName();
            ShowWelcome();
            ChatLoop();
            _styler.PrintGoodbye(_userName);
        }

        private void PlayVoiceGreeting()
        {
            string audioPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "greeting.wav");

            if (!File.Exists(audioPath))
            {
                _styler.PrintInfo("Voice greeting not found. Add your WAV file at: assets/greeting.wav");
                return;
            }

            // If not running on Windows, just skip audio playback.
            if (!OperatingSystem.IsWindows())
            {
                _styler.PrintWarning("Voice playback is only supported on Windows on this project.");
                return;
            }

            try
            {
                // Use reflection so the project can compile/run even when Windows Desktop Runtime isn't installed.
                // SoundPlayer class is in System.Media (Windows desktop stack).
                var windowsExtensions = Assembly.Load("System.Windows.Extensions");
                var soundPlayerType = windowsExtensions.GetType("System.Media.SoundPlayer", throwOnError: false);

                if (soundPlayerType == null)
                {
                    _styler.PrintWarning("Voice playback not available (SoundPlayer type missing).");
                    return;
                }

                object? player = Activator.CreateInstance(soundPlayerType, audioPath);
                if (player == null)
                {
                    _styler.PrintWarning("Voice playback not available (could not create SoundPlayer).");
                    return;
                }

                soundPlayerType.GetMethod("Load", Type.EmptyTypes)?.Invoke(player, null);
                soundPlayerType.GetMethod("PlaySync", Type.EmptyTypes)?.Invoke(player, null);
            }
            catch (Exception ex)
            {
                _styler.PrintWarning($"Could not play audio: {ex.Message}");
                _styler.PrintInfo("If you want voice to work, install .NET Windows Desktop Runtime 8.0.");
            }
        }

        private void AskUserName()
        {
            while (true)
            {
                _styler.TypeLine("Please enter your name: ", ConsoleColor.Cyan, 10);
                string? input = Console.ReadLine();

                if (!string.IsNullOrWhiteSpace(input))
                {
                    _userName = input.Trim();
                    break;
                }

                _styler.PrintWarning("Name cannot be empty. Please try again.");
            }
        }

        private void ShowWelcome()
        {
            _styler.PrintDivider();
            _styler.TypeLine($"Welcome, {_userName}! I am CyberBuddy.", ConsoleColor.Green, 12);
            _styler.TypeLine("Ask me about passwords, phishing, safe browsing, and more.", ConsoleColor.Green, 12);
            _styler.TypeLine("Type 'help' for options or 'exit' to quit.", ConsoleColor.Yellow, 12);
            _styler.PrintDivider();
        }

        private void ChatLoop()
        {
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"\n{_userName}> ");
                string? userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput))
                {
                    _styler.PrintWarning("I didn't catch that. Please type a question.");
                    continue;
                }

                string cleanedInput = userInput.Trim();

                if (cleanedInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (cleanedInput.Equals("help", StringComparison.OrdinalIgnoreCase))
                {
                    _styler.PrintHelp();
                    continue;
                }

                string response = _responseService.GetResponse(cleanedInput, _userName);
                _styler.TypeLine(response, ConsoleColor.Magenta, 8);
            }
        }
    }
}