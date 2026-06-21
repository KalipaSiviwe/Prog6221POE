using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberBuddy1.Models;
using CyberBuddy1.Services;

namespace CyberBuddy1
{
    public partial class MainWindow : Window
    {
        private readonly ChatMemory _memory = new();
        private readonly DatabaseService _database = new();
        private readonly ActivityLogService _activityLog = new();
        private readonly TaskAssistantService _taskService;
        private readonly QuizService _quizService;
        private readonly NlpIntentDetector _nlp = new();
        private readonly ChatEngine _engine;
        private readonly DispatcherTimer _reminderTimer;

        public MainWindow()
        {
            InitializeComponent();

            _taskService = new TaskAssistantService(_database, _activityLog);
            _quizService = new QuizService(_activityLog);
            _engine = new ChatEngine(_memory, _taskService, _quizService, _activityLog, _nlp);

            AsciiText.Text = AsciiArtProvider.CyberBuddyBanner;

            _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _reminderTimer.Tick += ReminderTimer_Tick;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_database.TestConnection(out string dbError))
            {
                AppendBotMessage($"Database connection failed: {dbError}\nCheck appsettings.json and ensure SQL Server is running.");
            }
            else
            {
                AppendBotMessage("Connected to SQL Server database.");
            }

            PlayVoiceGreeting();
            AppendBotMessage("Welcome! Try: \"Add task - Review privacy settings\", \"list tasks\", \"start quiz\", or \"show activity log\".");

            _reminderTimer.Start();
            await RefreshTaskListAsync();
            RefreshActivityLog(recentOnly: true);
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _reminderTimer.Stop();
        }

        private async void ReminderTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var due = await _taskService.GetDueRemindersAsync();
                foreach (var task in due)
                {
                    string msg = $"Reminder: '{task.Title}' — {task.Description}";
                    AppendBotMessage(msg);
                    MessageBox.Show(msg, "CyberBuddy Reminder", MessageBoxButton.OK, MessageBoxImage.Information);
                    await _taskService.MarkReminderNotifiedAsync(task.Id);
                    _activityLog.Log($"Reminder triggered for '{task.Title}'.");
                }

