namespace core.DTOs
{
    public class ExamTemplateDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string InstitutionName { get; set; } = string.Empty;

        public int StudentNoDigits { get; set; }

        public int TotalQuestionCount { get; set; }

        public List<ExamSectionDto> Sections { get; set; } = new();

        public DateTime CreatedAt { get; set; }
    }
}