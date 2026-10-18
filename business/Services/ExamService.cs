using core.DTOs;
using core.Entities;
using core.interfaces;
using core.Interfaces;

namespace business.Services
{
    public class ExamService : IExamService
    {
        private readonly IExamRepository _examRepository;
        private readonly IExamTemplateRepository _examTemplateRepository;

        public ExamService(
            IExamRepository examRepository,
            IExamTemplateRepository examTemplateRepository)
        {
            _examRepository = examRepository;
            _examTemplateRepository = examTemplateRepository;
        }

        public async Task<ExamDto> CreateAsync(
            int ownerUserId,
            CreateExamDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new ArgumentException(
                    "Sınav başlığı boş olamaz.");
            }

            var template = await _examTemplateRepository
                .GetByIdAsync(request.ExamTemplateId);

            if (template == null || template.OwnerUserId != ownerUserId)
            {
                throw new ArgumentException(
                    "Sınav şablonu bulunamadı veya erişim yetkiniz yok.");
            }

            if (request.TotalScore < 10 || request.TotalScore > 1000)
            {
                throw new ArgumentException(
                    "Toplam puan 10 ile 1000 arasında olmalıdır.");
            }

            if (request.PercentRatio < 50 || request.PercentRatio > 200)
            {
                throw new ArgumentException(
                    "Hesaplama yüzdesi 50 ile 200 arasında olmalıdır.");
            }

            if (request.PointPerQuestion < 0)
            {
                throw new ArgumentException(
                    "Soru başı puan negatif olamaz.");
            }

            var totalQuestions = template.Sections
                .Sum(section => section.QuestionCount);

            if (totalQuestions <= 0)
            {
                throw new ArgumentException(
                    "Sınav şablonunda soru bulunamadı.");
            }

            var pointPerQuestion = request.PointPerQuestion > 0
                ? request.PointPerQuestion
                : (request.TotalScore * request.PercentRatio / 100.0)
                    / totalQuestions;

            var exam = new Exam
            {
                OwnerUserId = ownerUserId,
                ExamTemplateId = request.ExamTemplateId,
                Title = request.Title.Trim(),
                IsBulk = request.IsBulk,
                TotalScore = request.TotalScore,
                PercentRatio = request.PercentRatio,
                PointPerQuestion = pointPerQuestion
            };

            var created = await _examRepository.AddAsync(exam);

            return MapToDto(created);
        }

        public async Task<List<ExamDto>> GetMyExamsAsync(int ownerUserId)
        {
            var exams = await _examRepository
                .GetByOwnerUserIdAsync(ownerUserId);

            return exams.Select(MapToDto).ToList();
        }

        public async Task<ExamDto?> GetByIdAsync(int id, int ownerUserId)
        {
            var exam = await _examRepository.GetByIdAsync(id);

            if (exam == null || exam.OwnerUserId != ownerUserId)
            {
                return null;
            }

            return MapToDto(exam);
        }

        public async Task DeleteAsync(int id, int ownerUserId)
        {
            var exam = await _examRepository.GetByIdAsync(id);

            if (exam == null || exam.OwnerUserId != ownerUserId)
            {
                throw new ArgumentException(
                    "Sınav bulunamadı veya erişim yetkiniz yok.");
            }

            await _examRepository.DeleteAsync(id);
        }

        private static ExamDto MapToDto(Exam exam)
        {
            return new ExamDto
            {
                Id = exam.Id,
                ExamTemplateId = exam.ExamTemplateId,
                Title = exam.Title,
                IsBulk = exam.IsBulk,
                TotalScore = exam.TotalScore,
                PercentRatio = exam.PercentRatio,
                PointPerQuestion = exam.PointPerQuestion,
                CreatedAt = exam.CreatedAt
            };
        }
    }
}