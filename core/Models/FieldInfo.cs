namespace core.Models
{
    public class FieldInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public double Xmm { get; set; }

        public double Ymm { get; set; }

        public double WidthMm { get; set; }

        public double HeightMm { get; set; }

        public int CharacterCount { get; set; }
    }
}