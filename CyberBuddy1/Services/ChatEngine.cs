using System.Text.RegularExpressions;
using CyberBuddy1.Models;

namespace CyberBuddy1.Services
{
    /// <summary>
    /// Core chat logic: keywords, randomised tips (generic collections), follow-up flow,
    /// memory, sentiment, and safe fallbacks. Uses a delegate for random response selection (learning outcome).
    /// </summary>
    public class ChatEngine
    {
        /// <summary>
        /// Delegate: picks one line from a list of candidate responses (demonstrates delegate use).
        /// </summary>
        public delegate string RandomResponsePicker(IReadOnlyList<string> candidates);

        private readonly Random _random = new();
        private readonly ChatMemory _memory;
        private readonly Dictionary<string, List<string>> _responsesByTopic;
        private readonly List<string> _followUpPhrases;
        private readonly RandomResponsePicker _pickRandomResponse;

        private string? _activeTopicKey;

        public ChatEngine(ChatMemory memory)
        {
            _memory = memory;
            _pickRandomResponse = PickRandomFromList;
            _responsesByTopic = BuildTopicResponses();
            _followUpPhrases = new List<string>
            {
                "tell me more", "explain more", "more detail", "more details",
                "give me another tip", "another tip", "another one", "what else",
                "go on", "continue", "elaborate", "anything else"
            };
        }

        private string PickRandomFromList(IReadOnlyList<string> candidates)
        {
            if (candidates.Count == 0)
            {
                return "I do not have extra tips for that topic yet.";
            }

            int index = _random.Next(candidates.Count);
            return candidates[index];
        }

        /// <summary>
        /// Processes user text. Returns bot message(s). Sets exitRequested when user wants to quit.
        /// </summary>
        public string ProcessInput(string rawInput, out bool exitRequested)
        {
            exitRequested = false;

            try
            {
                if (string.IsNullOrWhiteSpace(rawInput))
                {
                    return "I did not quite catch that. Please type your question or say 'help' for ideas.";
                }

                string input = rawInput.Trim();
                string lower = input.ToLowerInvariant();

                if (IsExitCommand(lower))
                {
                    exitRequested = true;
                    return $"Goodbye, {_memory.UserName}. Stay cyber safe!";
                }

                if (lower is "help" or "?")
                {
                    return BuildHelpText();
                }

                // Remember explicit name / topic statements first
                string? memoryAck = TryRememberFromInput(input, lower);
                if (memoryAck != null)
                {
                    return memoryAck;
                }

                if (TryHandleFollowUp(lower, out string? followUpReply))
                {
                    return followUpReply!;
                }

                if (IsFollowUpPhrase(lower) && _activeTopicKey == null)
                {
                    return "Ask me for a tip on a topic first (for example passwords, scams, or privacy). Then you can say \"tell me more\" for another related tip.";
                }

                UserSentiment sentiment = DetectSentiment(lower);
                string? keywordReply = TryKeywordResponse(lower, sentiment);
                if (keywordReply != null)
                {
                    return keywordReply;
                }

                return "I'm not sure I understand. Can you try rephrasing? You can ask about passwords, scams, privacy, phishing, malware, or safe browsing.";
            }
            catch (Exception)
            {
                return "Something went wrong on my side, but I am still here. Please try again with a shorter message.";
            }
        }

        private static bool IsExitCommand(string lower) =>
            lower is "exit" or "quit" or "bye" or "goodbye" or "close";

        private string BuildHelpText()
        {
            return "Try asking: password safety, phishing tips, scam awareness, privacy settings, malware basics, safe browsing — " +
                   "or say things like 'tell me more' after a tip. Commands: help, exit.";
        }

