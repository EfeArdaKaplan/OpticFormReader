using System;
using System.Collections.Generic;
using System.Text;

namespace core.DTOs
{
    public class CreateExamTemplateDto
    {
        public string Name { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public int OptionCount { get; set; } = 5;
    }
}
