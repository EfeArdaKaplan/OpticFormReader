using core.Interfaces;
using core.Models;
using SkiaSharp;
using System.Text;

namespace business.Services
{
    public class FastOmrEngine : IOmrEngine
    {
        // Özel SectionId tanımları (FormGeneratorService ile birebir uyumlu)
        private const int StudentNumberSectionId = -1;
        private const int TcNumberSectionId = -2;
        private const int BookletSectionId = -3;
        private const int NameSectionId = -4;

        // Baloncuğun dolu sayılması için eşik değerler
        private const float DarknessThreshold = 0.28f;
        private const float MarginDifference = 0.12f;

        public SheetOmrResult Process(Stream imageStream, FormLayout layout)
        {
            ArgumentNullException.ThrowIfNull(imageStream);
            ArgumentNullException.ThrowIfNull(layout);

            using var bitmap = SKBitmap.Decode(imageStream);
            if (bitmap == null)
            {
                return new SheetOmrResult
                {
                    NeedsReview = true,
                    ReviewReason = "Görsel formatı okunamadı veya dosya bozuk."
                };
            }

            var result = new SheetOmrResult();

            // 1) Sayfa ve Ölçek Hesaplama (mm -> Piksel dönüşümü)
            double scaleX = (double)bitmap.Width / layout.PageWidthMm;
            double scaleY = (double)bitmap.Height / layout.PageHeightMm;

            // 2) Bütün baloncukların koyuluklarını hesaplayıp gruplayalım
            // Key: (SectionId, QuestionNumber) -> Value: List of (Option, DarknessScore)
            var questionGroups = new Dictionary<(int SectionId, int QuestionNumber), List<(string Option, float Darkness)>>();

            foreach (var bubble in layout.Bubbles)
            {
                int px = (int)(bubble.Xmm * scaleX);
                int py = (int)(bubble.Ymm * scaleY);
                int radiusPx = Math.Max(2, (int)(bubble.RadiusMm * Math.Min(scaleX, scaleY)));

                float darkness = SampleBubbleDarkness(bitmap, px, py, radiusPx);

                var key = (bubble.SectionId, bubble.QuestionNumber);
                if (!questionGroups.TryGetValue(key, out var list))
                {
                    list = new List<(string Option, float Darkness)>();
                    questionGroups[key] = list;
                }
                list.Add((bubble.Option, darkness));
            }

            if (questionGroups.TryGetValue((BookletSectionId, 1), out var bookletOptions))
            {
                var detectedBooklet = ResolveBestOption(bookletOptions);
                if (!string.IsNullOrEmpty(detectedBooklet) && detectedBooklet != "DOUBLE")
                {
                    result.Booklet = detectedBooklet;
                }
                else
                {
                    result.NeedsReview = true;
                    result.ReviewReason = "Kitapçık türü okunamadı veya çift kodlandı.";
                }
            }
            else
            {
                result.NeedsReview = true;
                result.ReviewReason = "Kitapçık alanı tespit edilemedi veya kodlanmadı.";
            }

            // 4) Öğrenci Numarasını Çözümleme
            var studentNoBuilder = new StringBuilder();
            for (int col = 1; col <= layout.StudentNoDigits; col++)
            {
                if (questionGroups.TryGetValue((StudentNumberSectionId, col), out var digits))
                {
                    var opt = ResolveBestOption(digits);
                    studentNoBuilder.Append(!string.IsNullOrEmpty(opt) && opt != "DOUBLE" ? opt : "?");
                }
            }
            result.StudentNumber = studentNoBuilder.ToString();
            if (result.StudentNumber.Contains('?'))
            {
                result.NeedsReview = true;
                result.ReviewReason = (result.ReviewReason + " Öğrenci numarası eksik veya hatalı.").Trim();
            }

            // 5) T.C. Kimlik Numarasını Çözümleme (11 Hane)
            var tcBuilder = new StringBuilder();
            for (int col = 1; col <= 11; col++)
            {
                if (questionGroups.TryGetValue((TcNumberSectionId, col), out var digits))
                {
                    var opt = ResolveBestOption(digits);
                    tcBuilder.Append(!string.IsNullOrEmpty(opt) && opt != "DOUBLE" ? opt : " ");
                }
            }
            result.TcNo = tcBuilder.ToString().Trim();

            // 6) Ad - Soyad Harf Kodlamasını Çözümleme (25 Karakter)
            var nameBuilder = new StringBuilder();
            for (int col = 1; col <= 25; col++)
            {
                if (questionGroups.TryGetValue((NameSectionId, col), out var letters))
                {
                    var opt = ResolveBestOption(letters);
                    nameBuilder.Append(!string.IsNullOrEmpty(opt) && opt != "DOUBLE" ? opt : " ");
                }
            }
            string rawName = nameBuilder.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(rawName))
            {
                var parts = rawName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    result.LastName = parts[^1];
                    result.FirstName = string.Join(" ", parts.Take(parts.Length - 1));
                }
                else
                {
                    result.FirstName = rawName;
                }
            }

