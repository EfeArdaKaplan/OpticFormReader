using core.DTOs;
using core.Entities;
using core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace business.Services
{
    public class ExamTemplateService : IExamTemplateService
    {
        private readonly IExamTemplateRepository _examTemplateRepository;

        public ExamTemplateService(IExamTemplateRepository examTemplateRepository)
        {
            _examTemplateRepository = examTemplateRepository;
        }

        public async Task<ExamTemplateDto> CreateAsync(int ownerUserId, CreateExamTemplateDto request)
        {
            if (request.QuestionCount <1 || request.QuestionCount > 150)
            {
                throw new Exception("Soru sayisi 1 ile 150 arasinda olmalidir.");
            }

            if (request.OptionCount < 2 || request.OptionCount > 5)
            {
                throw new Exception("Şık sayısı 2 ile 5 arasında olmalıdır.");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new Exception("Şablon ismi boş olamaz.");
            }

            var examTemplate = new ExamTemplate
            {
                OwnerUserId = ownerUserId,
                Name = request.Name,
                QuestionCount = request.QuestionCount,
                OptionCount = request.OptionCount
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

            if (request.QuestionCount < 1 || request.QuestionCount > 150)
            {
                throw new Exception("Soru sayısı 1 ile 150 arasında olmalıdır.");
            }

            template.Name = request.Name;
            template.QuestionCount = request.QuestionCount;
            template.OptionCount = request.OptionCount;

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

        private static ExamTemplateDto MapToDto(ExamTemplate template)
        {
            return new ExamTemplateDto
            {
                Id = template.Id,
                Name = template.Name,
                QuestionCount = template.QuestionCount,
                OptionCount = template.OptionCount,
                CreatedAt = template.CreatedAt
            };
        }
    }
}
