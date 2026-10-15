namespace core.DTOs
{
    public class ExamSectionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Order { get; set; }
        public int QuestionCount { get; set; }
        public int OptionCount { get; set; }
    }
}