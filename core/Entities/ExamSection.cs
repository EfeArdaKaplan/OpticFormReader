using System;
using System.Collections.Generic;
using System.Text;

namespace core.Entities
{
    public class ExamSection
    {
        public int Id { get; set; }
        public int ExamTemplateId { get; set; }
        public string Name { get; set; } = string.Empty;   // "Türkçe"
        public int Order { get; set; }                       // 1, 2, 3... formda görünme sırası
        public int QuestionCount { get; set; }               // 50
        public int OptionCount { get; set; } = 5;            // 4 veya 5
    }

}
