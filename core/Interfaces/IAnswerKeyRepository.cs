using core.Entities;

namespace core.Interfaces
{
    public interface IAnswerKeyRepository
    {
        Task<List<AnswerKey>> GetByExamIdAsync(int examId);

        Task<List<AnswerKey>> ReplaceForExamAndBookletAsync(
            int examId,
            string booklet,
            List<AnswerKey> newKeys);

        Task DeleteByExamIdAsync(int examId);
    }
}