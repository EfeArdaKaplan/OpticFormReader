namespace core.DTOs
{
    public class ExamResultSummaryDto
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public int TotalStudents { get; set; }
        public int TotalQuestions { get; set; }
        public double ClassAverage { get; set; }
        public double HighestScore { get; set; }
        public double LowestScore { get; set; }
        public List<StudentResultItemDto> Students { get; set; } = new();
    }

    public class StudentResultItemDto
    {
        public int StudentSheetId { get; set; }
        public int Rank { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string TcNo { get; set; } = string.Empty;
        public string Booklet { get; set; } = "A";
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int EmptyCount { get; set; }
        public double NetScore { get; set; }
        public double TotalScore { get; set; }
        public bool NeedsReview { get; set; }
        public string? ReviewReason { get; set; }
        public List<SectionResultSummaryDto> SectionResults { get; set; } = new();
    }

    public class SectionResultSummaryDto
    {
        public int SectionId { get; set; }
        public string SectionName { get; set; } = string.Empty;
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int EmptyCount { get; set; }
        public double Score { get; set; }
    }
}