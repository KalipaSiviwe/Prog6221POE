using System.Text.RegularExpressions;
using CyberBuddy1.Models;

namespace CyberBuddy1.Services
{
    public class ChatEngine
    {
        public delegate string RandomResponsePicker(IReadOnlyList<string> candidates);

        private readonly Random _random = new();
        private readonly ChatMemory _memory;
        private readonly Dictionary<string, List<string>> _responsesByTopic;
        private readonly List<string> _followUpPhrases;
        private readonly RandomResponsePicker _pickRandomResponse;
        private readonly TaskAssistantService _tasks;
        private readonly QuizService _quiz;
        private readonly ActivityLogService _activityLog;
        private readonly NlpIntentDetector _nlp;

        private string? _activeTopicKey;
        private int? _pendingReminderTaskId;

        private PendingTaskAction _pendingAction = PendingTaskAction.None;
        private int? _pendingTaskId;
        private string? _pendingUpdateField;

        public ChatEngine(
            ChatMemory memory,
            TaskAssistantService tasks,
            QuizService quiz,
            ActivityLogService activityLog,
            NlpIntentDetector nlp)
        {
            _memory = memory;
            _tasks = tasks;
            _quiz = quiz;
            _activityLog = activityLog;
            _nlp = nlp;
            _pickRandomResponse = PickRandomFromList;
            _responsesByTopic = BuildTopicResponses();
            _followUpPhrases = new List<string>
            {
                "tell me more", "explain more", "more detail", "more details",
                "give me another tip", "another tip", "another one", "what else",
                "go on", "continue", "elaborate", "anything else"
            };
        }

