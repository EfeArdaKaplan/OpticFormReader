using System;
using System.Collections.Generic;
using System.Text;
using core.DTOs;

namespace core.Interfaces
{
    public interface IExamTemplateService
    {
        Task<ExamTemplateDto> CreateAsync(int ownerUserId, CreateExamTemplateDto request);
        Task<List<ExamTemplateDto>> GetMyTemplatesAsync(int ownerUserId);
        Task<ExamTemplateDto?> GetByIdAsync(int id, int ownerUserId);
        Task UpdateAsync(int id, int ownerUserId, CreateExamTemplateDto request);
        Task DeleteAsync(int id, int ownerUserId);
        Task<byte[]> GenerateFormPdfAsync(int id, int ownerUserId);
    }
}
