using System.Text.RegularExpressions;
using CyberBuddy1.Models;

namespace CyberBuddy1.Services
{
    public class QuizService
    {
        private readonly ActivityLogService _activityLog;
        private readonly List<QuizQuestion> _questions;
        private int _currentIndex;
        private int _score;
        private bool _isActive;

        public bool IsActive => _isActive;
        public int QuestionsAnswered => _isActive ? _currentIndex : 0;
        public int TotalQuestions => _questions.Count;
        public int CurrentScore => _score;

        public QuizService(ActivityLogService activityLog)
        {
            _activityLog = activityLog;
            _questions = BuildQuestions();
        }

        public void StartQuiz()
        {
            _currentIndex = 0;
            _score = 0;
            _isActive = true;
            _activityLog.Log($"Quiz started — {_questions.Count} questions.");
        }

        public QuizQuestion? GetCurrentQuestion()
        {
            if (!_isActive || _currentIndex >= _questions.Count) return null;
            return _questions[_currentIndex];
        }

        public string FormatCurrentQuestion()
        {
            var q = GetCurrentQuestion();
            if (q == null) return "No active quiz question.";

            var options = string.Join("\n", q.Options.Select((o, i) => $"{(char)('A' + i)}) {o}"));
            return $"Question {_currentIndex + 1}/{_questions.Count}:\n{q.Question}\n\n{options}\n\nReply with A, B, C, D — or True/False.";
        }

        public string SubmitAnswer(string rawAnswer)
        {
            if (!_isActive) return "No quiz is running. Say \"start quiz\" to begin.";

            var q = _questions[_currentIndex];
            int? chosen = ParseAnswer(rawAnswer, q.Options.Count);

            if (chosen == null)
                return "Please reply with a letter (A, B, C, D) or True/False.";

            bool correct = chosen.Value == q.CorrectIndex;
            if (correct) _score++;

            string feedback = correct
                ? $"Correct! {q.Explanation}"
                : $"Not quite. The best answer is {q.Options[q.CorrectIndex]}. {q.Explanation}";

            _currentIndex++;

            if (_currentIndex >= _questions.Count)
            {
                _isActive = false;
                string final = GetFinalScoreMessage();
                _activityLog.Log($"Quiz completed — score {_score}/{_questions.Count}.");
                return feedback + "\n\n" + final;
            }

            return feedback + "\n\n" + FormatCurrentQuestion();
        }

        public string GetFinalScoreMessage()
        {
            double pct = (double)_score / _questions.Count * 100;
            string grade = pct >= 80
                ? "Great job! You're a cybersecurity pro!"
                : pct >= 50
                    ? "Good effort! Review the explanations and try again."
                    : "Keep learning to stay safe online!";

            return $"Quiz finished! Your score: {_score}/{_questions.Count} ({pct:0}%)\n{grade}";
        }

        private static int? ParseAnswer(string raw, int optionCount)
        {
            string a = raw.Trim().ToLowerInvariant();

            if (a is "true" or "t") return 0;
            if (a is "false" or "f") return 1;

            if (a.Length == 1 && a[0] >= 'a' && a[0] < 'a' + optionCount)
                return a[0] - 'a';

            if (Regex.IsMatch(a, @"^[a-d]$"))
                return a[0] - 'a';

            return null;
        }

        private static List<QuizQuestion> BuildQuestions()
        {
            return new List<QuizQuestion>
            {
                new() {
                    Question = "What should you do if you receive an email asking for your password?",
                    Options = new() { "Reply with your password", "Delete the email", "Report the email as phishing", "Ignore it" },
                    CorrectIndex = 2,
                    Explanation = "Reporting phishing emails helps prevent scams."
                },
                new() {
                    Question = "A strong password should include a mix of letters, numbers, and symbols.",
                    Options = new() { "True", "False" },
                    CorrectIndex = 0,
                    Explanation = "Complex passwords are harder for attackers to guess or crack."
                },
                new() {
                    Question = "What is phishing?",
                    Options = new() { "A type of malware", "Tricking users into revealing sensitive info", "A firewall feature", "A VPN protocol" },
                    CorrectIndex = 1,
                    Explanation = "Phishing uses fake messages to steal credentials or data."
                },
                new() {
                    Question = "You should use the same password for all your accounts to remember it easily.",
                    Options = new() { "True", "False" },
                    CorrectIndex = 1,
                    Explanation = "Unique passwords limit damage if one account is breached."
                },
                new() {
                    Question = "What does HTTPS in a browser address bar indicate?",
                    Options = new() { "The site is always safe", "Traffic is encrypted between you and the site", "The site is government-owned", "No ads will appear" },
                    CorrectIndex = 1,
                    Explanation = "HTTPS encrypts data in transit; it does not guarantee the site is trustworthy."
                },
                new() {
                    Question = "Two-factor authentication (2FA) adds an extra verification step beyond your password.",
                    Options = new() { "True", "False" },
                    CorrectIndex = 0,
                    Explanation = "2FA significantly reduces account takeover risk."
                },
                new() {
                    Question = "Which is a sign of a social engineering attack?",
                    Options = new() { "Urgent pressure to act now", "A scheduled newsletter", "A known contact's normal message", "A software update prompt from Windows" },
                    CorrectIndex = 0,
                    Explanation = "Urgency and pressure are classic manipulation tactics."
                },
                new() {
                    Question = "Opening attachments from unknown senders is generally safe if the email looks professional.",
                    Options = new() { "True", "False" },
                    CorrectIndex = 1,
                    Explanation = "Malware is often delivered through convincing fake emails."
                },
                new() {
                    Question = "What is the safest action on public Wi-Fi when banking?",
                    Options = new() { "Log in without extra precautions", "Use a VPN or wait for a trusted network", "Share the password with a friend", "Disable your antivirus" },
                    CorrectIndex = 1,
                    Explanation = "Public networks can be monitored; VPNs add protection."
                },
                new() {
                    Question = "Ransomware encrypts your files and demands payment to restore access.",
                    Options = new() { "True", "False" },
                    CorrectIndex = 0,
                    Explanation = "Regular backups are the best defence against ransomware."
                },
                new() {
                    Question = "Which link is most suspicious?",
                    Options = new() { "https://www.google.com", "https://paypa1-secure-login.xyz", "https://github.com", "https://microsoft.com" },
                    CorrectIndex = 1,
                    Explanation = "Look-alike domains (paypa1) are a common phishing trick."
                },
                new() {
                    Question = "Keeping software updated helps patch security vulnerabilities.",
                    Options = new() { "True", "False" },
                    CorrectIndex = 0,
                    Explanation = "Updates often fix flaws attackers exploit."
                }
            };
        }
    }
}