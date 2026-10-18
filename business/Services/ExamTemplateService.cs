using core.DTOs;
using core.Entities;
using core.Interfaces;

namespace business.Services
{
    public class ExamTemplateService : IExamTemplateService
    {
        private const int MaxTotalQuestions = 150;

        private readonly IExamTemplateRepository _examTemplateRepository;

        public ExamTemplateService(IExamTemplateRepository examTemplateRepository)
        {
            _examTemplateRepository = examTemplateRepository;
        }

        public async Task<ExamTemplateDto> CreateAsync(int ownerUserId, CreateExamTemplateDto request)
        {
            Validate(request);

            var examTemplate = new ExamTemplate
            {
                OwnerUserId = ownerUserId,
                Name = request.Name.Trim(),
                InstitutionName = string.IsNullOrWhiteSpace(request.InstitutionName)
                    ? string.Empty
                    : request.InstitutionName.Trim(),
                StudentNoDigits = request.StudentNoDigits,
                Sections = BuildSections(request.Sections)
            };

            var created = await _examTemplateRepository.AddAsync(examTemplate);
            return MapToDto(created);
        }

        public async Task<List<ExamTemplateDto>> GetMyTemplatesAsync(int ownerUserId)
        {
            var templates = await _examTemplateRepository.GetByOwnerUserIdAsync(ownerUserId);
            return templates.Select(MapToDto).ToList();
        }

        public async Task<ExamTemplateDto?> GetByIdAsync(int id, int ownerUserId)
        {
            var template = await _examTemplateRepository.GetByIdAsync(id);

            if (template == null || template.OwnerUserId != ownerUserId)
            {
                return null;
            }

            return MapToDto(template);
        }

        public async Task UpdateAsync(int id, int ownerUserId, CreateExamTemplateDto request)
        {
            var template = await _examTemplateRepository.GetByIdAsync(id);

            if (template == null || template.OwnerUserId != ownerUserId)
            {
                throw new Exception("Şablon bulunamadı veya bu şablona erişim yetkiniz yok.");
            }

            Validate(request);

            template.Name = request.Name.Trim();
            template.InstitutionName = string.IsNullOrWhiteSpace(request.InstitutionName)
                ? string.Empty
                : request.InstitutionName.Trim();
            template.StudentNoDigits = request.StudentNoDigits;

            // Eski dersleri at, yenilerini koy
            template.Sections.Clear();
            foreach (var section in BuildSections(request.Sections))
            {
                template.Sections.Add(section);
            }

            await _examTemplateRepository.UpdateAsync(template);
        }

        public async Task DeleteAsync(int id, int ownerUserId)
        {
            var template = await _examTemplateRepository.GetByIdAsync(id);

            if (template == null || template.OwnerUserId != ownerUserId)
            {
                throw new Exception("Şablon bulunamadı veya bu şablona erişim yetkiniz yok.");
            }

            await _examTemplateRepository.DeleteAsync(id);
        }

        private static void Validate(CreateExamTemplateDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new Exception("Şablon ismi boş olamaz.");
            }

            if (request.StudentNoDigits < 1 || request.StudentNoDigits > 11)
            {
                throw new Exception("Öğrenci numarası hane sayısı 1 ile 11 arasında olmalıdır.");
            }

            if (request.Sections == null || request.Sections.Count == 0)
            {
                throw new Exception("En az bir ders eklemelisiniz.");
            }

            foreach (var section in request.Sections)
            {
                if (string.IsNullOrWhiteSpace(section.Name))
                {
                    throw new Exception("Ders ismi boş olamaz.");
                }

                if (section.QuestionCount < 1)
                {
                    throw new Exception($"{section.Name} dersi için soru sayısı en az 1 olmalıdır.");
                }

                if (section.OptionCount < 2 || section.OptionCount > 5)
                {
                    throw new Exception($"{section.Name} dersi için şık sayısı 2 ile 5 arasında olmalıdır.");
                }
            }

            var total = request.Sections.Sum(s => s.QuestionCount);
            if (total > MaxTotalQuestions)
            {
                throw new Exception($"Toplam soru sayısı en fazla {MaxTotalQuestions} olabilir. Girilen: {total}.");
            }
        }

        private static List<ExamSection> BuildSections(List<CreateExamSectionDto> dtos)
        {
            return dtos
                .Select((s, index) => new ExamSection
                {
                    Name = s.Name.Trim(),
                    Order = index + 1,
                    QuestionCount = s.QuestionCount,
                    OptionCount = s.OptionCount
                })
                .ToList();
        }

        private static ExamTemplateDto MapToDto(ExamTemplate template)
        {
            var sections = template.Sections
                .OrderBy(s => s.Order)
                .Select(s => new ExamSectionDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Order = s.Order,
                    QuestionCount = s.QuestionCount,
                    OptionCount = s.OptionCount
                })
                .ToList();

            return new ExamTemplateDto
            {
                Id = template.Id,
                Name = template.Name,
                InstitutionName = template.InstitutionName,
                StudentNoDigits = template.StudentNoDigits,
                TotalQuestionCount = sections.Sum(s => s.QuestionCount),
                Sections = sections,
                CreatedAt = template.CreatedAt
            };
        }
    }
}