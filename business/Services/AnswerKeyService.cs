using core.DTOs;
using core.Entities;
using core.interfaces;
using core.Interfaces;
using System.Text.Json;

namespace business.Services
{
    public class AnswerKeyService : IAnswerKeyService
    {
        private readonly IAnswerKeyRepository _answerKeyRepository;
        private readonly IExamRepository _examRepository;
        private readonly IExamTemplateRepository _examTemplateRepository;

        public AnswerKeyService(
            IAnswerKeyRepository answerKeyRepository,
            IExamRepository examRepository,
            IExamTemplateRepository examTemplateRepository)
        {
            _answerKeyRepository = answerKeyRepository;
            _examRepository = examRepository;
            _examTemplateRepository = examTemplateRepository;
        }

        public async Task<List<AnswerKeyDto>> SaveManualAsync(int examId, int ownerUserId, SaveAnswerKeyDto request)
        {
            var exam = await GetOwnedExamAsync(examId, ownerUserId);

            var template = await _examTemplateRepository.GetByIdAsync(exam.ExamTemplateId);
            if (template == null)
            {
                throw new Exception("Sınavın şablonu bulunamadı.");
            }

            // Her ders tam olarak bir kez gelmeli
            var incomingIds = request.Sections.Select(s => s.ExamSectionId).ToList();
            if (incomingIds.Count != incomingIds.Distinct().Count())
            {
                throw new Exception("Aynı ders birden fazla kez gönderilmiş.");
            }

            var templateIds = template.Sections.Select(s => s.Id).ToHashSet();
            if (!templateIds.SetEquals(incomingIds))
            {
                throw new Exception("Cevap anahtarında şablondaki tüm dersler eksiksiz olmalıdır.");
            }

            var newKeys = new List<AnswerKey>();

            foreach (var sectionAnswers in request.Sections)
            {
                var section = template.Sections.First(s => s.Id == sectionAnswers.ExamSectionId);
                var normalized = NormalizeAnswers(section, sectionAnswers.Answers);

                newKeys.Add(new AnswerKey
                {
                    ExamId = examId,
                    ExamSectionId = section.Id,
                    IsScanned = false,
                    AnswersJson = JsonSerializer.Serialize(normalized)
                });
            }

            var saved = await _answerKeyRepository.ReplaceForExamAsync(examId, newKeys);
            return saved.Select(k => MapToDto(k, template)).ToList();
        }

        public async Task<List<AnswerKeyDto>> GetByExamIdAsync(int examId, int ownerUserId)
        {
            var exam = await _examRepository.GetByIdAsync(examId);
            if (exam == null || exam.OwnerUserId != ownerUserId)
            {
                return new List<AnswerKeyDto>();
            }

            var template = await _examTemplateRepository.GetByIdAsync(exam.ExamTemplateId);
            if (template == null)
            {
                return new List<AnswerKeyDto>();
            }

            var keys = await _answerKeyRepository.GetByExamIdAsync(examId);
            return keys.Select(k => MapToDto(k, template)).ToList();
        }

        public async Task DeleteAsync(int examId, int ownerUserId)
        {
            await GetOwnedExamAsync(examId, ownerUserId);

            var keys = await _answerKeyRepository.GetByExamIdAsync(examId);
            if (keys.Count == 0)
            {
                throw new Exception("Bu sınav için cevap anahtarı bulunamadı.");
            }

            await _answerKeyRepository.DeleteByExamIdAsync(examId);
        }

        // Bir dersin cevaplarını doğrular ve büyük harfe çevirir
        private static Dictionary<int, string> NormalizeAnswers(ExamSection section, Dictionary<int, string> answers)
        {
            if (answers.Count != section.QuestionCount)
            {
                throw new Exception($"{section.Name} dersinin tüm soruları cevaplanmalıdır. Beklenen: {section.QuestionCount}, gelen: {answers.Count}.");
            }

            var validOptions = "ABCDE".Substring(0, section.OptionCount);
            var normalized = new Dictionary<int, string>();

            foreach (var item in answers)
            {
                if (item.Key < 1 || item.Key > section.QuestionCount)
                {
                    throw new Exception($"{section.Name} dersi için geçersiz soru numarası: {item.Key}. Numaralar 1 ile {section.QuestionCount} arasında olmalıdır.");
                }

                var option = (item.Value ?? string.Empty).Trim().ToUpperInvariant();
                if (option.Length != 1 || !validOptions.Contains(option))
                {
                    throw new Exception($"{section.Name} dersi {item.Key}. soru için geçersiz şık: '{item.Value}'. Geçerli şıklar: {string.Join(", ", validOptions.ToCharArray())}.");
                }

                normalized[item.Key] = option;
            }

            return normalized;
        }

        private async Task<Exam> GetOwnedExamAsync(int examId, int ownerUserId)
        {
            var exam = await _examRepository.GetByIdAsync(examId);
            if (exam == null || exam.OwnerUserId != ownerUserId)
            {
                throw new Exception("Sınav bulunamadı veya bu sınava erişim yetkiniz yok.");
            }
            return exam;
        }

        private static AnswerKeyDto MapToDto(AnswerKey key, ExamTemplate template)
        {
            var sectionName = template.Sections.FirstOrDefault(s => s.Id == key.ExamSectionId)?.Name ?? string.Empty;

            return new AnswerKeyDto
            {
                Id = key.Id,
                ExamId = key.ExamId,
                ExamSectionId = key.ExamSectionId,
                SectionName = sectionName,
                IsScanned = key.IsScanned,
                Answers = JsonSerializer.Deserialize<Dictionary<int, string>>(key.AnswersJson) ?? new(),
                CreatedAt = key.CreatedAt
            };
        }
    }
}