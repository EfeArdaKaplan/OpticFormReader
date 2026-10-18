using business.Services;
using core.Entities;
using core.Models;
using SkiaSharp;
using System.Text;
using Xunit;

namespace OpticEngine.Tests
{
    public class OmrEngineTests
    {
        [Fact]
        public void AdaptiveThresholding_UnmarkedQuestion_ReturnsEmpty()
        {
            // Arrange
            var engine = new FastOmrEngine();
            var layout = CreateSimpleTestLayout();

            // Sadece beyaz arka plan ve boş baloncuklar içeren görsel üret
            using var bitmap = new SKBitmap(800, 1100);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                using var stroke = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f };
                foreach (var b in layout.Bubbles)
                {
                    canvas.DrawCircle((float)b.Xmm * 4f, (float)b.Ymm * 4f, (float)b.RadiusMm * 4f, stroke);
                }
            }

            using var stream = new MemoryStream();
            bitmap.Encode(SKEncodedImageFormat.Jpeg, 90).SaveTo(stream);
            stream.Position = 0;

            // Act
            var result = engine.Process(stream, layout);

            // Assert: Hiçbir soru dolu sayılmamalıdır (Boş olarak çözümlenmelidir)
            Assert.Empty(result.Answers);
        }

        [Fact]
        public void AdaptiveThresholding_SingleMarkedOption_ResolvesCorrectOption()
        {
            // Arrange
            var engine = new FastOmrEngine();
            var layout = CreateSimpleTestLayout();

            // Soru 1'de 'C' şıkkını dolduralım
            using var bitmap = new SKBitmap(800, 1100);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                using var stroke = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f };
                using var fill = new SKPaint { Color = new SKColor(25, 25, 25), Style = SKPaintStyle.Fill };

                foreach (var b in layout.Bubbles)
                {
                    float cx = (float)b.Xmm * 4f;
                    float cy = (float)b.Ymm * 4f;
                    float r = (float)b.RadiusMm * 4f;

                    canvas.DrawCircle(cx, cy, r, stroke);

                    // Soru 1, C şıkkı dolu
                    if (b.SectionId == 1 && b.QuestionNumber == 1 && b.Option == "C")
                    {
                        canvas.DrawCircle(cx, cy, r * 0.9f, fill);
                    }
                }
            }

            using var stream = new MemoryStream();
            bitmap.Encode(SKEncodedImageFormat.Jpeg, 90).SaveTo(stream);
            stream.Position = 0;

            // Act
            var result = engine.Process(stream, layout);

            // Assert
            Assert.True(result.Answers.ContainsKey((1, 1)));
            Assert.Equal("C", result.Answers[(1, 1)].Option);
            Assert.True(result.Answers[(1, 1)].Confidence > 0.6);
        }

        [Fact]
        public void DoubleMarking_TwoBubblesFilled_FlagsAsDoubleAndCountsAsWrong()
        {
            // Arrange
            var engine = new FastOmrEngine();
            var layout = CreateSimpleTestLayout();

            // Soru 2'de hem 'A' hem de 'B' şıkkını dolduralım (Çift İşaretleme)
            using var bitmap = new SKBitmap(800, 1100);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                using var stroke = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f };
                using var fill = new SKPaint { Color = new SKColor(20, 20, 20), Style = SKPaintStyle.Fill };

                foreach (var b in layout.Bubbles)
                {
                    float cx = (float)b.Xmm * 4f;
                    float cy = (float)b.Ymm * 4f;
                    float r = (float)b.RadiusMm * 4f;

                    canvas.DrawCircle(cx, cy, r, stroke);

                    if (b.SectionId == 1 && b.QuestionNumber == 2 && (b.Option == "A" || b.Option == "B"))
                    {
                        canvas.DrawCircle(cx, cy, r * 0.9f, fill);
                    }
                }
            }

            using var stream = new MemoryStream();
            bitmap.Encode(SKEncodedImageFormat.Jpeg, 90).SaveTo(stream);
            stream.Position = 0;

            // Act
            var result = engine.Process(stream, layout);

            // Assert: Motor soruyu "DOUBLE" olarak döndürmelidir
            Assert.True(result.Answers.ContainsKey((1, 2)));
            Assert.Equal("DOUBLE", result.Answers[(1, 2)].Option);

            // Simüle Cevap Anahtarı Karşılaştırması (String Comparison Testi)
            string correctOpt = "A";
            string selected = result.Answers[(1, 2)].Option ?? string.Empty;

            int correct = 0;
            int wrong = 0;
            int empty = 0;

            if (string.IsNullOrEmpty(selected))
            {
                empty++;
            }
            else if (!string.IsNullOrEmpty(correctOpt) && string.Equals(selected.Trim(), correctOpt.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                correct++;
            }
            else
            {
                wrong++;
            }

            // Çift işaretleme KESİNLİKLE boş (empty) sayılmamalı, yanlış (wrong) sayılmalıdır!
            Assert.Equal(0, empty);
            Assert.Equal(1, wrong);
            Assert.Equal(0, correct);
        }

        [Fact]
        public void AnswerKeyComparison_ExactStringLogic_ZeroErrorTolerance()
        {
            // Soru 1: Doğru (A == A)
            // Soru 2: Yanlış (B != A)
            // Soru 3: Boş ("" -> empty)
            // Soru 4: Çift İşaretleme ("DOUBLE" -> wrong)
            var studentAnswers = new Dictionary<int, string>
            {
                { 1, "A" },
                { 2, "B" },
                { 3, "" },
                { 4, "DOUBLE" }
            };

            var answerKey = new Dictionary<int, string>
            {
                { 1, "A" },
                { 2, "A" },
                { 3, "C" },
                { 4, "D" }
            };

            int correct = 0;
            int wrong = 0;
            int empty = 0;

            for (int q = 1; q <= 4; q++)
            {
                studentAnswers.TryGetValue(q, out var selected);
                selected ??= string.Empty;

                answerKey.TryGetValue(q, out var correctOpt);
                correctOpt ??= string.Empty;

                if (string.IsNullOrEmpty(selected))
                {
                    empty++;
                }
                else if (!string.IsNullOrEmpty(correctOpt) && string.Equals(selected.Trim(), correctOpt.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    correct++;
                }
                else
                {
                    wrong++;
                }
            }

            // Ham Net = Doğru sayısı (4 yanlış 1 doğruyu götürmez)
            double net = correct;

            Assert.Equal(1, correct);
            Assert.Equal(2, wrong); // Soru 2 ve Soru 4 (DOUBLE) yanlış olmalı
            Assert.Equal(1, empty); // Sadece Soru 3 boş olmalı
            Assert.Equal(1.0, net);
        }

        [Fact]
        public void GroundTruth_FiveSampleSheets_ReadAndVerifyAccurately()
        {
            var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\.."));
            var sampleDir = Path.Combine(projectDir, "sample_sheets");

            if (!Directory.Exists(sampleDir))
            {
                // Alternatif doğrudan yol
                sampleDir = @"C:\Users\efear\OneDrive\Belgeler\EfeArda\YAZILIM\C#\optic\sample_sheets";
            }

            Assert.True(Directory.Exists(sampleDir), $"Örnek kağıt klasörü bulunamadı: {sampleDir}");

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
            var omrEngine = new FastOmrEngine();

            var sampleFiles = new[]
            {
                new { FileName = "Ornek_Optik_Form_1_KEMAL_SUNAL_A_Kitapcik.jpg", ExpectedNo = "20261001", ExpectedBooklet = "A" },
                new { FileName = "Ornek_Optik_Form_2_SENER_SEN_A_Kitapcik.jpg", ExpectedNo = "20261002", ExpectedBooklet = "A" },
                new { FileName = "Ornek_Optik_Form_3_ILYAS_SALMAN_B_Kitapcik.jpg", ExpectedNo = "20261003", ExpectedBooklet = "B" },
                new { FileName = "Ornek_Optik_Form_4_HALIT_AKCATEPE_B_Kitapcik.jpg", ExpectedNo = "20261004", ExpectedBooklet = "B" },
                new { FileName = "Ornek_Optik_Form_5_MUNIR_OZKUL_A_Kitapcik.jpg", ExpectedNo = "20261005", ExpectedBooklet = "A" },
            };

            foreach (var sample in sampleFiles)
            {
                var filePath = Path.Combine(sampleDir, sample.FileName);
                Assert.True(File.Exists(filePath), $"Örnek optik form bulunamadı: {filePath}");

                using var stream = File.OpenRead(filePath);
                var result = omrEngine.Process(stream, layout);

                Assert.Equal(sample.ExpectedNo, result.StudentNumber);
                Assert.Equal(sample.ExpectedBooklet, result.Booklet);
                Assert.True(result.Answers.Count >= 20, $"Öğrenci {sample.FileName} için en az 20 soru okunmalıydı, okunan: {result.Answers.Count}");
                Assert.False(result.NeedsReview, $"Öğrencinin kağıdı NeedsReview=true olmamalıdır ({result.ReviewReason})");
            }
        }

        private static FormLayout CreateSimpleTestLayout()
        {
            var layout = new FormLayout
            {
                PageWidthMm = 200,
                PageHeightMm = 275,
                StudentNoDigits = 8
            };

            // 1 Ders, 3 Soru, 5 Şık
            layout.Sections.Add(new SectionLayout
            {
                SectionId = 1,
                Name = "Test Dersi",
                QuestionStart = 1,
                QuestionEnd = 3,
                TotalQuestionCount = 3
            });

            var options = new[] { "A", "B", "C", "D", "E" };
            for (int q = 1; q <= 3; q++)
            {
                for (int o = 0; o < 5; o++)
                {
                    layout.Bubbles.Add(new BubbleInfo
                    {
                        SectionId = 1,
                        QuestionNumber = q,
                        Option = options[o],
                        Xmm = 30 + o * 10,
                        Ymm = 40 + q * 12,
                        RadiusMm = 2.4
                    });
                }
            }

            return layout;
        }
    }
}
