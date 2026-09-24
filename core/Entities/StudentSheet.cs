using System;
using System.Collections.Generic;
using System.Text;

namespace core.Entities
{
    public class StudentSheet
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public string ProcessingStatus { get; set; } = "Pending"; // Pending, Processing, Done, Error
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
