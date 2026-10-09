using core.DTOs;
using core.Entities;
using core.interfaces;
using core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace business.Services
{
    public class ExamService : IExamService
    {
        private readonly IExamRepository _examRepository;
        private readonly IExamTemplateRepository _examTemplateRepository;

        public ExamService(IExamRepository examRepository, IExamTemplateRepository examTemplateRepository)
        {
            _examRepository = examRepository;
            _examTemplateRepository = examTemplateRepository;
        }

        public async Task<ExamDto> CreateAsync(int ownerUserId, CreateExamDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new ArgumentException("Sinav basligi bos olamazs.");
            }

            var template = await _examTemplateRepository.GetByIdAsync(request.ExamTemplateId);
            
            if (template == null || template.OwnerUserId != ownerUserId)
            {
                throw new ArgumentException("Sinav şablonu bulunamadi veya yetkiniz yok.");
            }

            var exam = new Exam
            {
                OwnerUserId = ownerUserId,
                ExamTemplateId = request.ExamTemplateId,
                Title = request.Title,
                IsBulk = request.IsBulk,
            };

            var created = await _examRepository.AddAsync(exam);
            return MapToDto(created);
        }

        public async Task<List<ExamDto>> GetMyExamsAsync(int ownerUserId)
        {
            var exams = await _examRepository.GetByOwnerUserIdAsync(ownerUserId);
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
                throw new ArgumentException("Sinav bulunamadi veya yetkiniz yok.");
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
                CreatedAt = exam.CreatedAt
            };
        }
    }
}
