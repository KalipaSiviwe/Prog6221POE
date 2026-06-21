using CyberBuddy1.Models;

namespace CyberBuddy1.Services
{
    public class ActivityLogService
    {
        private readonly List<ActivityLogEntry> _entries = new();
        private const int DefaultDisplayCount = 8;

        public void Log(string description)
        {
            _entries.Add(new ActivityLogEntry { Description = description });
        }

        public IReadOnlyList<ActivityLogEntry> GetRecent(int count = DefaultDisplayCount)
        {
            return _entries
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .Reverse()
                .ToList();
        }

        public IReadOnlyList<ActivityLogEntry> GetAll()
        {
            return _entries.OrderByDescending(e => e.Timestamp).ToList();
        }

        public string FormatRecentSummary()
        {
            var recent = GetRecent();
            if (recent.Count == 0)
            {
                return "No actions recorded yet. Try adding a task or starting a quiz!";
            }

            var lines = recent.Select((e, i) => $"{i + 1}. {e.Description}");
            return "Here's a summary of recent actions:\n" + string.Join("\n", lines)
                   + "\n\nSay \"show more log\" to see the full history.";
        }

        public string FormatFullSummary()
        {
            var all = GetAll();
            if (all.Count == 0) return "The activity log is empty.";

            var lines = all.Select((e, i) => $"{i + 1}. {e.Description}");
            return "Full activity history:\n" + string.Join("\n", lines);
        }
    }
}