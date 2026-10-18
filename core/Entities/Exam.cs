namespace core.Entities
{
    public class Exam
    {
        public int Id { get; set; }

        public int OwnerUserId { get; set; }

        public int ExamTemplateId { get; set; }

        public string Title { get; set; } = string.Empty;

        public bool IsBulk { get; set; }

        public double TotalScore { get; set; } = 100;

        public double PercentRatio { get; set; } = 100;

        public double PointPerQuestion { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}