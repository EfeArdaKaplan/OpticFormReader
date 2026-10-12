using core.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace core.Interfaces
{
    public interface IAnswerKeyService
    {
        Task<AnswerKeyDto> SaveManuelAsync(int examId, int ownerUserId, SaveAnswerKeyDto request);
        Task<AnswerKeyDto?> GetByExamIdAsync(int examId, int ownerUserId);
        Task DeleteAsync(int examId, int ownerUserId);
    }
}
