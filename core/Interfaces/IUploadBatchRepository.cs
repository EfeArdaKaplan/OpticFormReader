using core.Entities;

namespace core.Interfaces
{
    public interface IUploadBatchRepository
    {
        Task<UploadBatch> AddAsync(UploadBatch batch);
        Task<UploadBatch?> GetByIdAsync(int id);
        Task<List<UploadBatch>> GetByExamIdAsync(int examId);
        Task UpdateAsync(UploadBatch batch);
        Task DeleteAsync(int id);
    }
}