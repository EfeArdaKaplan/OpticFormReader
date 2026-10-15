using core.DTOs;

namespace core.Interfaces
{
    public interface IAnswerKeyService
    {
        Task<List<AnswerKeyDto>> SaveManualAsync(int examId, int ownerUserId, SaveAnswerKeyDto request);
        Task<List<AnswerKeyDto>> GetByExamIdAsync(int examId, int ownerUserId);
        Task DeleteAsync(int examId, int ownerUserId);
    }
}