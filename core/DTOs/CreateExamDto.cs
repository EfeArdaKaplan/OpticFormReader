namespace core.DTOs
{
    public class CreateExamDto
    {
        public int ExamTemplateId { get; set; }

        public string Title { get; set; } = string.Empty;

        public bool IsBulk { get; set; }

        public double TotalScore { get; set; } = 100;

        public double PercentRatio { get; set; } = 100;

        public double PointPerQuestion { get; set; }
    }
}