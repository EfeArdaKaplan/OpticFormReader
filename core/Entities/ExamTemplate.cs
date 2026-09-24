using System;
using System.Collections.Generic;
using System.Text;

namespace core.Entities
{
    public class ExamTemplate
    {
        public int Id { get; set; }
        public int OwnerUserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public int OptionCount { get; set; } = 5; //  A B C D E gibi
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
