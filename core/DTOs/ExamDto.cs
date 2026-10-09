namespace core.DTOs
{
    public class ExamDto
    {
        public int Id { get; set; }
        public int ExamTemplateId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsBulk { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}