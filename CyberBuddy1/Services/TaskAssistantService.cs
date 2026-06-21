using CyberBuddy1.Models;
using Microsoft.Data.SqlClient;

namespace CyberBuddy1.Services
{
    public class TaskAssistantService
    {
        private readonly DatabaseService _db;
        private readonly ActivityLogService _activityLog;

        private static readonly Dictionary<string, string> KnownDescriptions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["review privacy settings"] = "Review account privacy settings to ensure your data is protected.",
            ["enable two-factor authentication"] = "Turn on 2FA on your important accounts for an extra layer of security.",
            ["enable 2fa"] = "Turn on two-factor authentication on email, banking, and social accounts.",
            ["update my password"] = "Create a strong, unique password and update it on the affected account.",
            ["update password"] = "Use a password manager to generate and store a strong unique password.",
            ["run antivirus scan"] = "Run a full antivirus scan to detect and remove potential malware.",
            ["check phishing emails"] = "Review your inbox for suspicious emails and report phishing attempts.",
        };

        public TaskAssistantService(DatabaseService db, ActivityLogService activityLog)
        {
            _db = db;
            _activityLog = activityLog;
        }

        public string SuggestDescription(string title)
        {
            string key = title.Trim();
            if (KnownDescriptions.TryGetValue(key, out string? desc)) return desc;

            foreach (var pair in KnownDescriptions)
            {
                if (key.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }

            return $"Complete this cybersecurity task: {title}.";
        }

        public async Task<int> AddTaskAsync(string title, string description, DateTime? reminderDate = null)
        {
            await using var conn = await _db.OpenConnectionAsync().ConfigureAwait(false);
            await using var cmd = new SqlCommand(
                @"INSERT INTO tasks (title, description, reminder_date)
                  OUTPUT INSERTED.id
                  VALUES (@title, @desc, @reminder);", conn);

            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@desc", description);
            cmd.Parameters.AddWithValue("@reminder", reminderDate.HasValue ? reminderDate.Value : DBNull.Value);

            int id = Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));

            string logMsg = reminderDate.HasValue
                ? $"Task added: '{title}' (Reminder set for {reminderDate:dd MMM yyyy})."
                : $"Task added: '{title}' (no reminder set).";
            _activityLog.Log(logMsg);

            return id;
        }

        public async Task<List<CyberTask>> GetAllTasksAsync()
        {
            var list = new List<CyberTask>();
            await using var conn = await _db.OpenConnectionAsync().ConfigureAwait(false);
            await using var cmd = new SqlCommand(
                "SELECT id, title, description, reminder_date, is_completed, reminder_notified, created_at FROM tasks ORDER BY created_at DESC", conn);
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(MapTask(reader));
            }

            return list;
        }

        public async Task<bool> MarkCompleteAsync(int id)
        {
            await using var conn = await _db.OpenConnectionAsync().ConfigureAwait(false);
            await using var cmd = new SqlCommand("UPDATE tasks SET is_completed = 1 WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            int rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (rows > 0)
            {
                _activityLog.Log($"Task #{id} marked as completed.");
                return true;
            }

            return false;
        }

        public async Task<bool> DeleteTaskAsync(int id)
        {
            await using var conn = await _db.OpenConnectionAsync().ConfigureAwait(false);
            await using var cmd = new SqlCommand("DELETE FROM tasks WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            int rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (rows > 0)
            {
                _activityLog.Log($"Task #{id} deleted.");
                return true;
            }

            return false;
        }

        public async Task<bool> SetReminderAsync(int id, DateTime reminderDate)
        {
            await using var conn = await _db.OpenConnectionAsync().ConfigureAwait(false);
            await using var cmd = new SqlCommand(
                "UPDATE tasks SET reminder_date = @date, reminder_notified = 0 WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@date", reminderDate);
            cmd.Parameters.AddWithValue("@id", id);
            int rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (rows > 0)
            {
                _activityLog.Log($"Reminder set for task #{id} on {reminderDate:dd MMM yyyy}.");
                return true;
            }

            return false;
        }

        public async Task<List<CyberTask>> GetDueRemindersAsync()
        {
            var list = new List<CyberTask>();
            await using var conn = await _db.OpenConnectionAsync().ConfigureAwait(false);
            await using var cmd = new SqlCommand(
                @"SELECT id, title, description, reminder_date, is_completed, reminder_notified, created_at
                  FROM tasks
                  WHERE reminder_date IS NOT NULL
                    AND reminder_date <= @now
                    AND is_completed = 0
                    AND reminder_notified = 0", conn);
            cmd.Parameters.AddWithValue("@now", DateTime.Now);
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(MapTask(reader));
            }

            return list;
        }

        public async Task MarkReminderNotifiedAsync(int id)
        {
            await using var conn = await _db.OpenConnectionAsync().ConfigureAwait(false);
            await using var cmd = new SqlCommand("UPDATE tasks SET reminder_notified = 1 WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private static CyberTask MapTask(SqlDataReader reader) => new()
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            Title = reader.GetString(reader.GetOrdinal("title")),
            Description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString(reader.GetOrdinal("description")),
            ReminderDate = reader.IsDBNull(reader.GetOrdinal("reminder_date")) ? null : reader.GetDateTime(reader.GetOrdinal("reminder_date")),
            IsCompleted = reader.GetBoolean(reader.GetOrdinal("is_completed")),
            ReminderNotified = reader.GetBoolean(reader.GetOrdinal("reminder_notified")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
        };

        public async Task<CyberTask?> GetTaskByIdAsync(int id)
        {
            await using var conn = await _db.OpenConnectionAsync().ConfigureAwait(false);
            await using var cmd = new SqlCommand(
                "SELECT id, title, description, reminder_date, is_completed, reminder_notified, created_at FROM tasks WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

            if (await reader.ReadAsync().ConfigureAwait(false))
                return MapTask(reader);

            return null;
        }

        public async Task<bool> UpdateTaskAsync(int id, string? newTitle = null, string? newDescription = null, bool? isCompleted = null)
        {
            var parts = new List<string>();
            var cmd = new SqlCommand();

            if (newTitle != null)
            {
                parts.Add("title = @title");
                cmd.Parameters.AddWithValue("@title", newTitle);
            }

            if (newDescription != null)
            {
                parts.Add("description = @desc");
                cmd.Parameters.AddWithValue("@desc", newDescription);
            }

            if (isCompleted.HasValue)
            {
                parts.Add("is_completed = @done");
                cmd.Parameters.AddWithValue("@done", isCompleted.Value);
            }

            if (parts.Count == 0) return false;

            await using var conn = await _db.OpenConnectionAsync().ConfigureAwait(false);
            cmd.Connection = conn;
            cmd.CommandText = $"UPDATE tasks SET {string.Join(", ", parts)} WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);

            int rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            if (rows > 0)
            {
                string changes = string.Join(", ", parts.Select(p => p.Split('=')[0].Trim()));
                _activityLog.Log($"Task #{id} updated ({changes}).");
                return true;
            }

            return false;
        }
    }
}