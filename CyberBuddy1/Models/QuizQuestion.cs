namespace CyberBuddy1.Models
{
    public class QuizQuestion
    {
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public int CorrectIndex { get; set; }
        public string Explanation { get; set; } = string.Empty;
        public bool IsTrueFalse => Options.Count == 2;
    }
}