namespace core.DTOs
{
    public class StudentSheetDto
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public int UploadBatchId { get; set; }
        public int PageNumber { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string TcNo { get; set; } = string.Empty;
        public string? Booklet { get; set; }
        public string ImageKey { get; set; } = string.Empty;
        public string ProcessingStatus { get; set; } = string.Empty;
        public bool NeedsReview { get; set; }
        public string? ReviewReason { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}