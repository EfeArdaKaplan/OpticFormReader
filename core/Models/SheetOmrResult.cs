namespace core.Models
{
    public class SheetOmrResult
    {
        public string? Booklet { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string TcNo { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // (SectionId, QuestionNumber) -> (Seçilen Şık, Güven Skoru 0.0 - 1.0)
        public Dictionary<(int SectionId, int QuestionNumber), (string Option, double Confidence)> Answers { get; set; } = new();

        public bool NeedsReview { get; set; }
        public string? ReviewReason { get; set; }
    }
}