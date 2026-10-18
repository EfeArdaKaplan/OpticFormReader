namespace core.DTOs
{
    public class UploadBatchDto
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string FileKey { get; set; } = string.Empty;
        public int PageCount { get; set; }
        public string ProcessingStatus { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }
}