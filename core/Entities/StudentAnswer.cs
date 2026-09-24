using System;
using System.Collections.Generic;
using System.Text;

namespace core.Entities
{
    public class StudentAnswer
    {
        public int Id { get; set; }
        public int StudentSheetId { get; set; }
        public int QuestionNumber { get; set; }
        public string SelectedOption { get; set; } = string.Empty; // A, B, C, D, E, boş, belirsiz
        public double ConfidenceScore { get; set; } // güven skoru
    }
}
