using CyberBuddy1.Models;
using CyberBuddy1.Services;
using System.IO;
using System.Media;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CyberBuddy1
{
    public partial class MainWindow : Window
    {
        private readonly ChatMemory _memory = new();
        private readonly ChatEngine _engine;

        public MainWindow()
        {
            InitializeComponent();
            _engine = new ChatEngine(_memory);
            AsciiText.Text = AsciiArtProvider.CyberBuddyBanner;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PlayVoiceGreeting();
            AppendBotMessage("Welcome! I am CyberBuddy. You can tell me your name with \"My name is ...\" or share a topic you care about. A voice greeting should have played if Greetings.wav is in the Assets folder.");
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

        private void SendUserInput()
        {
            string text = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            InputBox.Clear();
            AppendUserMessage(text);

            string reply = _engine.ProcessInput(text, out bool exit);
            AppendBotMessage(reply);

            if (exit)
            {
                InputBox.IsEnabled = false;
                SendButton.IsEnabled = false;
            }
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

            var tb = new TextBlock
            {
                Text = text,
                Foreground = (Brush)Application.Current.Resources["BrushText"],
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };

            border.Child = tb;
            return border;
        }

        private void ScrollChatToEnd()
        {
            ChatScroll.UpdateLayout();
            ChatScroll.ScrollToEnd();
        }
    }
}
