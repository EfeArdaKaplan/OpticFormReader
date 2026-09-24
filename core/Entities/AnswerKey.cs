using System;
using System.Collections.Generic;
using System.Text;

namespace core.Entities
{
    public class AnswerKey
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public bool IsScanned { get; set; } // Taranan mı Manuel mi girilmiş
        public string AnswersJson { get; set; } = string.Empty; // Soru-cevap eşlemesi
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
