namespace core.DTOs
{
    public class StudentDetailedReportDto
    {
        public int StudentSheetId { get; set; }
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public string StudentNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string TcNo { get; set; } = string.Empty;
        public string Booklet { get; set; } = "A";
        public int Rank { get; set; }
        public int TotalStudents { get; set; }
        public double ClassAverage { get; set; }
        public double TotalScore { get; set; }
        public double NetScore { get; set; }
        public int TotalCorrect { get; set; }
        public int TotalWrong { get; set; }
        public int TotalEmpty { get; set; }
        public List<SectionDetailedReportDto> Sections { get; set; } = new();
    }

    public class SectionDetailedReportDto
    {
        public int SectionId { get; set; }
        public string SectionName { get; set; } = string.Empty;
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int EmptyCount { get; set; }
        public double Score { get; set; }
        public List<QuestionDetailDto> Questions { get; set; } = new();
    }

    public class QuestionDetailDto
    {
        public int QuestionNumber { get; set; }
        public string CorrectOption { get; set; } = string.Empty;
        public string SelectedOption { get; set; } = string.Empty;
        public string Status { get; set; } = "Empty"; // "Correct", "Wrong", "Empty"
        public double Confidence { get; set; }
    }
}