        private string? TryRememberFromInput(string original, string lower)
        {
            // Name: "my name is Sam", "call me Alex"
            Match nameMatch = Regex.Match(original, @"^(?:my name is|call me)\s+(.{2,40})$", RegexOptions.IgnoreCase);
            if (nameMatch.Success)
            {
                string name = nameMatch.Groups[1].Value.Trim().TrimEnd('.', '!', '?');
                if (name.Length > 0)
                {
                    _memory.UserName = name;
                    return $"Thanks, {_memory.UserName}. I will use that name in our chat.";
                }
            }

            // Topic memory: "I'm interested in privacy" (assignment example)
            Match interest = Regex.Match(
                original,
                @"(?:i'?m interested in|i care about|i like|my favorite topic is|favourite topic is|i want to learn about)\s+(.{3,60})",
                RegexOptions.IgnoreCase);
            if (interest.Success)
            {
                string topic = interest.Groups[1].Value.Trim().TrimEnd('.', '!', '?');
                _memory.FavoriteCyberTopic = topic;
                _activeTopicKey = MapLooseTopicToKey(topic.ToLowerInvariant()) ?? _activeTopicKey;
                return $"Great! I will remember that you are interested in {topic}. It is an important part of staying safe online.";
            }

            return null;
        }

        private bool IsFollowUpPhrase(string lower) =>
            _followUpPhrases.Any(p => lower.Contains(p, StringComparison.Ordinal));

        private bool TryHandleFollowUp(string lower, out string? reply)
        {
            reply = null;
            if (_activeTopicKey == null || !_responsesByTopic.ContainsKey(_activeTopicKey))
            {
                return false;
            }

            if (!IsFollowUpPhrase(lower))
            {
                return false;
            }

            string tip = _pickRandomResponse(_responsesByTopic[_activeTopicKey]);
            reply = _memory.WithTopicRecall(tip);
            return true;
        }

        private string? TryKeywordResponse(string lower, UserSentiment sentiment)
        {
            // Ordered checks: more specific first
            if (ContainsAny(lower, "how are you", "how r you", "how are u"))
            {
                _activeTopicKey = null;
                return $"I am running smoothly, {_memory.UserName}. Ready to help you stay safer online.";
            }

            if (ContainsAny(lower, "purpose", "what do you do", "who are you"))
            {
                _activeTopicKey = null;
                return "I am CyberBuddy, a cybersecurity awareness assistant. I share practical tips about passwords, scams, phishing, malware, privacy, and safe browsing.";
            }

            if (ContainsAny(lower, "phishing", "phish"))
            {
                return AssignTopicAndReply("phishing", sentiment, lower);
            }

            if (ContainsAny(lower, "password", "passcode", "passphrase"))
            {
                return AssignTopicAndReply("password", sentiment, lower);
            }

            if (ContainsAny(lower, "scam", "fraud", "con artist"))
            {
                return AssignTopicAndReply("scam", sentiment, lower);
            }

            if (ContainsAny(lower, "privacy", "private data", "personal data"))
            {
                return AssignTopicAndReply("privacy", sentiment, lower);
            }

            if (ContainsAny(lower, "malware", "virus", "trojan", "ransomware"))
            {
                return AssignTopicAndReply("malware", sentiment, lower);
            }

            if (ContainsAny(lower, "safe browsing", "browse safely", "https", "public wi-fi", "public wifi"))
            {
                return AssignTopicAndReply("safebrowse", sentiment, lower);
            }

            if (ContainsAny(lower, "two-factor", "2fa", "mfa", "multi-factor"))
            {
                return AssignTopicAndReply("password", sentiment, lower);
            }

            return null;
        }

        private string AssignTopicAndReply(string topicKey, UserSentiment sentiment, string lower)
        {
            _activeTopicKey = topicKey;
            string tip = _pickRandomResponse(_responsesByTopic[topicKey]);

            // Assignment: if user is worried about scams, empathise then immediately give a tip (no extra prompt).
            if (sentiment == UserSentiment.Worried && topicKey == "scam")
            {
                string empathy = "It is completely understandable to feel that way. Scammers can be very convincing.\n\n";
                return empathy + _memory.WithTopicRecall(tip);
            }

            if (sentiment == UserSentiment.Worried)
            {
                string empathy = "It is okay to feel unsure about security topics — many people do.\n\n";
                return empathy + _memory.WithTopicRecall(tip);
            }

            if (sentiment == UserSentiment.Curious)
            {
                return $"Great question spirit, {_memory.UserName}! {_memory.WithTopicRecall(tip)}";
            }

            if (sentiment == UserSentiment.Frustrated)
            {
                return $"I hear you — security advice can feel like a lot. Here is one clear step: {_memory.WithTopicRecall(tip)}";
            }

            return _memory.WithTopicRecall(tip);
        }

