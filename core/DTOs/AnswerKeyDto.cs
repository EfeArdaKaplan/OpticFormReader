namespace core.DTOs
{
    public class AnswerKeyDto
    {
        public int Id { get; set; }

        public int ExamId { get; set; }

        public int ExamSectionId { get; set; }

        public string SectionName { get; set; } = string.Empty;

        public string Booklet { get; set; } = "A";

        public bool IsScanned { get; set; }

        public Dictionary<int, string> Answers { get; set; } = new();

        public DateTime CreatedAt { get; set; }
    }
}