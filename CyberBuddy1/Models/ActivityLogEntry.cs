namespace CyberBuddy1.Models
{
    public class ActivityLogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Description { get; set; } = string.Empty;
        public string Formatted => $"{Timestamp:HH:mm dd MMM} — {Description}";
    }
}