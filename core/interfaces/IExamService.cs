using core.DTOs;

namespace core.Interfaces
{
    public interface IExamService
    {
        Task<ExamDto> CreateAsync(int ownerUserId, CreateExamDto request);
        Task<List<ExamDto>> GetMyExamsAsync(int ownerUserId);
        Task<ExamDto?> GetByIdAsync(int id, int ownerUserId);
        Task DeleteAsync(int id, int ownerUserId);
    }
}