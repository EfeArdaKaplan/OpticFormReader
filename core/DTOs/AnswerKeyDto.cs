using System;
using System.Collections.Generic;
using System.Text;

namespace core.DTOs
{
    public class AnswerKeyDto
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public bool IsScanned { get; set; }
        public Dictionary<int, string> Answers { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
