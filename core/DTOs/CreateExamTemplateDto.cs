namespace core.DTOs
{
    public class CreateExamTemplateDto
    {
        public string Name { get; set; } = string.Empty;
        public int StudentNoDigits { get; set; } = 11;
        public List<CreateExamSectionDto> Sections { get; set; } = new();
    }
}