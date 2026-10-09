namespace core.DTOs
{
    public class CreateExamDto
    {
        public int ExamTemplateId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsBulk { get; set; }
    }
}