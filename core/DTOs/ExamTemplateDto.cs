using System;
using System.Collections.Generic;
using System.Text;

namespace core.DTOs
{
    public class ExamTemplateDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public int OptionCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
