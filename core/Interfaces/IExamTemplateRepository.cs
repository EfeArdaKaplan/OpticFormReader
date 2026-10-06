using System;
using System.Collections.Generic;
using System.Text;
using core.DTOs;
using core.Entities;

namespace core.Interfaces
{
    public interface IExamTemplateRepository
    {
        Task<ExamTemplate?> GetByIdAsync(int id);
        Task<List<ExamTemplate>> GetByOwnerUserIdAsync(int ownerUserId);
        Task<ExamTemplate> AddAsync(ExamTemplate examTemplate);
        Task UpdateAsync(ExamTemplate examTemplate);
        Task DeleteAsync(int id);
    }
}
