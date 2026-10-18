using core.DTOs;
using core.Entities;
using core.interfaces;
using core.Interfaces;
using core.Models;
using data.Context;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace business.Services
{
    public class EvaluationService : IEvaluationService
    {
        // Sınavların anlık ilerleme durumlarını hafızada tutan thread-safe havuz
        private static readonly ConcurrentDictionary<int, EvaluationProgressDto> ProgressTracker = new();

        private readonly AppDbContext _context;
        private readonly IExamRepository _examRepository;
        private readonly IExamTemplateRepository _templateRepository;
        private readonly IAnswerKeyRepository _answerKeyRepository;
        private readonly IStudentSheetRepository _sheetRepository;
        private readonly IFileStorage _storage;
        private readonly IOmrEngine _omrEngine;
        private readonly FormGeneratorService _formGeneratorService;
        private readonly IEvaluationNotificationService _notificationService;

        public EvaluationService(
            AppDbContext context,
            IExamRepository examRepository,
            IExamTemplateRepository templateRepository,
            IAnswerKeyRepository answerKeyRepository,
            IStudentSheetRepository sheetRepository,
            IFileStorage storage,
            IOmrEngine omrEngine,
            FormGeneratorService formGeneratorService,
            IEvaluationNotificationService notificationService)
        {
            _context = context;
            _examRepository = examRepository;
            _templateRepository = templateRepository;
            _answerKeyRepository = answerKeyRepository;
            _sheetRepository = sheetRepository;
            _storage = storage;
            _omrEngine = omrEngine;
            _formGeneratorService = formGeneratorService;
            _notificationService = notificationService;
        }

        public EvaluationProgressDto GetProgress(int examId)
        {
            if (ProgressTracker.TryGetValue(examId, out var progress))
            {
                return progress;
            }

            return new EvaluationProgressDto
            {
                ExamId = examId,
                CurrentStatus = "Beklemede",
                Percent = 0
            };
        }

        public async Task<EvaluationSummaryDto> EvaluateExamAsync(
            int examId,
            int ownerUserId,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            // 1) Sınav, Şablon ve Cevap Anahtarlarını Getir
            var exam = await _examRepository.GetByIdAsync(examId);
            if (exam == null || exam.OwnerUserId != ownerUserId)
            {
                throw new ArgumentException("Sınav bulunamadı veya bu sınava erişim yetkiniz yok.");
            }

            var template = await _templateRepository.GetByIdAsync(exam.ExamTemplateId);
            if (template == null)
            {
                throw new ArgumentException("Sınavın şablonu bulunamadı.");
            }

            var answerKeys = await _answerKeyRepository.GetByExamIdAsync(examId);
            if (answerKeys.Count == 0)
            {
                throw new InvalidOperationException("Bu sınav için henüz cevap anahtarı kaydedilmemiş.");
            }

            // Şablonun yerleşim koordinat haritasını hazırla
            FormLayout layout;
            if (!string.IsNullOrWhiteSpace(template.LayoutJson))
            {
                layout = JsonSerializer.Deserialize<FormLayout>(template.LayoutJson)!;
            }
            else
            {
                layout = _formGeneratorService.BuildLayout(template);
            }

            // Cevap Anahtarlarını Hızlı Arama Haritasına Al
            // Key: (Booklet, ExamSectionId) -> Value: Dictionary<QuestionNumber, CorrectOption>
            var keyMap = new Dictionary<(string Booklet, int SectionId), Dictionary<int, string>>();
            foreach (var ak in answerKeys)
            {
                var answers = JsonSerializer.Deserialize<Dictionary<int, string>>(ak.AnswersJson) ?? new();
                keyMap[(ak.Booklet.ToUpperInvariant(), ak.ExamSectionId)] = answers;
            }

            // 2) Okunacak Kağıtları Getir
            var sheets = await _sheetRepository.GetByExamIdAsync(examId);
            if (sheets.Count == 0)
            {
                throw new InvalidOperationException("Değerlendirilecek optik form kağıdı bulunamadı.");
            }

            int totalSheets = sheets.Count;
            int processedCount = 0;

            // İlerleme durumunu başlat
            var progress = new EvaluationProgressDto
            {
                ExamId = examId,
                TotalSheets = totalSheets,
                ProcessedSheets = 0,
                Percent = 0,
                CurrentStatus = "Optik formlar paralel okunuyor...",
                IsCompleted = false
            };
            ProgressTracker[examId] = progress;
            await BroadcastProgressAsync(progress);

            // Puan Katsayısı Hesaplama
            int totalQuestions = template.Sections.Sum(s => s.QuestionCount);
            double effectiveTotal = exam.TotalScore * (exam.PercentRatio / 100.0);
            double pointPerQ = exam.PointPerQuestion > 0
                ? exam.PointPerQuestion
                : (totalQuestions > 0 ? (effectiveTotal / totalQuestions) : 0);

            // Paralel sonuçları toplayacağımız thread-safe havuzlar
            var allAnswers = new ConcurrentBag<StudentAnswer>();
            var allResults = new ConcurrentBag<ExamResult>();
            var updatedSheets = new ConcurrentBag<StudentSheet>();

            // 3) PARALEL İŞLEME MOTORU (Tüm CPU Çekirdekleri Devrede!)
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(sheets, parallelOptions, async (sheet, ct) =>
            {
                try
                {
                    // SeaweedFS'ten görseli oku
                    await using var stream = await _storage.OpenReadAsync(sheet.ImageKey, ct);

                    // OMR Motoru ile baloncukları milisaniyeler içinde çöz
                    var omr = _omrEngine.Process(stream, layout);

                    // Kağıt bilgilerini güncelle
                    sheet.StudentNumber = omr.StudentNumber;
                    sheet.FirstName = omr.FirstName;
                    sheet.LastName = omr.LastName;
                    sheet.TcNo = omr.TcNo;
                    sheet.Booklet = omr.Booklet ?? "A"; // Okunamazsa varsayılan A veya NeedsReview
                    sheet.NeedsReview = omr.NeedsReview;
                    sheet.ReviewReason = omr.ReviewReason;
                    sheet.ProcessingStatus = omr.NeedsReview ? "NeedsReview" : "Done";

                    string activeBooklet = (sheet.Booklet ?? "A").ToUpperInvariant();

                    // Kitapçığın cevap anahtarında tanımlı olup olmadığını kontrol et (en az 1 dolu geçerli cevap olmalı)
                    bool hasBookletKey = keyMap.Keys.Any(k => k.Booklet == activeBooklet && keyMap[k].Values.Any(v => !string.IsNullOrWhiteSpace(v) && v.Trim() != "-"));

                    if (!hasBookletKey)
                    {
                        // B (veya tanımlanmamış kitapçık) optik kağıtta seçiliyse ve cevap anahtarında yoksa:
                        // "b yi bos gec ve gecersiz de gonderilen optiklere"
                        sheet.ProcessingStatus = "Invalid";
                        sheet.NeedsReview = true;
                        sheet.ReviewReason = $"Geçersiz: {activeBooklet} Kitapçığı cevap anahtarı bulunamadı (Boş geçildi).";

                        foreach (var section in template.Sections)
                        {
                            for (int q = 1; q <= section.QuestionCount; q++)
                            {
                                omr.Answers.TryGetValue((section.Id, q), out var studentChoice);
                                string selected = studentChoice.Option ?? string.Empty;

                                allAnswers.Add(new StudentAnswer
                                {
                                    StudentSheetId = sheet.Id,
                                    ExamSectionId = section.Id,
                                    QuestionNumber = q,
                                    SelectedOption = selected,
                                    ConfidenceScore = studentChoice.Confidence
                                });
                            }

                            allResults.Add(new ExamResult
                            {
                                StudentSheetId = sheet.Id,
                                ExamSectionId = section.Id,
                                CorrectCount = 0,
                                WrongCount = 0,
                                EmptyCount = section.QuestionCount,
                                Score = 0.0,
                                CalculatedAt = DateTime.UtcNow
                            });
                        }

                        updatedSheets.Add(sheet);
                        return;
                    }

                    // Ders bazında cevapları karşılaştır ve puanla
                    foreach (var section in template.Sections)
                    {
                        keyMap.TryGetValue((activeBooklet, section.Id), out var correctAnswers);
                        correctAnswers ??= new Dictionary<int, string>();

                        int correct = 0;
                        int wrong = 0;
                        int empty = 0;

                        for (int q = 1; q <= section.QuestionCount; q++)
                        {
                            omr.Answers.TryGetValue((section.Id, q), out var studentChoice);
                            string selected = studentChoice.Option ?? string.Empty;

                            correctAnswers.TryGetValue(q, out var correctOpt);
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

                            allAnswers.Add(new StudentAnswer
                            {
                                StudentSheetId = sheet.Id,
                                ExamSectionId = section.Id,
                                QuestionNumber = q,
                                SelectedOption = selected,
                                ConfidenceScore = studentChoice.Confidence
                            });
                        }

                        // Net = Doğru sayısı (Ham puan esası)
                        double sectionScore = correct * pointPerQ;

                        allResults.Add(new ExamResult
                        {
                            StudentSheetId = sheet.Id,
                            ExamSectionId = section.Id,
                            CorrectCount = correct,
                            WrongCount = wrong,
                            EmptyCount = empty,
                            Score = Math.Round(sectionScore, 2),
                            CalculatedAt = DateTime.UtcNow
                        });
                    }

                    updatedSheets.Add(sheet);
                }
                catch (Exception ex)
                {
                    sheet.ProcessingStatus = "Error";
                    sheet.NeedsReview = true;
                    sheet.ReviewReason = $"Hata: {ex.Message}";
                    updatedSheets.Add(sheet);
                }

                // İlerlemeyi atomik artır ve SignalR ile yayınla
                int current = Interlocked.Increment(ref processedCount);
                int pct = (int)((double)current / totalSheets * 100);

                progress.ProcessedSheets = current;
                progress.Percent = pct;
                progress.CurrentStatus = $"Okunan: {current} / {totalSheets} kağıt (%{pct})";

                await BroadcastProgressAsync(progress);
            });

            // 4) TOPLU VERİTABANI YAZIMI (Tek Bir Hızlı Transaction)
            progress.CurrentStatus = "Sonuçlar veritabanına kaydediliyor...";
            await BroadcastProgressAsync(progress);

            // Varsa eski cevap ve sonuçları temizle
            var sheetIds = sheets.Select(s => s.Id).ToList();
            var oldAnswers = await _context.StudentAnswers
                .Where(a => sheetIds.Contains(a.StudentSheetId))
                .ToListAsync(cancellationToken);
            if (oldAnswers.Count > 0) _context.StudentAnswers.RemoveRange(oldAnswers);

            var oldResults = await _context.ExamResults
                .Where(r => sheetIds.Contains(r.StudentSheetId))
                .ToListAsync(cancellationToken);
            if (oldResults.Count > 0) _context.ExamResults.RemoveRange(oldResults);

            // Yeni kayıtları tek seferde ekle
            await _context.StudentAnswers.AddRangeAsync(allAnswers, cancellationToken);
            await _context.ExamResults.AddRangeAsync(allResults, cancellationToken);
            _context.StudentSheets.UpdateRange(updatedSheets);

            await _context.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();

            // 5) Tamamlandı Bilgisi ve İstatistik Özeti
            progress.IsCompleted = true;
            progress.Percent = 100;
            progress.CurrentStatus = $"Tamamlandı! {totalSheets} kağıt {stopwatch.ElapsedMilliseconds} ms sürede okundu.";
            await BroadcastProgressAsync(progress);

            // Sınıf İstatistikleri
            var studentScores = allResults
                .GroupBy(r => r.StudentSheetId)
                .Select(g => g.Sum(r => r.Score))
                .ToList();

            return new EvaluationSummaryDto
            {
                ExamId = examId,
                TotalSheets = totalSheets,
                EvaluatedSheets = updatedSheets.Count(s => s.ProcessingStatus == "Done"),
                NeedsReviewCount = updatedSheets.Count(s => s.NeedsReview),
                ClassAverageScore = studentScores.Count > 0 ? Math.Round(studentScores.Average(), 2) : 0,
                HighestScore = studentScores.Count > 0 ? Math.Round(studentScores.Max(), 2) : 0,
                LowestScore = studentScores.Count > 0 ? Math.Round(studentScores.Min(), 2) : 0,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }

        private async Task BroadcastProgressAsync(EvaluationProgressDto progress)
        {
            try
            {
                await _notificationService.BroadcastProgressAsync(progress);
            }
            catch
            {
                // SignalR bağlantısı olmasa bile değerlendirmeyi aksatma
            }
        }
    }
}