                RefreshActivityLog(recentOnly: true);
            }
            catch
            {
                // ignore timer errors
            }
        }

        private void PlayVoiceGreeting()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Greetings.wav");
            try
            {
                if (!File.Exists(path))
                {
                    AppendBotMessage("(Voice file missing: place Greetings.wav under Assets and rebuild.)");
                    return;
                }

                using var player = new SoundPlayer(path);
                player.Load();
                player.Play();
            }
            catch (Exception ex)
            {
                AppendBotMessage($"Could not play greeting audio: {ex.Message}");
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e) => SendUserInput();

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendUserInput();
                e.Handled = true;
            }
        }

        private async void SendUserInput()
        {
            string text = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            InputBox.Clear();
            AppendUserMessage(text);

            SendButton.IsEnabled = false;
            InputBox.IsEnabled = false;

            bool exit = false;

            try
            {
                ChatResult result = await _engine.ProcessInputAsync(text);
                AppendBotMessage(result.Message);
                exit = result.ExitRequested;

                if (!exit)
                {
                    await RefreshTaskListAsync();
                    RefreshActivityLog(recentOnly: true);
                }
            }
            catch (Exception ex)
            {
                AppendBotMessage($"Error: {ex.Message}");
            }
            finally
            {
                if (!exit)
                {
                    InputBox.IsEnabled = true;
                    SendButton.IsEnabled = true;
                    InputBox.Focus();
                }
            }
        }

        private async void TasksTab_GotFocus(object sender, RoutedEventArgs e) => await RefreshTaskListAsync();

        private async void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            string title = TaskTitleBox.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                MessageBox.Show("Enter a task title.", "CyberBuddy");
                return;
            }

            string desc = string.IsNullOrWhiteSpace(TaskDescBox.Text)
                ? _taskService.SuggestDescription(title)
                : TaskDescBox.Text.Trim();

            DateTime? reminder = TaskReminderPicker.SelectedDate?.Date.AddHours(9);

            try
            {
                await _taskService.AddTaskAsync(title, desc, reminder);

                TaskTitleBox.Clear();
                TaskDescBox.Clear();
                TaskReminderPicker.SelectedDate = null;

                await RefreshTaskListAsync();
                RefreshActivityLog(recentOnly: true);
                AppendBotMessage($"Task '{title}' added from the Tasks tab.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not add task: {ex.Message}", "Database Error");
            }
        }

        private async void CompleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskListView.SelectedItem is not CyberTask task)
            {
                MessageBox.Show("Select a task first.", "CyberBuddy");
                return;
            }

            await _taskService.MarkCompleteAsync(task.Id);
            await RefreshTaskListAsync();
            RefreshActivityLog(recentOnly: true);
        }

        private async void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskListView.SelectedItem is not CyberTask task)
            {
                MessageBox.Show("Select a task first.", "CyberBuddy");
                return;
            }

            if (MessageBox.Show($"Delete task '{task.Title}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await _taskService.DeleteTaskAsync(task.Id);
                await RefreshTaskListAsync();
                RefreshActivityLog(recentOnly: true);
            }
        }

        private async void RefreshTasksButton_Click(object sender, RoutedEventArgs e) => await RefreshTaskListAsync();

        private async Task RefreshTaskListAsync()
        {
            try
            {
                var tasks = await _taskService.GetAllTasksAsync();
                TaskListView.ItemsSource = tasks;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load tasks: {ex.Message}", "Database Error");
            }
        }

        private void StartQuizButton_Click(object sender, RoutedEventArgs e)
        {
            _quizService.StartQuiz();
            ShowCurrentQuizQuestion();
            RefreshActivityLog(recentOnly: true);
        }

        private void ShowCurrentQuizQuestion()
        {
            QuizOptionsPanel.Children.Clear();
            var q = _quizService.GetCurrentQuestion();

            if (q == null)
            {
                QuizQuestionText.Text = _quizService.GetFinalScoreMessage();
                QuizScoreText.Text = "Quiz complete";
                return;
            }

            QuizQuestionText.Text = q.Question;
            QuizScoreText.Text = $"Score: {_quizService.CurrentScore} / {_quizService.TotalQuestions}";

            for (int i = 0; i < q.Options.Count; i++)
            {
                int index = i;
                var btn = new Button
                {
                    Content = $"{(char)('A' + i)}) {q.Options[i]}",
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(12, 10, 12, 10),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = (Brush)FindResource("BrushAccentDim"),
                    Foreground = (Brush)FindResource("BrushBgDeep"),
                    BorderThickness = new Thickness(0),
                    FontSize = 14,
                    Cursor = Cursors.Hand
                };

                btn.Click += (_, _) =>
                {
                    string feedback = _quizService.SubmitAnswer(((char)('a' + index)).ToString());
                    MessageBox.Show(feedback, "Quiz Feedback");
                    ShowCurrentQuizQuestion();
                    RefreshActivityLog(recentOnly: true);
                };

                QuizOptionsPanel.Children.Add(btn);
            }
        }

        private void ShowRecentLogButton_Click(object sender, RoutedEventArgs e) => RefreshActivityLog(recentOnly: true);

        private void ShowFullLogButton_Click(object sender, RoutedEventArgs e) => RefreshActivityLog(recentOnly: false);

        private void RefreshActivityLog(bool recentOnly)
        {
            var entries = recentOnly ? _activityLog.GetRecent() : _activityLog.GetAll();
            ActivityLogList.ItemsSource = entries.Select(e => e.Formatted).ToList();
        }

        private void AppendUserMessage(string text)
        {
            ChatPanel.Children.Add(BuildBubble(text, isUser: true));
            ScrollChatToEnd();
        }

        private void AppendBotMessage(string text)
        {
            ChatPanel.Children.Add(BuildBubble(text, isUser: false));
            ScrollChatToEnd();
        }

        private static Border BuildBubble(string text, bool isUser)
        {
            var brush = (SolidColorBrush)Application.Current.Resources[isUser ? "BrushUserBubble" : "BrushBotBubble"];
            var border = new Border
            {
                Background = brush,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 10),
                MaxWidth = 720,
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            border.Child = new TextBlock
            {
                Text = text,
                Foreground = (Brush)Application.Current.Resources["BrushText"],
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };

            return border;
        }

        private void ScrollChatToEnd()
        {
            ChatScroll.UpdateLayout();
            ChatScroll.ScrollToEnd();
        }
    }
}