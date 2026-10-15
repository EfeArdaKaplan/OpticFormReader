namespace core.Entities
{
    public class AnswerKey
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public int ExamSectionId { get; set; }
        public string Booklet { get; set; } = "A";                   // YENİ: A, B, C veya D
        public bool IsScanned { get; set; }
        public string AnswersJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}