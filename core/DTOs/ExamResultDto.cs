using System;
using System.Collections.Generic;
using System.Text;

namespace core.DTOs
{
    public class ExamResultDto
    {
        public int StudentSheetId { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int EmptyCount { get; set; }
        public double Score { get; set; }
    }
}
