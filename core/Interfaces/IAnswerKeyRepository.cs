using core.Entities;

namespace core.Interfaces
{
    public interface IAnswerKeyRepository
    {
        Task<List<AnswerKey>> GetByExamIdAsync(int examId);
        Task<List<AnswerKey>> ReplaceForExamAsync(int examId, List<AnswerKey> newKeys);
        Task DeleteByExamIdAsync(int examId);
    }
}