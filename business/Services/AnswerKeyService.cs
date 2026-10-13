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

        public async Task<AnswerKeyDto> SaveManualAsync(int examId, int ownerUserId, SaveAnswerKeyDto request)
        {
            var exam = await GetOwnedExamAsync(examId, ownerUserId);

            var template = await _examTemplateRepository.GetByIdAsync(exam.ExamTemplateId);
            if (template == null)
            {
                throw new Exception("Sınavın şablonu bulunamadı.");
            }

            if (request.Answers.Count != template.QuestionCount)
            {
                throw new Exception($"Tüm soruların cevabı girilmelidir. Beklenen: {template.QuestionCount}, gelen: {request.Answers.Count}.");
            }

            var validOptions = "ABCDE".Substring(0, template.OptionCount);
            var normalized = new Dictionary<int, string>();

            foreach (var item in request.Answers)
            {
                if (item.Key < 1 || item.Key > template.QuestionCount)
                {
                    throw new Exception($"Geçersiz soru numarası: {item.Key}. Soru numaraları 1 ile {template.QuestionCount} arasında olmalıdır.");
                }

                var option = (item.Value ?? string.Empty).Trim().ToUpperInvariant();
                if (option.Length != 1 || !validOptions.Contains(option))
                {
                    throw new Exception($"{item.Key}. soru için geçersiz şık: '{item.Value}'. Geçerli şıklar: {string.Join(", ", validOptions.ToCharArray())}.");
                }

                normalized[item.Key] = option;
            }

            var json = JsonSerializer.Serialize(normalized);

            var existing = await _answerKeyRepository.GetByExamIdAsync(examId);
            if (existing != null)
            {
                existing.AnswersJson = json;
                existing.IsScanned = false;
                await _answerKeyRepository.UpdateAsync(existing);
                return MapToDto(existing);
            }

            var answerKey = new AnswerKey
            {
                ExamId = examId,
                IsScanned = false,
                AnswersJson = json
            };

            var created = await _answerKeyRepository.AddAsync(answerKey);
            return MapToDto(created);
        }

        public async Task<AnswerKeyDto?> GetByExamIdAsync(int examId, int ownerUserId)
        {
            var exam = await _examRepository.GetByIdAsync(examId);
            if (exam == null || exam.OwnerUserId != ownerUserId)
            {
                return null;
            }

            var answerKey = await _answerKeyRepository.GetByExamIdAsync(examId);
            return answerKey == null ? null : MapToDto(answerKey);
        }

        public async Task DeleteAsync(int examId, int ownerUserId)
        {
            await GetOwnedExamAsync(examId, ownerUserId);

            var answerKey = await _answerKeyRepository.GetByExamIdAsync(examId);
            if (answerKey == null)
            {
                throw new Exception("Bu sınav için cevap anahtarı bulunamadı.");
            }

            await _answerKeyRepository.DeleteAsync(answerKey.Id);
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

        private static AnswerKeyDto MapToDto(AnswerKey answerKey)
        {
            return new AnswerKeyDto
            {
                Id = answerKey.Id,
                ExamId = answerKey.ExamId,
                IsScanned = answerKey.IsScanned,
                Answers = JsonSerializer.Deserialize<Dictionary<int, string>>(answerKey.AnswersJson) ?? new(),
                CreatedAt = answerKey.CreatedAt
            };
        }
    }
}