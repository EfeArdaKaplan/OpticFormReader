using core.Interfaces;
using System.Text;

namespace business.Services
{
    public class ExportService : IExportService
    {
        private readonly IResultService _resultService;

        public ExportService(IResultService resultService)
        {
            _resultService = resultService;
        }

        /// <summary>
        /// CSV Formula Injection (Excel DDE/Cmd enjeksiyonu) koruması:
        /// '=', '+', '-', '@', '\t', '\r' ile başlayan hücrelerin başına tek tırnak ekler ve çift tırnakları kaçırır.
        /// </summary>
        private static string SanitizeCsvCell(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string cleaned = value.Replace("\"", "\"\"");
            char first = cleaned[0];
            if (first is '=' or '+' or '-' or '@' or '\t' or '\r')
            {
                cleaned = "'" + cleaned;
            }
            return cleaned;
        }

        public async Task<(byte[] FileBytes, string FileName, string ContentType)> ExportResultsCsvAsync(int examId, int ownerUserId)
        {
            var summary = await _resultService.GetExamResultsAsync(examId, ownerUserId);

            var sb = new StringBuilder();

            // Sınav Başlığı ve Genel Bilgiler
            sb.AppendLine($"# Sınav:;\"{SanitizeCsvCell(summary.ExamTitle)}\"");
            sb.AppendLine($"# Toplam Öğrenci:;{summary.TotalStudents}");
            sb.AppendLine($"# Sınıf Ortalaması:;{summary.ClassAverage}");
            sb.AppendLine($"# En Yüksek Puan:;{summary.HighestScore}");
            sb.AppendLine($"# En Düşük Puan:;{summary.LowestScore}");
            sb.AppendLine();

            // Dinamik Sütun Başlıkları (Dersleri otomatik ekler)
            var sampleStudent = summary.Students.FirstOrDefault();
            var sectionNames = sampleStudent?.SectionResults.Select(r => r.SectionName).ToList() ?? new List<string>();

            var header = new StringBuilder();
            header.Append("Sıra;Öğrenci No;T.C. Kimlik No;Adı Soyadı;Kitapçık;");

            foreach (var secName in sectionNames)
            {
                string safeSec = SanitizeCsvCell(secName);
                header.Append($"{safeSec} (Doğru);{safeSec} (Yanlış);{safeSec} (Net);{safeSec} (Puan);");
            }

            header.Append("Toplam Doğru;Toplam Yanlış;Toplam Boş;Toplam Net;Toplam Puan;Durum");
            sb.AppendLine(header.ToString());

            // Öğrenci Satırları
            foreach (var s in summary.Students)
            {
                var row = new StringBuilder();
                row.Append($"{s.Rank};");
                row.Append($"\"{SanitizeCsvCell(s.StudentNumber)}\";");
                row.Append($"\"{SanitizeCsvCell(s.TcNo)}\";");
                row.Append($"\"{SanitizeCsvCell(s.FullName)}\";");
                row.Append($"\"{SanitizeCsvCell(s.Booklet)}\";");

                // Ders bazında başarılar
                foreach (var secName in sectionNames)
                {
                    var sec = s.SectionResults.FirstOrDefault(r => r.SectionName == secName);
                    if (sec != null)
                    {
                        row.Append($"{sec.CorrectCount};{sec.WrongCount};{sec.CorrectCount};{sec.Score};");
                    }
                    else
                    {
                        row.Append("0;0;0;0;");
                    }
                }

                string status = s.NeedsReview ? $"İncelenmeli ({s.ReviewReason})" : "Normal";
                row.Append($"{s.CorrectCount};{s.WrongCount};{s.EmptyCount};{s.NetScore};{s.TotalScore};\"{SanitizeCsvCell(status)}\"");
                sb.AppendLine(row.ToString());
            }

            // Excel'in Türkçe karakterleri doğru tanıması için UTF-8 BOM ekliyoruz
            var preamble = Encoding.UTF8.GetPreamble();
            var dataBytes = Encoding.UTF8.GetBytes(sb.ToString());

            var finalBytes = new byte[preamble.Length + dataBytes.Length];
            Buffer.BlockCopy(preamble, 0, finalBytes, 0, preamble.Length);
            Buffer.BlockCopy(dataBytes, 0, finalBytes, preamble.Length, dataBytes.Length);

            string safeTitle = string.Join("_", summary.ExamTitle.Split(Path.GetInvalidFileNameChars())).Trim();
            if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = $"Sinav_{examId}";

            string fileName = $"{safeTitle}_Sonuclar.csv";

            return (finalBytes, fileName, "text/csv; charset=utf-8");
        }
    }
}
