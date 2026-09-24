using core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace core.interfaces
{
    public interface IExamRepository
    {
        Task<Exam?> GetByIdAsync(int id);
        Task<List<Exam>> GetByOwnerUserIdAsync(int ownerUserId);
        Task<Exam> AddAsync(Exam exam);
        Task UpdateAsync(Exam exam);
        Task DeleteAsync(int id);
    }
}
