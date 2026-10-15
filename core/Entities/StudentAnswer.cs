namespace core.Entities
{
    public class StudentAnswer
    {
        public int Id { get; set; }
        public int StudentSheetId { get; set; }
        public int ExamSectionId { get; set; }                       // YENİ
        public int QuestionNumber { get; set; }                      // dersin içinde 1'den başlar
        public string SelectedOption { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
    }
}