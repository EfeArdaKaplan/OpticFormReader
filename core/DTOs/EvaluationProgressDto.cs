namespace core.DTOs
{
    public class EvaluationProgressDto
    {
        public int ExamId { get; set; }
        public int TotalSheets { get; set; }
        public int ProcessedSheets { get; set; }
        public int Percent { get; set; }
        public string CurrentStatus { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
    }
}