namespace core.DTOs
{
    public class CreateExamSectionDto
    {
        public string Name { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public int OptionCount { get; set; } = 5;
    }
}