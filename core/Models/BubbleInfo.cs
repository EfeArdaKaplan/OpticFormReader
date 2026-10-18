namespace core.Models
{
    public class BubbleInfo
    {
        public int SectionId { get; set; }

        public int QuestionNumber { get; set; }

        public string Option { get; set; } = string.Empty;

        public double Xmm { get; set; }

        public double Ymm { get; set; }

        public double RadiusMm { get; set; }
    }
}