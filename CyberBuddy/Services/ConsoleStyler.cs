namespace CyberBuddy.Services
{
    public class ConsoleStyler
    {
        public void PrintHeader()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==============================================================");
            Console.WriteLine("   ____      _               ____            _     _       ");
            Console.WriteLine("  / ___|   _| |__   ___ _ __| __ ) _   _  __| | __| |_   _ ");
            Console.WriteLine(" | |  | | | | '_ \\ / _ \\ '__|  _ \\| | | |/ _` |/ _` | | | |");
            Console.WriteLine(" | |__| |_| | |_) |  __/ |  | |_) | |_| | (_| | (_| | |_| |");
            Console.WriteLine("  \\____\\__, |_.__/ \\___|_|  |____/ \\__,_|\\__,_|\\__,_|\\__, |");
            Console.WriteLine("       |___/                                          |___/ ");
            Console.WriteLine("==============================================================");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("CyberBuddy - Cybersecurity Awareness Assistant");
            Console.WriteLine("Stay alert. Stay secure.");
            Console.ResetColor();
        }

        public void PrintDivider()
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("--------------------------------------------------------------");
            Console.ResetColor();
        }

        public void PrintInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"[INFO] {message}");
            Console.ResetColor();
        }

        public void PrintWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARNING] {message}");
            Console.ResetColor();
        }

        public void PrintGoodbye(string userName)
        {
            PrintDivider();
            TypeLine($"Goodbye, {userName}. Stay cyber safe!", ConsoleColor.Green, 10);
            PrintDivider();
        }

        public void PrintHelp()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("You can ask things like:");
            Console.WriteLine("- How are you?");
            Console.WriteLine("- What is your purpose?");
            Console.WriteLine("- What can I ask you about?");
            Console.WriteLine("- Tips for password safety");
            Console.WriteLine("- How to detect phishing");
            Console.WriteLine("- Safe browsing tips");
            Console.WriteLine("- Malware basics");
            Console.WriteLine("- Privacy protection tips");
            Console.WriteLine("Type 'exit' to close CyberBuddy.");
            Console.ResetColor();
        }

        public void TypeLine(string text, ConsoleColor color, int delayMs)
        {
            Console.ForegroundColor = color;
            foreach (char c in text)
            {
                Console.Write(c);
                Thread.Sleep(delayMs);
            }

            Console.WriteLine();
            Console.ResetColor();
        }
    }
}