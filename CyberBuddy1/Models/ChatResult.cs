namespace CyberBuddy1.Models
{
    public sealed class ChatResult
    {
        public string Message { get; init; } = string.Empty;
        public bool ExitRequested { get; init; }
    }
}