        private static UserSentiment DetectSentiment(string lower)
        {
            if (ContainsAny(lower, "worried", "anxious", "scared", "nervous", "afraid", "fear", "stress", "overwhelmed"))
            {
                return UserSentiment.Worried;
            }

            if (ContainsAny(lower, "curious", "wondering", "interested to know", "tell me why", "how does"))
            {
                return UserSentiment.Curious;
            }

            if (ContainsAny(lower, "frustrated", "annoyed", "angry", "fed up", "tired of", "sick of"))
            {
                return UserSentiment.Frustrated;
            }

            return UserSentiment.Neutral;
        }

        private static string? MapLooseTopicToKey(string t)
        {
            if (t.Contains("password", StringComparison.Ordinal)) return "password";
            if (t.Contains("phish", StringComparison.Ordinal)) return "phishing";
            if (t.Contains("scam", StringComparison.Ordinal) || t.Contains("fraud", StringComparison.Ordinal)) return "scam";
            if (t.Contains("privacy", StringComparison.Ordinal)) return "privacy";
            if (t.Contains("malware", StringComparison.Ordinal) || t.Contains("virus", StringComparison.Ordinal)) return "malware";
            if (t.Contains("browse", StringComparison.Ordinal) || t.Contains("wifi", StringComparison.Ordinal)) return "safebrowse";
            return null;
        }

        private Dictionary<string, List<string>> BuildTopicResponses()
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["password"] = new List<string>
                {
                    "Use strong, unique passwords for each account. Avoid birthdays or pet names that others could guess.",
                    "A password manager can create and store long random passwords so you do not have to remember them all.",
                    "Turn on multi-factor authentication wherever it is offered — it is one of the strongest protections available.",
                    "If a site is breached, change that password immediately and never reuse the old one elsewhere."
                },
                ["scam"] = new List<string>
                {
                    "Slow down if someone pressures you to pay quickly or share codes — legitimate organisations rarely do that.",
                    "Verify unexpected messages through an official phone number or website you looked up yourself, not links in the message.",
                    "Be sceptical of too-good-to-be-true prizes, romance promises, or investment returns with guaranteed profits.",
                    "Never share one-time PINs or remote-desktop access with callers claiming to be from your bank or IT support."
                },
                ["privacy"] = new List<string>
                {
                    "Review app permissions regularly and remove access that apps do not truly need.",
                    "Limit what you post publicly — details like your address, workplace, or travel plans can be misused.",
                    "Use private browsing for sensitive searches on shared devices, and log out when finished.",
                    "Check privacy settings on social accounts so only people you trust can see personal posts."
                },
                ["phishing"] = new List<string>
                {
                    "Be cautious of emails asking for personal information. Scammers often disguise themselves as trusted organisations.",
                    "Hover over links before clicking to see the real destination, or type the known website address manually.",
                    "Look for odd spelling, generic greetings like 'Dear customer', or mismatched sender domains.",
                    "If an email creates urgency ('act in one hour'), pause and verify through another channel."
                },
                ["malware"] = new List<string>
                {
                    "Keep your operating system and applications updated so security patches apply promptly.",
                    "Only download software from official stores or the vendor's website, not random pop-up ads.",
                    "Run a reputable antivirus scan on attachments before opening, especially unexpected invoices.",
                    "Disable macros in Office files from unknown senders — they are a common malware delivery route."
                },
                ["safebrowse"] = new List<string>
                {
                    "Prefer HTTPS sites (padlock in the address bar) and avoid entering passwords on untrusted networks without a VPN.",
                    "Do not ignore browser warnings about unsafe sites — they often detect known threats.",
                    "Use separate profiles or browsers for banking versus casual browsing to reduce tracking overlap.",
                    "Clear saved passwords on shared computers and never store banking sessions on them."
                }
            };
        }

        private static bool ContainsAny(string input, params string[] needles)
        {
            foreach (string n in needles)
            {
                if (input.Contains(n, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
