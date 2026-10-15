using System;
using System.Collections.Generic;
using System.Text;

namespace core.Entities
{
    public class AnswerKey
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public int ExamSectionId { get; set; }               // YENİ: hangi dersin anahtarı
        public bool IsScanned { get; set; }
        public string AnswersJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
