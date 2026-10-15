namespace core.Entities
{
    public class StudentSheet
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public int UploadBatchId { get; set; }                       // YENİ: hangi dosyadan geldi
        public int PageNumber { get; set; } = 1;                     // YENİ: PDF'teki sayfa sırası

        public string StudentNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;        // YENİ (StudentName yerine)
        public string LastName { get; set; } = string.Empty;         // YENİ
        public string TcNo { get; set; } = string.Empty;             // YENİ
        public string? Booklet { get; set; }                         // YENİ: okunamazsa null

        public string ImageKey { get; set; } = string.Empty;         // ImagePath yerine: SeaweedFS anahtarı
        public string ProcessingStatus { get; set; } = "Pending";
        public bool NeedsReview { get; set; }                        // YENİ: şüpheli kâğıt
        public string? ReviewReason { get; set; }                    // YENİ: neden şüpheli
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}