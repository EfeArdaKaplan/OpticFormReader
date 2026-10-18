using core.Entities;

namespace core.Interfaces
{
    public interface IStudentSheetRepository
    {
        Task<StudentSheet> AddAsync(StudentSheet sheet);
        Task AddRangeAsync(IEnumerable<StudentSheet> sheets);
        Task<StudentSheet?> GetByIdAsync(int id);
        Task<List<StudentSheet>> GetByExamIdAsync(int examId);
        Task<List<StudentSheet>> GetByBatchIdAsync(int uploadBatchId);
        Task UpdateAsync(StudentSheet sheet);
        Task DeleteAsync(int id);
        Task DeleteByExamIdAsync(int examId);
    }
}