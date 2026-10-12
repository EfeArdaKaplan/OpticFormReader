using core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace core.Interfaces
{
    public interface IAnswerKeyRepository
    {
        Task<AnswerKey?> GetByExamIdAsync(int examId);
        Task<AnswerKey> AddAsync(AnswerKey answerKey);
        Task UpdateAsync(AnswerKey answerKey);
        Task DeleteAsync(int id);
    }
}
