namespace core.Models
{
    public class SectionLayout
    {
        public int SectionId { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Order { get; set; }

        public int QuestionStart { get; set; }

        public int QuestionEnd { get; set; }
        public int TotalQuestionCount { get; set; }

        public double Xmm { get; set; }

        public double Ymm { get; set; }

        public double WidthMm { get; set; }

        public double HeightMm { get; set; }
    }
}