namespace core.Entities
{
    // Öğrenci başına, ders başına bir satır
    public class ExamResult
    {
        public int Id { get; set; }
        public int StudentSheetId { get; set; }
        public int ExamSectionId { get; set; }                       // YENİ
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int EmptyCount { get; set; }
        public double Score { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }
}