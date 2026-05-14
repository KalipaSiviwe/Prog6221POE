namespace CyberBuddy1.Models
{
    /// <summary>
    /// Stores user details for recall later in the conversation (Part 2 memory requirement).
    /// </summary>
    public class ChatMemory
    {
        public string UserName { get; set; } = "User";

        public string? FavoriteCyberTopic { get; set; }

        public bool HasFavoriteTopic => !string.IsNullOrWhiteSpace(FavoriteCyberTopic);

        public string WithTopicRecall(string sentence)
        {
            if (!HasFavoriteTopic)
            {
                return sentence;
            }

            return $"As someone interested in {FavoriteCyberTopic}, {sentence}";
        }
    }
}
