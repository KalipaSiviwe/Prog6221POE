using System.Text.RegularExpressions;
using CyberBuddy1.Models;

namespace CyberBuddy1.Services
{
    public class NlpIntentDetector
    {
        public UserIntent DetectIntent(string lower)
        {
            if (ContainsAny(lower, "exit", "quit", "bye", "goodbye", "close"))
                return UserIntent.Exit;

            if (lower is "help" or "?")
                return UserIntent.Help;

            if (ContainsAny(lower, "show more log", "full log", "all activity", "show full log"))
                return UserIntent.ShowMoreActivityLog;

            if (ContainsAny(lower, "activity log", "what have you done", "show log", "recent actions", "what did you do"))
                return UserIntent.ShowActivityLog;

            if (ContainsAny(lower, "start quiz", "begin quiz", "play quiz", "cyber quiz", "mini game", "mini-game"))
                return UserIntent.StartQuiz;

            if (Regex.IsMatch(lower, @"^(?:answer\s+)?[a-d]$") || lower is "true" or "false")
                return UserIntent.AnswerQuiz;

            if (ContainsAny(lower, "list tasks", "show tasks", "my tasks", "view tasks", "all tasks"))
                return UserIntent.ListTasks;

            if (ContainsAny(lower, "complete task", "mark complete", "mark as complete", "finish task", "mark task done", "task done"))
                return UserIntent.CompleteTask;

            if (ContainsAny(lower, "update task", "edit task", "change task", "modify task", "update my task"))
                return UserIntent.UpdateTask;

            if (ContainsAny(lower, "delete task", "remove task"))
                return UserIntent.DeleteTask;

            if (ContainsAny(lower, "remind me", "set a reminder", "set reminder", "reminder for"))
                return UserIntent.SetReminder;

            if (Regex.IsMatch(lower, @"(?:add|create|new)\s+(?:a\s+)?task")
                || lower.StartsWith("add task")
                || (ContainsAny(lower, "task") && ContainsAny(lower, "add", "create", "set")))
                return UserIntent.AddTask;

            return UserIntent.Unknown;
        }

        public string? ExtractTaskTitle(string input)
        {
            var match = Regex.Match(input,
                @"(?:add\s+task\s*[-–:]\s*|add\s+(?:a\s+)?task\s+(?:to\s+)?|create\s+(?:a\s+)?task\s+(?:to\s+)?)(.+)$",
                RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value.Trim().TrimEnd('.', '!', '?');

            match = Regex.Match(input, @"task\s+(?:to\s+)?(.+)$", RegexOptions.IgnoreCase);
            if (match.Success && !ContainsAny(match.Groups[1].Value.ToLowerInvariant(), "list", "show", "delete", "complete", "update", "edit"))
                return match.Groups[1].Value.Trim().TrimEnd('.', '!', '?');

            return null;
        }

        /// <summary>
        /// Parses: "update task #1 title to Enable 2FA" or "change task 2 description to ..."
        /// </summary>
        public (int? Id, string? Field, string? Value) ParseDirectUpdate(string input)
        {
            var match = Regex.Match(input,
                @"(?:update|edit|change|modify)\s+task\s*#?(\d+)\s+(title|name|description|desc|done|complete|completed)\s*(?:to\s+)?(.+)?",
                RegexOptions.IgnoreCase);

            if (!match.Success) return (null, null, null);

            int id = int.Parse(match.Groups[1].Value);
            string field = match.Groups[2].Value.ToLowerInvariant();
            string? value = match.Groups[3].Success ? match.Groups[3].Value.Trim().TrimEnd('.', '!', '?') : null;

            if (field is "name") field = "title";
            if (field is "desc") field = "description";
            if (field is "complete" or "completed") field = "done";

            if (field == "done") value = null;

            return (id, field, value);
        }

        public DateTime? ParseReminderDuration(string lower)
        {
            var match = Regex.Match(lower, @"(\d+)\s*(day|days|hour|hours|week|weeks)");
            if (!match.Success) return null;

            int amount = int.Parse(match.Groups[1].Value);
            string unit = match.Groups[2].Value;

            return unit.StartsWith("hour")
                ? DateTime.Now.AddHours(amount)
                : unit.StartsWith("week")
                    ? DateTime.Now.AddDays(amount * 7)
                    : DateTime.Now.AddDays(amount);
        }

        public DateTime? ParseTomorrow(string lower)
        {
            if (lower.Contains("tomorrow"))
                return DateTime.Today.AddDays(1).AddHours(9);
            return null;
        }

        public int? ExtractTaskId(string lower)
        {
            var match = Regex.Match(lower, @"(?:task\s*#?|#)(\d+)");
            if (match.Success) return int.Parse(match.Groups[1].Value);

            if (Regex.IsMatch(lower, @"^\d+$"))
                return int.Parse(lower.Trim());

            return null;
        }

        public string? ParseUpdateFieldChoice(string lower)
        {
            if (ContainsAny(lower, "title", "name", "rename"))
                return "title";
            if (ContainsAny(lower, "description", "desc", "details"))
                return "description";
            if (ContainsAny(lower, "done", "complete", "completed", "finished", "mark done"))
                return "done";
            return null;
        }

        private static bool ContainsAny(string input, params string[] needles)
        {
            foreach (string n in needles)
            {
                if (input.Contains(n, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}