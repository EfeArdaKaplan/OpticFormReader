namespace core.DTOs
{
    public class EvaluationSummaryDto
    {
        public int ExamId { get; set; }
        public int TotalSheets { get; set; }
        public int EvaluatedSheets { get; set; }
        public int NeedsReviewCount { get; set; }
        public double ClassAverageScore { get; set; }
        public double HighestScore { get; set; }
        public double LowestScore { get; set; }
        public long DurationMs { get; set; }
    }
}