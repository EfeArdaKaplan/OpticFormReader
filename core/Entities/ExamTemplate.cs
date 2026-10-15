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
        public int StudentNoDigits { get; set; } = 11;      
        public string SourceType { get; set; } = "Standard"; 
        public string LayoutJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<ExamSection> Sections { get; set; } = new(); 
    }
}
