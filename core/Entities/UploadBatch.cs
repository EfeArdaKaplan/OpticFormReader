namespace core.Entities
{
    // Kullanıcının yüklediği her dosya (PDF veya görüntü) için bir kayıt
    public class UploadBatch
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;           // PDF, JPG, PNG
        public string FileKey { get; set; } = string.Empty;          // SeaweedFS'teki anahtar
        public int PageCount { get; set; }                           // PDF'te sayfa sayısı, görüntüde 1
        public string ProcessingStatus { get; set; } = "Pending";    // Pending, Processing, Done, Error
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}