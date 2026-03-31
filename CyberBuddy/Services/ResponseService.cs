namespace CyberBuddy.Services
{
    public class ResponseService
    {
        public string GetResponse(string input, string userName)
        {
            string text = input.ToLowerInvariant();

            if (ContainsAny(text, "how are you", "how r u", "how are u"))
            {
                return $"I am functioning well, {userName}. Thanks for asking!";
            }

            if (ContainsAny(text, "what is your purpose", "your purpose", "why are you here"))
            {
                return "My purpose is to improve your cybersecurity awareness and help you stay safe online.";
            }

            if (ContainsAny(text, "what can i ask", "help topics", "what do you do"))
            {
                return "You can ask me about password safety, phishing, safe browsing, malware basics, and privacy tips.";
            }

            if (ContainsAny(text, "password", "strong password", "password safety"))
            {
                return "Use long, unique passwords (12+ characters), mix letters/numbers/symbols, and never reuse passwords. A password manager helps a lot.";
            }

            if (ContainsAny(text, "phishing", "scam email", "fake email", "suspicious link"))
            {
                return "Check sender addresses carefully, avoid urgent scare tactics, don't click unknown links, and verify requests through trusted channels.";
            }

            if (ContainsAny(text, "safe browsing", "browse safely", "website safety"))
            {
                return "Only visit HTTPS websites, keep browser/software updated, avoid downloading unknown files, and use antivirus protection.";
            }

            if (ContainsAny(text, "malware", "virus", "ransomware"))
            {
                return "Malware is harmful software. Prevent it by updating systems, avoiding suspicious downloads, and scanning files before opening.";
            }

            if (ContainsAny(text, "privacy", "protect my data", "data protection"))
            {
                return "Limit personal info shared online, review privacy settings, enable MFA, and be careful on public Wi-Fi.";
            }

            return $"{userName}, I didn't quite understand that. Could you rephrase or type 'help' for available topics?";
        }

        private static bool ContainsAny(string input, params string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (input.Contains(keyword))
                {
                    return true;
                }
            }

            return false;
        }
    }
}