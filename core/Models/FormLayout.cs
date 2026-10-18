using System.Reflection;

namespace core.Models
{
    public class FormLayout
    {
        public int TemplateId { get; set; }

        public string InstitutionName { get; set; } = string.Empty;

        public string FormTitle { get; set; } = string.Empty;

        public double PageWidthMm { get; set; } = 210;

        public double PageHeightMm { get; set; } = 297;

        public int StudentNoDigits { get; set; }

        public int TotalQuestionCount { get; set; }

        public List<MarkerInfo> Markers { get; set; } = new();

        public List<FieldInfo> Fields { get; set; } = new();

        public List<SectionLayout> Sections { get; set; } = new();

        public List<BubbleInfo> Bubbles { get; set; } = new();
    }
}