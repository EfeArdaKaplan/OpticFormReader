namespace core.DTOs
{
    public class SaveAnswerKeyDto
    {
        public string Booklet { get; set; } = "A";

        public List<SectionAnswersDto> Sections { get; set; } = new();
    }
}