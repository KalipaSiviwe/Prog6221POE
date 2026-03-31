using CyberBuddy.Services;
using System.Media;

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

            try
            {
                if (!File.Exists(audioPath))
                {
                    _styler.PrintInfo("Unfortunatley my voice is gone, i have the flu =( ");
                    return;
                }

                using SoundPlayer player = new SoundPlayer(audioPath);
                player.Load();
                player.PlaySync();
            }
            catch (Exception ex)
            {
                _styler.PrintWarning($"Could not play audio: {ex.Message}");
            }
        }

        private void AskUserName()
        {
            while (true)
            {
                _styler.TypeLine("Please enter your name: ", ConsoleColor.Cyan, 8);
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
            _styler.TypeLine($"Welcome, {_userName}! I am CyberBuddy.", ConsoleColor.Green, 10);
            _styler.TypeLine("Ask me about passwords, phishing, safe browsing, malware, and privacy.", ConsoleColor.Green, 10);
            _styler.TypeLine("Type 'help' for examples, or 'exit' to quit.", ConsoleColor.Yellow, 10);
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
                    _styler.PrintWarning("I didn't quite catch that. Please type your question.");
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
                _styler.TypeLine(response, ConsoleColor.Magenta, 6);
            }
        }
    }
}