            // 7) Derslerin Soru Cevaplarını Çözümleme
            foreach (var section in layout.Sections)
            {
                for (int q = section.QuestionStart; q <= section.QuestionEnd; q++)
                {
                    if (questionGroups.TryGetValue((section.SectionId, q), out var choices))
                    {
                        var best = ResolveBestOptionWithConfidence(choices);
                        if (!string.IsNullOrEmpty(best.Option))
                        {
                            result.Answers[(section.SectionId, q)] = (best.Option, best.Confidence);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Baloncuğun içindeki pikselleri örnekleyerek koyuluk oranını (0.0 = Beyaz, 1.0 = Simsiyah) hesaplar.
        /// </summary>
        private static float SampleBubbleDarkness(SKBitmap bitmap, int cx, int cy, int radius)
        {
            if (cx < radius || cy < radius || cx >= bitmap.Width - radius || cy >= bitmap.Height - radius)
                return 0f;

            float totalLuminance = 0f;
            int count = 0;

            // Daire içinde örnekleme pikselleri al (iç çekirdek %85 çap: dış çizgi gürültüsünü filtreler)
            int sampleRadius = Math.Max(2, (int)(radius * 0.85f));
            for (int y = -sampleRadius; y <= sampleRadius; y += 2)
            {
                for (int x = -sampleRadius; x <= sampleRadius; x += 2)
                {
                    if (x * x + y * y <= sampleRadius * sampleRadius)
                    {
                        var pixel = bitmap.GetPixel(cx + x, cy + y);
                        // Gri tonlama parlaklığı (0 = siyah, 255 = beyaz)
                        float lum = 0.299f * pixel.Red + 0.587f * pixel.Green + 0.114f * pixel.Blue;
                        totalLuminance += lum;
                        count++;
                    }
                }
            }

            if (count == 0) return 0f;
            float avgLuminance = totalLuminance / (count * 255f);

            // 1.0 - parlaklık = koyuluk derecesi (Koyu kalemle çizildiyse 1'e yakın olur)
            return Math.Clamp(1.0f - avgLuminance, 0f, 1f);
        }

        private static string? ResolveBestOption(List<(string Option, float Darkness)> choices)
        {
            var res = ResolveBestOptionWithConfidence(choices);
            return res.Option;
        }

        private static (string? Option, double Confidence) ResolveBestOptionWithConfidence(
            List<(string Option, float Darkness)> choices)
        {
            if (choices == null || choices.Count == 0) return (null, 0);

            // 1) Adaptif Eşik ve Arka Plan Taban Kalibrasyonu (Adaptive Baseline & Relative Contrast)
            float minDarkness = choices.Min(c => c.Darkness);
            float maxDarkness = choices.Max(c => c.Darkness);
            float contrast = maxDarkness - minDarkness;

            // Kağıt taban koyuluğuna göre dinamik eşik:
            // En az 0.22f mutlak koyuluk ve yerel tabana kıyasla en az 0.14f bağıl kontrast aranır
            float dynamicThreshold = Math.Max(0.22f, minDarkness + 0.14f);

            var sorted = choices.OrderByDescending(c => c.Darkness).ToList();
            var top = sorted[0];

            // En koyu baloncuk bile dinamik eşiğin veya bağıl kontrastın altındaysa -> Kesinlikle BOŞ
            if (top.Darkness < dynamicThreshold || contrast < 0.12f)
            {
                return (null, 0);
            }

            // 2) Çift İşaretleme Kontrolü (Double Marking Detection):
            // Eğer ikinci en koyu baloncuk da dinamik eşiği geçiyorsa ve birinciye çok yakınsa -> GEÇERSİZ / YANLIŞ
            if (sorted.Count > 1)
            {
                var second = sorted[1];
                if (second.Darkness >= dynamicThreshold && (top.Darkness - second.Darkness) < MarginDifference)
                {
                    return ("DOUBLE", 0); // Kural gereği çift işaretleme geçersiz sayılır (yanlış kabul edilir, boş değil!)
                }
            }

            // 3) Tekil Geçerli İşaretleme: Güven skoru hesaplanır
            double confidence = Math.Clamp((top.Darkness - minDarkness) / Math.Max(0.01f, 1.0f - minDarkness), 0.5, 1.0);
            return (top.Option, confidence);
        }
    }
}