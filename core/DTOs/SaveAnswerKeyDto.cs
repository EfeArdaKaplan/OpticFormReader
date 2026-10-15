namespace core.DTOs
{
    public class SaveAnswerKeyDto
    {
        public List<SectionAnswersDto> Sections { get; set; } = new();
    }
}