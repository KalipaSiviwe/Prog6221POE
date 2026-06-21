namespace CyberBuddy1.Models
{
    public class CyberTask
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? ReminderDate { get; set; }
        public bool IsCompleted { get; set; }
        public bool ReminderNotified { get; set; }
        public DateTime CreatedAt { get; set; }

        public string DisplayLine =>
            ReminderDate.HasValue
                ? $"{Title} — Reminder: {ReminderDate:dd MMM yyyy HH:mm}" + (IsCompleted ? " [Done]" : "")
                : $"{Title}" + (IsCompleted ? " [Done]" : "");
    }
}