using System;
using System.Collections.Generic;
using System.Text;

namespace core.DTOs
{
    public class SaveAnswerKeyDto
    {
        // Anahtar: soru numarası, değer: doğru şık. Örn: { "1": "C", "2": "A" }
        public Dictionary<int, string> Answers { get; set; } = new();
    }
}
