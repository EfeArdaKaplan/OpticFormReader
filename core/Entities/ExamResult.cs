using System;
using System.Collections.Generic;
using System.Text;

namespace core.Entities
{
    public class ExamResult
    {
        public int Id { get; set; }
        public int StudentSheetId { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int EmptyCount { get; set; }
        public double Score { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }
}
