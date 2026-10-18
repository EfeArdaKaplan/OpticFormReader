using core.Entities;
using core.Models;
using business.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SkiaSharp;
using System.Globalization;
using System.Text;

namespace SampleSheetGenerator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("OptiMatrix Örnek Doldurulmuş Sınav Kağıdı Üretici Başlatılıyor...");
            QuestPDF.Settings.License = LicenseType.Community;

            var template = new ExamTemplate
            {
                Id = 1,
                Name = "Matematik 1. Ara Sınav",
                InstitutionName = "MÜHENDİSLİK FAKÜLTESİ",
                StudentNoDigits = 8,
                Sections = new List<ExamSection>
                {
                    new ExamSection { Id = 1, Name = "Temel Matematik", Order = 1, QuestionCount = 20, OptionCount = 5 },
                    new ExamSection { Id = 2, Name = "Geometri", Order = 2, QuestionCount = 10, OptionCount = 5 }
                }
            };

            var formService = new FormGeneratorService();
            var layout = formService.BuildLayout(template);

            // Klasör yolları
            var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\.."));
            var targetSampleDir = Path.Combine(projectDir, "sample_sheets");
            var userDownloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "ornek_optik_formlar");

            Directory.CreateDirectory(targetSampleDir);
            Directory.CreateDirectory(userDownloadsDir);

            Console.WriteLine($"Hedef klasör 1: {targetSampleDir}");
            Console.WriteLine($"Hedef klasör 2: {userDownloadsDir}");

            // 5 Farklı Başarı ve Karakterde Öğrenci Profili
            var students = new[]
            {
                new StudentProfile
                {
                    Name = "KEMAL SUNAL",
                    StudentNo = "20261001",
                    TcNo = "12345678901",
                    Booklet = "A",
                    ClassBranch = "1-A",
                    AnswerSeed = 42
                },
                new StudentProfile
                {
                    Name = "SENER SEN",
                    StudentNo = "20261002",
                    TcNo = "23456789012",
                    Booklet = "A",
                    ClassBranch = "1-A",
                    AnswerSeed = 99
                },
                new StudentProfile
                {
                    Name = "ILYAS SALMAN",
                    StudentNo = "20261003",
                    TcNo = "34567890123",
                    Booklet = "B",
                    ClassBranch = "1-B",
                    AnswerSeed = 77
                },
                new StudentProfile
                {
                    Name = "HALIT AKCATEPE",
                    StudentNo = "20261004",
                    TcNo = "45678901234",
                    Booklet = "B",
                    ClassBranch = "1-B",
                    AnswerSeed = 13
                },
                new StudentProfile
                {
                    Name = "MUNIR OZKUL",
                    StudentNo = "20261005",
                    TcNo = "56789012345",
                    Booklet = "A",
                    ClassBranch = "1-C",
                    AnswerSeed = 100
                }
            };

            var options = new[] { "A", "B", "C", "D", "E" };

            for (int i = 0; i < students.Length; i++)
            {
                var s = students[i];
                Console.WriteLine($"[{i + 1}/5] {s.Name} optik formu üretiliyor...");

                // 1) Temel SVG şablonunu al
                var baseSvg = FormGeneratorService.BuildSvg(layout);

                // 2) Öğrencinin dolduracağı baloncukları hesapla
                var filledBubbles = new List<BubbleInfo>();

                // Kitapçık
                var bookletBubble = layout.Bubbles.FirstOrDefault(b =>
                    b.SectionId == FormGeneratorService.BookletSectionId &&
                    b.Option.Equals(s.Booklet, StringComparison.OrdinalIgnoreCase));
                if (bookletBubble != null) filledBubbles.Add(bookletBubble);

                // Öğrenci Numarası
                for (int pos = 1; pos <= layout.StudentNoDigits; pos++)
                {
                    if (pos <= s.StudentNo.Length)
                    {
                        var digitChar = s.StudentNo[pos - 1].ToString();
                        var digitBubble = layout.Bubbles.FirstOrDefault(b =>
                            b.SectionId == FormGeneratorService.StudentNumberSectionId &&
                            b.QuestionNumber == pos &&
                            b.Option == digitChar);
                        if (digitBubble != null) filledBubbles.Add(digitBubble);
                    }
                }

                // TC Kimlik No
                for (int pos = 1; pos <= 11; pos++)
                {
                    if (pos <= s.TcNo.Length)
                    {
                        var digitChar = s.TcNo[pos - 1].ToString();
                        var digitBubble = layout.Bubbles.FirstOrDefault(b =>
                            b.SectionId == FormGeneratorService.TcNumberSectionId &&
                            b.QuestionNumber == pos &&
                            b.Option == digitChar);
                        if (digitBubble != null) filledBubbles.Add(digitBubble);
                    }
                }

                // Ders Soruları
                var rnd = new Random(s.AnswerSeed);
                foreach (var sec in template.Sections)
                {
                    for (int q = 1; q <= sec.QuestionCount; q++)
                    {
                        // %88 işaretleme oranı (%12 boş)
                        if (rnd.NextDouble() > 0.12)
                        {
                            var chosenOpt = options[rnd.Next(options.Length)];
                            var qBubble = layout.Bubbles.FirstOrDefault(b =>
                                b.SectionId == sec.Id &&
                                b.QuestionNumber == q &&
                                b.Option == chosenOpt);
                            if (qBubble != null) filledBubbles.Add(qBubble);
                        }
                    }
                }

                // 3) Baloncukları koyu kurşun kalem siyahı ile SVG üzerine ekle
                var overlays = new StringBuilder();
                foreach (var b in filledBubbles)
                {
                    overlays.AppendLine(
                        $"<circle cx=\"{F(b.Xmm)}\" cy=\"{F(b.Ymm)}\" r=\"{F(b.RadiusMm * 0.95)}\" fill=\"#1a1a1a\" stroke=\"#111111\" stroke-width=\"0.15\"/>");
                }

                // El Yazısı Bilgi Alanı Doldurma
                overlays.AppendLine($"<text x=\"36\" y=\"36.5\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"2.8\" font-weight=\"bold\" fill=\"#111827\">{s.Name}</text>");
                overlays.AppendLine($"<text x=\"36\" y=\"42.0\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"2.5\" font-weight=\"bold\" fill=\"#111827\">{s.ClassBranch}</text>");
                overlays.AppendLine("<path d=\"M125 41.5 C128 38.5, 133 43.5, 137 39.5 S143 42.5, 148 40.5\" stroke=\"#1e293b\" stroke-width=\"0.55\" fill=\"none\"/>");

                // SVG kapanışından önce enjekte et
                var finalSvg = baseSvg.Replace("</svg>", overlays.ToString() + "</svg>");

                // 4) QuestPDF ile Vektör PDF üret
                var pdfBytes = Document.Create(document =>
                {
                    document.Page(page =>
                    {
                        page.Size((float)layout.PageWidthMm, (float)layout.PageHeightMm, Unit.Millimetre);
                        page.Margin(0);
                        page.Content().Svg(finalSvg).FitArea();
                    });
                }).GeneratePdf();

                var fileNameBase = $"Ornek_Optik_Form_{i + 1}_{s.Name.Replace(" ", "_")}_{s.Booklet}_Kitapcik";
                var pdfPath1 = Path.Combine(targetSampleDir, $"{fileNameBase}.pdf");
                var pdfPath2 = Path.Combine(userDownloadsDir, $"{fileNameBase}.pdf");

                File.WriteAllBytes(pdfPath1, pdfBytes);
                File.WriteAllBytes(pdfPath2, pdfBytes);

                // 5) SkiaSharp ile JPG üret (1680 x 2376 px, 200 DPI)
                RenderJpgFromLayout(layout, filledBubbles, s, Path.Combine(targetSampleDir, $"{fileNameBase}.jpg"));
                File.Copy(Path.Combine(targetSampleDir, $"{fileNameBase}.jpg"), Path.Combine(userDownloadsDir, $"{fileNameBase}.jpg"), true);

                Console.WriteLine($"   -> PDF ve JPG kaydedildi: {fileNameBase}");
            }

            Console.WriteLine("\n5 adet doldurulmuş örnek form (PDF ve JPG) başarıyla oluşturuldu!");

            Console.WriteLine("\n[FastOmrEngine Test Doğrulaması]");
            var omrEngine = new FastOmrEngine();
            using var testStream = File.OpenRead(Path.Combine(targetSampleDir, "Ornek_Optik_Form_1_KEMAL_SUNAL_A_Kitapcik.jpg"));
            var omrResult = omrEngine.Process(testStream, layout);
            Console.WriteLine($"Kemal Sunal -> Okunan No: '{omrResult.StudentNumber}', Kitapçık: '{omrResult.Booklet}', Cevaplanan Soru: {omrResult.Answers.Count}, NeedsReview: {omrResult.NeedsReview}");
        }

        private static void RenderJpgFromLayout(FormLayout layout, List<BubbleInfo> filledBubbles, StudentProfile student, string outputPath)
        {
            // 8 px / mm = 203.2 DPI (~200 DPI yüksek kalite A4 tarama)
            float scale = 8.0f;
            int width = (int)Math.Round(layout.PageWidthMm * scale);
            int height = (int)Math.Round(layout.PageHeightMm * scale);

            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            using var blackPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            using var borderPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f, IsAntialias = true };
            using var thinLinePaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1.0f, IsAntialias = true };
            using var headerBgPaint = new SKPaint { Color = new SKColor(241, 245, 249), Style = SKPaintStyle.Fill };
            using var pencilFillPaint = new SKPaint { Color = new SKColor(26, 26, 26), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var bubbleStroke = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, IsAntialias = true };
            using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 14);

            // 1) 4 Köşe Homografi Referans Kareleri (Markörler)
            foreach (var m in layout.Markers)
            {
                canvas.DrawRect(
                    (float)m.Xmm * scale,
                    (float)m.Ymm * scale,
                    (float)m.SizeMm * scale,
                    (float)m.SizeMm * scale,
                    blackPaint);
            }

            // 2) Kutular ve Çerçeveler
            foreach (var f in layout.Fields)
            {
                canvas.DrawRect(
                    (float)f.Xmm * scale,
                    (float)f.Ymm * scale,
                    (float)f.WidthMm * scale,
                    (float)f.HeightMm * scale,
                    borderPaint);
            }

            // 3) Ders Sütunları
            foreach (var s in layout.Sections)
            {
                canvas.DrawRect(
                    (float)s.Xmm * scale,
                    (float)s.Ymm * scale,
                    (float)s.WidthMm * scale,
                    (float)s.HeightMm * scale,
                    borderPaint);

                canvas.DrawRect(
                    (float)(s.Xmm + 0.6) * scale,
                    (float)(s.Ymm + 0.6) * scale,
                    (float)(s.WidthMm - 1.2) * scale,
                    4.8f * scale,
                    headerBgPaint);

                using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
                using var secFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 13);
                canvas.DrawText(s.Name, (float)(s.Xmm + 2) * scale, (float)(s.Ymm + 4.2) * scale, secFont, textPaint);
            }

            // 4) Başlık Bilgisi
            using (var titleFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 26))
            using (var subFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal), 16))
            using (var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true, TextAlign = SKTextAlign.Center })
            {
                canvas.DrawText(layout.InstitutionName, width / 2f, 15f * scale, titleFont, textPaint);
                canvas.DrawText($"{layout.FormTitle} CEVAP FORMU", width / 2f, 21f * scale, subFont, textPaint);
            }

            // 5) Bütün Boş Baloncukları Çiz
            foreach (var b in layout.Bubbles)
            {
                float cx = (float)b.Xmm * scale;
                float cy = (float)b.Ymm * scale;
                float r = (float)b.RadiusMm * scale;
                canvas.DrawCircle(cx, cy, r, bubbleStroke);

                // Balon harfi/rakamı
                using var optFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), r * 1.15f);
                using var optPaint = new SKPaint { Color = new SKColor(70, 70, 70), IsAntialias = true, TextAlign = SKTextAlign.Center };
                canvas.DrawText(b.Option, cx, cy + (r * 0.42f), optFont, optPaint);
            }

            // 6) Öğrencinin İşaretlediği Baloncukları Koyu Kalemle Doldur
            var filledSet = new HashSet<(int, int, string)>(filledBubbles.Select(fb => (fb.SectionId, fb.QuestionNumber, fb.Option)));
            foreach (var b in layout.Bubbles)
            {
                if (filledSet.Contains((b.SectionId, b.QuestionNumber, b.Option)))
                {
                    float cx = (float)b.Xmm * scale;
                    float cy = (float)b.Ymm * scale;
                    float r = (float)b.RadiusMm * scale * 0.94f;
                    canvas.DrawCircle(cx, cy, r, pencilFillPaint);
                }
            }

            // 7) El Yazısı İsim ve İmza
            using (var hwFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 18))
            using (var hwPaint = new SKPaint { Color = new SKColor(15, 23, 42), IsAntialias = true })
            {
                canvas.DrawText(student.Name, 36f * scale, 36.5f * scale, hwFont, hwPaint);
                canvas.DrawText(student.ClassBranch, 36f * scale, 42f * scale, hwFont, hwPaint);
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 92);
            using var stream = File.OpenWrite(outputPath);
            data.SaveTo(stream);
        }

        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        private class StudentProfile
        {
            public string Name { get; set; } = string.Empty;
            public string StudentNo { get; set; } = string.Empty;
            public string TcNo { get; set; } = string.Empty;
            public string Booklet { get; set; } = "A";
            public string ClassBranch { get; set; } = "1-A";
            public int AnswerSeed { get; set; }
        }
    }
}
