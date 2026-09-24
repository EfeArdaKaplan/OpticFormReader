using System;
using System.Collections.Generic;
using System.Text;

namespace core.Entities
{
    public class Exam
    {
        public int Id { get; set; }
        public int OwnerUserId { get; set; }
        public int ExamTemplateId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsBulk { get; set; } // Tekli mi Toplu mu giriş
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
