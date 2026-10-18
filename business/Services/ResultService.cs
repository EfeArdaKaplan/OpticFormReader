using core.DTOs;
using core.Entities;
using core.interfaces;
using core.Interfaces;
using data.Context;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace business.Services
{
    public class ResultService : IResultService
    {
        private readonly AppDbContext _context;
        private readonly IExamRepository _examRepository;
        private readonly IExamTemplateRepository _templateRepository;
        private readonly IAnswerKeyRepository _answerKeyRepository;
        private readonly IStudentSheetRepository _sheetRepository;

        public ResultService(
            AppDbContext context,
            IExamRepository examRepository,
            IExamTemplateRepository templateRepository,
            IAnswerKeyRepository answerKeyRepository,
            IStudentSheetRepository sheetRepository)
        {
            _context = context;
            _examRepository = examRepository;
            _templateRepository = templateRepository;
            _answerKeyRepository = answerKeyRepository;
            _sheetRepository = sheetRepository;
        }

        public async Task<ExamResultSummaryDto> GetExamResultsAsync(int examId, int ownerUserId)
        {
            var exam = await GetOwnedExamAsync(examId, ownerUserId);
            var template = await _templateRepository.GetByIdAsync(exam.ExamTemplateId);
            if (template == null)
            {
                throw new ArgumentException("Sınav şablonu bulunamadı.");
            }

            var sheets = await _sheetRepository.GetByExamIdAsync(examId);
            var sheetIds = sheets.Select(s => s.Id).ToList();

            var allResults = await _context.ExamResults
                .Where(r => sheetIds.Contains(r.StudentSheetId))
                .ToListAsync();

            var resultsBySheet = allResults
                .GroupBy(r => r.StudentSheetId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var sectionMap = template.Sections.ToDictionary(s => s.Id, s => s.Name);

            var studentsList = new List<StudentResultItemDto>();

            foreach (var sheet in sheets)
            {
                resultsBySheet.TryGetValue(sheet.Id, out var sheetResults);
                sheetResults ??= new List<ExamResult>();

                int correct = sheetResults.Sum(r => r.CorrectCount);
                int wrong = sheetResults.Sum(r => r.WrongCount);
                int empty = sheetResults.Sum(r => r.EmptyCount);
                double totalScore = Math.Round(sheetResults.Sum(r => r.Score), 2);
                double net = correct; // Ham puan esası

                var sectionSummaries = sheetResults.Select(r => new SectionResultSummaryDto
                {
                    SectionId = r.ExamSectionId,
                    SectionName = sectionMap.TryGetValue(r.ExamSectionId, out var name) ? name : $"Ders {r.ExamSectionId}",
                    CorrectCount = r.CorrectCount,
                    WrongCount = r.WrongCount,
                    EmptyCount = r.EmptyCount,
                    Score = r.Score
                }).ToList();

                string fullName = $"{sheet.FirstName} {sheet.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = "İsimsiz Öğrenci";

                studentsList.Add(new StudentResultItemDto
                {
                    StudentSheetId = sheet.Id,
                    StudentNumber = sheet.StudentNumber,
                    FullName = fullName,
                    TcNo = sheet.TcNo,
                    Booklet = sheet.Booklet ?? "A",
                    CorrectCount = correct,
                    WrongCount = wrong,
                    EmptyCount = empty,
                    NetScore = net,
                    TotalScore = totalScore,
                    NeedsReview = sheet.NeedsReview,
                    ReviewReason = sheet.ReviewReason,
                    SectionResults = sectionSummaries
                });
            }

            // Puan ve nete göre sırala, dereceleri (Rank) ata
            studentsList = studentsList
                .OrderByDescending(s => s.TotalScore)
                .ThenByDescending(s => s.NetScore)
                .ToList();

            for (int i = 0; i < studentsList.Count; i++)
            {
                studentsList[i].Rank = i + 1;
            }

            return new ExamResultSummaryDto
            {
                ExamId = examId,
                ExamTitle = exam.Title,
                TotalStudents = studentsList.Count,
                TotalQuestions = template.Sections.Sum(s => s.QuestionCount),
                ClassAverage = studentsList.Count > 0 ? Math.Round(studentsList.Average(s => s.TotalScore), 2) : 0,
                HighestScore = studentsList.Count > 0 ? studentsList.Max(s => s.TotalScore) : 0,
                LowestScore = studentsList.Count > 0 ? studentsList.Min(s => s.TotalScore) : 0,
                Students = studentsList
            };
        }

        public async Task<StudentDetailedReportDto> GetStudentReportAsync(int examId, int sheetId, int ownerUserId)
        {
            var exam = await GetOwnedExamAsync(examId, ownerUserId);
            var sheet = await _sheetRepository.GetByIdAsync(sheetId);
            if (sheet == null || sheet.ExamId != examId)
            {
                throw new ArgumentException("Öğrenci kağıdı bulunamadı.");
            }

            var template = await _templateRepository.GetByIdAsync(exam.ExamTemplateId);
            if (template == null) throw new ArgumentException("Şablon bulunamadı.");

            // Sınıf genel sıralaması ve ortalaması için tüm sonuçları al
            var classSummary = await GetExamResultsAsync(examId, ownerUserId);
            var studentItem = classSummary.Students.FirstOrDefault(s => s.StudentSheetId == sheetId);

            // Cevap Anahtarını Al
            string booklet = (sheet.Booklet ?? "A").ToUpperInvariant();
            var answerKeys = await _answerKeyRepository.GetByExamIdAsync(examId);

            var keyMap = new Dictionary<int, Dictionary<int, string>>();
            foreach (var ak in answerKeys.Where(k => k.Booklet.Equals(booklet, StringComparison.OrdinalIgnoreCase)))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<int, string>>(ak.AnswersJson) ?? new();
                keyMap[ak.ExamSectionId] = dict;
            }

            // Öğrencinin İşaretlediği Cevapları Al
            var studentAnswers = await _context.StudentAnswers
                .Where(a => a.StudentSheetId == sheetId)
                .ToListAsync();

            var answersMap = studentAnswers.ToDictionary(
                a => (a.ExamSectionId, a.QuestionNumber),
                a => (a.SelectedOption, a.ConfidenceScore));

            // Öğrencinin Ders Sonuçlarını Al
            var examResults = await _context.ExamResults
                .Where(r => r.StudentSheetId == sheetId)
                .ToDictionaryAsync(r => r.ExamSectionId, r => r);

            var sectionsReport = new List<SectionDetailedReportDto>();

            foreach (var section in template.Sections.OrderBy(s => s.Order))
            {
                keyMap.TryGetValue(section.Id, out var sectionKeys);
                sectionKeys ??= new Dictionary<int, string>();

                examResults.TryGetValue(section.Id, out var secResult);

                var questionsList = new List<QuestionDetailDto>();

                for (int q = 1; q <= section.QuestionCount; q++)
                {
                    sectionKeys.TryGetValue(q, out var correctOpt);
                    correctOpt ??= string.Empty;

                    answersMap.TryGetValue((section.Id, q), out var studentAns);
                    string selected = studentAns.SelectedOption ?? string.Empty;

                    string status = "Empty";
                    if (!string.IsNullOrEmpty(selected))
                    {
                        status = string.Equals(selected, correctOpt, StringComparison.OrdinalIgnoreCase)
                            ? "Correct"
                            : "Wrong";
                    }

                    questionsList.Add(new QuestionDetailDto
                    {
                        QuestionNumber = q,
                        CorrectOption = correctOpt,
                        SelectedOption = selected,
                        Status = status,
                        Confidence = studentAns.ConfidenceScore
                    });
                }

                sectionsReport.Add(new SectionDetailedReportDto
                {
                    SectionId = section.Id,
                    SectionName = section.Name,
                    CorrectCount = secResult?.CorrectCount ?? 0,
                    WrongCount = secResult?.WrongCount ?? 0,
                    EmptyCount = secResult?.EmptyCount ?? 0,
                    Score = secResult?.Score ?? 0,
                    Questions = questionsList
                });
            }

            string fullName = $"{sheet.FirstName} {sheet.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName)) fullName = "İsimsiz Öğrenci";

            return new StudentDetailedReportDto
            {
                StudentSheetId = sheet.Id,
                ExamId = examId,
                ExamTitle = exam.Title,
                StudentNumber = sheet.StudentNumber,
                FullName = fullName,
                TcNo = sheet.TcNo,
                Booklet = booklet,
                Rank = studentItem?.Rank ?? 0,
                TotalStudents = classSummary.TotalStudents,
                ClassAverage = classSummary.ClassAverage,
                TotalScore = studentItem?.TotalScore ?? 0,
                NetScore = studentItem?.NetScore ?? 0,
                TotalCorrect = studentItem?.CorrectCount ?? 0,
                TotalWrong = studentItem?.WrongCount ?? 0,
                TotalEmpty = studentItem?.EmptyCount ?? 0,
                Sections = sectionsReport
            };
        }

        public async Task<StudentResultItemDto> UpdateSheetManualAsync(
            int examId,
            int sheetId,
            int ownerUserId,
            ManualCorrectionDto request)
        {
            var exam = await GetOwnedExamAsync(examId, ownerUserId);
            var sheet = await _sheetRepository.GetByIdAsync(sheetId);
            if (sheet == null || sheet.ExamId != examId)
            {
                throw new ArgumentException("Kağıt bulunamadı.");
            }

            bool bookletChanged = false;

            if (!string.IsNullOrWhiteSpace(request.StudentNumber))
                sheet.StudentNumber = request.StudentNumber.Trim();

            if (!string.IsNullOrWhiteSpace(request.FirstName))
                sheet.FirstName = request.FirstName.Trim();

            if (!string.IsNullOrWhiteSpace(request.LastName))
                sheet.LastName = request.LastName.Trim();

            if (!string.IsNullOrWhiteSpace(request.TcNo))
                sheet.TcNo = request.TcNo.Trim();

            if (!string.IsNullOrWhiteSpace(request.Booklet))
            {
                var newBooklet = request.Booklet.Trim().ToUpperInvariant();
                if (new[] { "A", "B", "C", "D" }.Contains(newBooklet))
                {
                    if (sheet.Booklet != newBooklet)
                    {
                        sheet.Booklet = newBooklet;
                        bookletChanged = true;
                    }
                }
            }

            // Öğretmen düzelttiği için şüpheli bayrağını kaldır
            sheet.NeedsReview = false;
            sheet.ReviewReason = null;

            // Eğer kitapçık değiştiyse, öğrencinin puanlarını yeni kitapçığa göre anında yeniden hesapla
            if (bookletChanged)
            {
                await RescoreSingleSheetAsync(exam, sheet);
            }

            await _context.SaveChangesAsync();

            var summary = await GetExamResultsAsync(examId, ownerUserId);
            return summary.Students.First(s => s.StudentSheetId == sheetId);
        }

        private async Task RescoreSingleSheetAsync(Exam exam, StudentSheet sheet)
        {
            var template = await _templateRepository.GetByIdAsync(exam.ExamTemplateId);
            if (template == null) return;

            string booklet = (sheet.Booklet ?? "A").ToUpperInvariant();
            var answerKeys = await _answerKeyRepository.GetByExamIdAsync(exam.Id);

            var keyMap = new Dictionary<int, Dictionary<int, string>>();
            foreach (var ak in answerKeys.Where(k => k.Booklet.Equals(booklet, StringComparison.OrdinalIgnoreCase)))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<int, string>>(ak.AnswersJson) ?? new();
                keyMap[ak.ExamSectionId] = dict;
            }

            var studentAnswers = await _context.StudentAnswers
                .Where(a => a.StudentSheetId == sheet.Id)
                .ToListAsync();

            int totalQuestions = template.Sections.Sum(s => s.QuestionCount);
            double effectiveTotal = exam.TotalScore * (exam.PercentRatio / 100.0);
            double pointPerQ = exam.PointPerQuestion > 0
                ? exam.PointPerQuestion
                : (totalQuestions > 0 ? (effectiveTotal / totalQuestions) : 0);

            var oldResults = await _context.ExamResults
                .Where(r => r.StudentSheetId == sheet.Id)
                .ToListAsync();
            _context.ExamResults.RemoveRange(oldResults);

            foreach (var section in template.Sections)
            {
                keyMap.TryGetValue(section.Id, out var correctAnswers);
                correctAnswers ??= new Dictionary<int, string>();

                int correct = 0;
                int wrong = 0;
                int empty = 0;

                var secAnswers = studentAnswers.Where(a => a.ExamSectionId == section.Id).ToList();

                for (int q = 1; q <= section.QuestionCount; q++)
                {
                    var ans = secAnswers.FirstOrDefault(a => a.QuestionNumber == q);
                    string selected = ans?.SelectedOption ?? string.Empty;

                    correctAnswers.TryGetValue(q, out var correctOpt);

                    if (string.IsNullOrEmpty(selected)) empty++;
                    else if (string.Equals(selected, correctOpt, StringComparison.OrdinalIgnoreCase)) correct++;
                    else wrong++;
                }

                _context.ExamResults.Add(new ExamResult
                {
                    StudentSheetId = sheet.Id,
                    ExamSectionId = section.Id,
                    CorrectCount = correct,
                    WrongCount = wrong,
                    EmptyCount = empty,
                    Score = Math.Round(correct * pointPerQ, 2),
                    CalculatedAt = DateTime.UtcNow
                });
            }
        }

        private async Task<Exam> GetOwnedExamAsync(int examId, int ownerUserId)
        {
            var exam = await _examRepository.GetByIdAsync(examId);
            if (exam == null || exam.OwnerUserId != ownerUserId)
            {
                throw new ArgumentException("Sınav bulunamadı veya bu sınava erişim yetkiniz yok.");
            }
            return exam;
        }
    }
}