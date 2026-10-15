namespace core.DTOs
{
    public class SectionAnswersDto
    {
        public int ExamSectionId { get; set; }

        // Anahtar: soru numarası (dersin içinde 1'den başlar), değer: doğru şık
        public Dictionary<int, string> Answers { get; set; } = new();
    }
}