        public async Task<ChatResult> ProcessInputAsync(string rawInput)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rawInput))
                {
                    return new ChatResult { Message = "I did not quite catch that. Please type your question or say 'help' for ideas." };
                }

                string input = rawInput.Trim();
                string lower = input.ToLowerInvariant();

                // --- Multi-step task conversations (checked first) ---
                if (_pendingReminderTaskId.HasValue)
                    return new ChatResult { Message = await HandlePendingReminderAsync(lower) };

                if (_pendingAction == PendingTaskAction.DeletePickId)
                    return new ChatResult { Message = await HandleDeletePickAsync(lower) };

                if (_pendingAction == PendingTaskAction.UpdatePickId)
                    return new ChatResult { Message = await HandleUpdatePickIdAsync(lower) };

                if (_pendingAction == PendingTaskAction.UpdatePickField)
                    return new ChatResult { Message = await HandleUpdatePickFieldAsync(lower) };

                if (_pendingAction == PendingTaskAction.UpdatePickValue)
                    return new ChatResult { Message = await HandleUpdatePickValueAsync(input) };

                // --- Intent detection ---
                UserIntent intent = _nlp.DetectIntent(lower);

                if (intent == UserIntent.Exit)
                {
                    ClearPendingAction();
                    return new ChatResult
                    {
                        Message = $"Goodbye, {_memory.UserName}. Stay cyber safe!",
                        ExitRequested = true
                    };
                }

                if (intent == UserIntent.Help)
                    return new ChatResult { Message = BuildHelpText() };

                if (intent == UserIntent.ShowActivityLog)
                {
                    _activityLog.Log("User viewed activity log (recent).");
                    return new ChatResult { Message = _activityLog.FormatRecentSummary() };
                }

                if (intent == UserIntent.ShowMoreActivityLog)
                    return new ChatResult { Message = _activityLog.FormatFullSummary() };

                if (intent == UserIntent.StartQuiz)
                {
                    _quiz.StartQuiz();
                    return new ChatResult { Message = "Cybersecurity quiz started!\n\n" + _quiz.FormatCurrentQuestion() };
                }

                if (intent == UserIntent.AnswerQuiz || _quiz.IsActive)
                    return new ChatResult { Message = _quiz.SubmitAnswer(input) };

                if (intent == UserIntent.AddTask)
                    return new ChatResult { Message = await HandleAddTaskAsync(input) };

                if (intent == UserIntent.SetReminder)
                    return new ChatResult { Message = await HandleSetReminderAsync(input, lower) };

                if (intent == UserIntent.ListTasks)
                    return new ChatResult { Message = await FormatTaskListAsync("Your cybersecurity tasks:") };

                if (intent == UserIntent.CompleteTask)
                    return new ChatResult { Message = await HandleCompleteTaskAsync(lower) };

                if (intent == UserIntent.DeleteTask)
                    return new ChatResult { Message = await HandleDeleteTaskAsync(lower) };

                if (intent == UserIntent.UpdateTask)
                    return new ChatResult { Message = await HandleUpdateTaskAsync(input, lower) };

                string? memoryAck = TryRememberFromInput(input, lower);
                if (memoryAck != null)
                    return new ChatResult { Message = memoryAck };

                if (TryHandleFollowUp(lower, out string? followUpReply))
                    return new ChatResult { Message = followUpReply! };

                if (IsFollowUpPhrase(lower) && _activeTopicKey == null)
                {
                    return new ChatResult
                    {
                        Message = "Ask me for a tip on a topic first (passwords, scams, privacy, etc.), then say \"tell me more\"."
                    };
                }

                UserSentiment sentiment = DetectSentiment(lower);
                string? keywordReply = TryKeywordResponse(lower, sentiment);
                if (keywordReply != null)
                {
                    _activityLog.Log($"NLP matched cybersecurity topic in: \"{input}\"");
                    return new ChatResult { Message = keywordReply };
                }

                return new ChatResult
                {
                    Message = "I didn't quite understand that. Try: add task, delete task, update task, list tasks, or start quiz."
                };
            }
            catch (Exception ex)
            {
                ClearPendingAction();
                return new ChatResult { Message = $"Something went wrong: {ex.Message}" };
            }
        }

        // ===================== DELETE FLOW =====================

        private async Task<string> HandleDeleteTaskAsync(string lower)
        {
            int? id = _nlp.ExtractTaskId(lower);

            if (id.HasValue)
            {
                bool ok = await _tasks.DeleteTaskAsync(id.Value).ConfigureAwait(false);
                return ok
                    ? $"Task #{id} deleted."
                    : $"Task #{id} was not found.";
            }

            var tasks = await _tasks.GetAllTasksAsync().ConfigureAwait(false);
            if (tasks.Count == 0)
                return "You have no tasks to delete.";

            _pendingAction = PendingTaskAction.DeletePickId;

            return await FormatTaskListAsync(
                "Which task would you like to delete? Reply with the task number (e.g. 2 or #2).\nSay 'cancel' to stop.");
        }

        private async Task<string> HandleDeletePickAsync(string lower)
        {
            if (ContainsAny(lower, "cancel", "stop", "nevermind", "never mind"))
            {
                ClearPendingAction();
                return "Delete cancelled.";
            }

            int? id = _nlp.ExtractTaskId(lower);
            if (!id.HasValue)
                return "Please reply with a task number, e.g. 2 or #2. Say 'cancel' to stop.";

            var task = await _tasks.GetTaskByIdAsync(id.Value).ConfigureAwait(false);
            if (task == null)
                return $"Task #{id} was not found. Pick another number or say 'cancel'.";

            await _tasks.DeleteTaskAsync(id.Value).ConfigureAwait(false);
            ClearPendingAction();

            return $"Task #{id} ('{task.Title}') has been deleted.";
        }

        // ===================== UPDATE FLOW =====================

        private async Task<string> HandleUpdateTaskAsync(string input, string lower)
        {
            var direct = _nlp.ParseDirectUpdate(input);
            if (direct.Id.HasValue && direct.Field != null)
            {
                return await ApplyUpdateAsync(direct.Id.Value, direct.Field, direct.Value).ConfigureAwait(false);
            }

            int? id = _nlp.ExtractTaskId(lower);
            if (id.HasValue)
            {
                var task = await _tasks.GetTaskByIdAsync(id.Value).ConfigureAwait(false);
                if (task == null)
                    return $"Task #{id} was not found.";

                _pendingTaskId = id;
                _pendingAction = PendingTaskAction.UpdatePickField;

                return $"Updating task #{id}: '{task.Title}'\nWhat would you like to change?\n• title\n• description\n• done (mark as complete)\nSay 'cancel' to stop.";
            }

            var tasks = await _tasks.GetAllTasksAsync().ConfigureAwait(false);
            if (tasks.Count == 0)
                return "You have no tasks to update.";

            _pendingAction = PendingTaskAction.UpdatePickId;

            return await FormatTaskListAsync(
                "Which task would you like to update? Reply with the task number (e.g. 1 or #1).\nSay 'cancel' to stop.");
        }

        private async Task<string> HandleUpdatePickIdAsync(string lower)
        {
            if (ContainsAny(lower, "cancel", "stop", "nevermind", "never mind"))
            {
                ClearPendingAction();
                return "Update cancelled.";
            }

            int? id = _nlp.ExtractTaskId(lower);
            if (!id.HasValue)
                return "Please reply with a task number, e.g. 1 or #1. Say 'cancel' to stop.";

            var task = await _tasks.GetTaskByIdAsync(id.Value).ConfigureAwait(false);
            if (task == null)
                return $"Task #{id} was not found. Pick another number or say 'cancel'.";

            _pendingTaskId = id;
            _pendingAction = PendingTaskAction.UpdatePickField;

            return $"Updating task #{id}: '{task.Title}'\nWhat would you like to change?\n• title\n• description\n• done (mark as complete)\nSay 'cancel' to stop.";
        }

        private async Task<string> HandleUpdatePickFieldAsync(string lower)
        {
            if (ContainsAny(lower, "cancel", "stop", "nevermind", "never mind"))
            {
                ClearPendingAction();
                return "Update cancelled.";
            }

            string? field = _nlp.ParseUpdateFieldChoice(lower);
            if (field == null)
            {
                return "Please reply with: title, description, or done.\nSay 'cancel' to stop.";
            }

            if (field == "done")
            {
                int id = _pendingTaskId!.Value;
                bool ok = await _tasks.UpdateTaskAsync(id, isCompleted: true).ConfigureAwait(false);
                ClearPendingAction();
                return ok
                    ? $"Task #{id} marked as done. Great work!"
                    : $"Could not update task #{id}.";
            }

            _pendingUpdateField = field;
            _pendingAction = PendingTaskAction.UpdatePickValue;

            return field == "title"
                ? "What should the new title be?"
                : "What should the new description be?";
        }

        private async Task<string> HandleUpdatePickValueAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Please enter a value, or say 'cancel'.";

            if (ContainsAny(input.ToLowerInvariant(), "cancel", "stop"))
            {
                ClearPendingAction();
                return "Update cancelled.";
            }

            int id = _pendingTaskId!.Value;
            string field = _pendingUpdateField!;

            bool ok = field == "title"
                ? await _tasks.UpdateTaskAsync(id, newTitle: input.Trim()).ConfigureAwait(false)
                : await _tasks.UpdateTaskAsync(id, newDescription: input.Trim()).ConfigureAwait(false);

            ClearPendingAction();

            return ok
                ? $"Task #{id} {field} updated successfully."
                : $"Could not update task #{id}.";
        }

        private async Task<string> ApplyUpdateAsync(int id, string field, string? value)
        {
            var task = await _tasks.GetTaskByIdAsync(id).ConfigureAwait(false);
            if (task == null)
                return $"Task #{id} was not found.";

            bool ok = field switch
            {
                "done" => await _tasks.UpdateTaskAsync(id, isCompleted: true).ConfigureAwait(false),
                "title" when !string.IsNullOrWhiteSpace(value) =>
                    await _tasks.UpdateTaskAsync(id, newTitle: value).ConfigureAwait(false),
                "description" when !string.IsNullOrWhiteSpace(value) =>
                    await _tasks.UpdateTaskAsync(id, newDescription: value).ConfigureAwait(false),
                "title" or "description" =>
                    throw new InvalidOperationException("missing value"),
                _ => false
            };

            if (field is "title" or "description" && string.IsNullOrWhiteSpace(value))
            {
                _pendingTaskId = id;
                _pendingUpdateField = field;
                _pendingAction = PendingTaskAction.UpdatePickValue;
                return field == "title"
                    ? $"What should the new title be for task #{id}?"
                    : $"What should the new description be for task #{id}?";
            }

            return ok
                ? field == "done"
                    ? $"Task #{id} marked as done. Great work!"
                    : $"Task #{id} {field} updated to \"{value}\"."
                : $"Could not update task #{id}.";
        }

        private void ClearPendingAction()
        {
            _pendingAction = PendingTaskAction.None;
            _pendingTaskId = null;
            _pendingUpdateField = null;
            _pendingReminderTaskId = null;
        }

        // ===================== OTHER TASK HANDLERS =====================

        private async Task<string> HandlePendingReminderAsync(string lower)
        {
            if (ContainsAny(lower, "no", "nope", "skip", "not now"))
            {
                _pendingReminderTaskId = null;
                return "No problem — your task was saved without a reminder.";
            }

            DateTime? reminder = _nlp.ParseReminderDuration(lower) ?? _nlp.ParseTomorrow(lower);
            if (reminder == null && ContainsAny(lower, "yes", "yeah", "sure", "ok"))
                return "When should I remind you? For example: \"in 3 days\" or \"tomorrow\".";

            if (reminder == null)
                return "I could not parse that reminder. Try: \"in 3 days\" or \"tomorrow\".";

            int taskId = _pendingReminderTaskId!.Value;
            await _tasks.SetReminderAsync(taskId, reminder.Value).ConfigureAwait(false);
            _pendingReminderTaskId = null;

            return $"Got it! I'll remind you on {reminder.Value:dd MMM yyyy 'at' HH:mm}.";
        }

        private async Task<string> HandleAddTaskAsync(string input)
        {
            string? title = _nlp.ExtractTaskTitle(input);
            if (string.IsNullOrWhiteSpace(title))
                return "What task should I add? Example: Add task - Review privacy settings";

            string description = _tasks.SuggestDescription(title);
            int id = await _tasks.AddTaskAsync(title, description).ConfigureAwait(false);

            _pendingReminderTaskId = id;

            return $"Task added with the description \"{description}\". Would you like a reminder?";
        }

        private async Task<string> HandleSetReminderAsync(string input, string lower)
        {
            DateTime? when = _nlp.ParseReminderDuration(lower) ?? _nlp.ParseTomorrow(lower);
            string? title = _nlp.ExtractTaskTitle(input);

            if (when == null)
                return "When should I remind you? Example: \"Remind me in 3 days\" or \"tomorrow\".";

            if (!string.IsNullOrWhiteSpace(title))
            {
                string desc = _tasks.SuggestDescription(title);
                await _tasks.AddTaskAsync(title, desc, when).ConfigureAwait(false);
                return $"Reminder set for '{title}' on {when.Value:dd MMM yyyy 'at' HH:mm}.";
            }

            return $"Reminder noted for {when.Value:dd MMM yyyy 'at' HH:mm}.";
        }

        private async Task<string> FormatTaskListAsync(string header)
        {
            var tasks = await _tasks.GetAllTasksAsync().ConfigureAwait(false);
            if (tasks.Count == 0)
                return "You have no tasks yet. Try: Add task - Enable two-factor authentication";

            var lines = tasks.Select(t =>
                $"#{t.Id} {(t.IsCompleted ? "[Done] " : "")}{t.Title} — {t.Description}" +
                (t.ReminderDate.HasValue ? $" (Reminder: {t.ReminderDate:dd MMM yyyy})" : ""));

            return header + "\n" + string.Join("\n", lines);
        }

        private async Task<string> HandleCompleteTaskAsync(string lower)
        {
            int? id = _nlp.ExtractTaskId(lower);
            if (id == null)
            {
                _pendingTaskId = null;
                _pendingAction = PendingTaskAction.UpdatePickId;
                return await FormatTaskListAsync(
                    "Which task is done? Reply with the number, or use: complete task #1");
            }

            bool ok = await _tasks.UpdateTaskAsync(id.Value, isCompleted: true).ConfigureAwait(false);
            return ok ? $"Task #{id} marked as complete. Well done!" : $"Task #{id} was not found.";
        }

        private string BuildHelpText()
        {
            return "CyberBuddy commands:\n" +
                   "• Tasks: add task - [title], list tasks\n" +
                   "• Delete: delete task (pick from list) or delete task #2\n" +
                   "• Update: update task (pick from list) or update task #1 title to [name]\n" +
                   "• Mark done: update task → done, or complete task #1\n" +
                   "• Reminders: yes remind me in 3 days / tomorrow\n" +
                   "• Quiz: start quiz | Log: show activity log\n" +
                   "• help / exit | cancel (during any multi-step flow)";
        }

        // ===================== EXISTING CHAT LOGIC (unchanged) =====================

        private string PickRandomFromList(IReadOnlyList<string> candidates)
        {
            if (candidates.Count == 0) return "I do not have extra tips for that topic yet.";
            return candidates[_random.Next(candidates.Count)];
        }

        private string? TryRememberFromInput(string original, string lower)
        {
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

            Match interest = Regex.Match(original,
                @"(?:i'?m interested in|i care about|i like|my favorite topic is|favourite topic is|i want to learn about)\s+(.{3,60})",
                RegexOptions.IgnoreCase);
            if (interest.Success)
            {
                string topic = interest.Groups[1].Value.Trim().TrimEnd('.', '!', '?');
                _memory.FavoriteCyberTopic = topic;
                _activeTopicKey = MapLooseTopicToKey(topic.ToLowerInvariant()) ?? _activeTopicKey;
                return $"Great! I will remember that you are interested in {topic}.";
            }

            return null;
        }

        private bool IsFollowUpPhrase(string lower) =>
            _followUpPhrases.Any(p => lower.Contains(p, StringComparison.Ordinal));

        private bool TryHandleFollowUp(string lower, out string? reply)
        {
            reply = null;
            if (_activeTopicKey == null || !_responsesByTopic.ContainsKey(_activeTopicKey)) return false;
            if (!IsFollowUpPhrase(lower)) return false;

            reply = _memory.WithTopicRecall(_pickRandomResponse(_responsesByTopic[_activeTopicKey]));
            return true;
        }

        private string? TryKeywordResponse(string lower, UserSentiment sentiment)
        {
            if (ContainsAny(lower, "how are you", "how r you", "how are u"))
            {
                _activeTopicKey = null;
                return $"I am running smoothly, {_memory.UserName}. Ready to help you stay safer online.";
            }

            if (ContainsAny(lower, "purpose", "what do you do", "who are you"))
            {
                _activeTopicKey = null;
                return "I am CyberBuddy — cybersecurity tips, task assistant, quiz, and activity log.";
            }

            if (ContainsAny(lower, "phishing", "phish")) return AssignTopicAndReply("phishing", sentiment);
            if (ContainsAny(lower, "password", "passcode", "passphrase")) return AssignTopicAndReply("password", sentiment);
            if (ContainsAny(lower, "scam", "fraud", "con artist")) return AssignTopicAndReply("scam", sentiment);
            if (ContainsAny(lower, "privacy", "private data", "personal data")) return AssignTopicAndReply("privacy", sentiment);
            if (ContainsAny(lower, "malware", "virus", "trojan", "ransomware")) return AssignTopicAndReply("malware", sentiment);
            if (ContainsAny(lower, "safe browsing", "browse safely", "https", "public wi-fi", "public wifi")) return AssignTopicAndReply("safebrowse", sentiment);
            if (ContainsAny(lower, "two-factor", "2fa", "mfa", "multi-factor")) return AssignTopicAndReply("password", sentiment);

            return null;
        }

        private string AssignTopicAndReply(string topicKey, UserSentiment sentiment)
        {
            _activeTopicKey = topicKey;
            string tip = _pickRandomResponse(_responsesByTopic[topicKey]);

            if (sentiment == UserSentiment.Worried && topicKey == "scam")
                return "It is completely understandable to feel that way.\n\n" + _memory.WithTopicRecall(tip);

            if (sentiment == UserSentiment.Worried)
                return "It is okay to feel unsure about security topics.\n\n" + _memory.WithTopicRecall(tip);

            if (sentiment == UserSentiment.Curious)
                return $"Great question spirit, {_memory.UserName}! {_memory.WithTopicRecall(tip)}";

            if (sentiment == UserSentiment.Frustrated)
                return $"I hear you. Here is one clear step: {_memory.WithTopicRecall(tip)}";

            return _memory.WithTopicRecall(tip);
        }

        private static UserSentiment DetectSentiment(string lower)
        {
            if (ContainsAny(lower, "worried", "anxious", "scared", "nervous", "afraid", "fear", "stress", "overwhelmed"))
                return UserSentiment.Worried;
            if (ContainsAny(lower, "curious", "wondering", "interested to know", "tell me why", "how does"))
                return UserSentiment.Curious;
            if (ContainsAny(lower, "frustrated", "annoyed", "angry", "fed up", "tired of", "sick of"))
                return UserSentiment.Frustrated;
            return UserSentiment.Neutral;
        }

        private static string? MapLooseTopicToKey(string t)
        {
            if (t.Contains("password")) return "password";
            if (t.Contains("phish")) return "phishing";
            if (t.Contains("scam") || t.Contains("fraud")) return "scam";
            if (t.Contains("privacy")) return "privacy";
            if (t.Contains("malware") || t.Contains("virus")) return "malware";
            if (t.Contains("browse") || t.Contains("wifi")) return "safebrowse";
            return null;
        }

        private Dictionary<string, List<string>> BuildTopicResponses()
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["password"] = new List<string> { "Use strong, unique passwords for each account.", "Turn on multi-factor authentication.", "Use a password manager.", "Change breached passwords immediately." },
                ["scam"] = new List<string> { "Slow down if someone pressures you to pay quickly.", "Verify messages through official channels.", "Be sceptical of too-good-to-be-true offers.", "Never share one-time PINs with callers." },
                ["privacy"] = new List<string> { "Review app permissions regularly.", "Limit public posts on social media.", "Use private browsing on shared devices.", "Check social account privacy settings." },
                ["phishing"] = new List<string> { "Be cautious of emails asking for personal information.", "Hover over links before clicking.", "Look for odd spelling or generic greetings.", "Pause and verify urgent emails." },
                ["malware"] = new List<string> { "Keep your OS and apps updated.", "Download only from official sources.", "Scan unexpected attachments.", "Disable macros from unknown senders." },
                ["safebrowse"] = new List<string> { "Prefer HTTPS sites.", "Do not ignore browser warnings.", "Use separate browsers for banking.", "Clear saved passwords on shared PCs." }
            };
        }

        private static bool ContainsAny(string input, params string[] needles)
        {
            foreach (string n in needles)
            {
                if (input.Contains(n, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}