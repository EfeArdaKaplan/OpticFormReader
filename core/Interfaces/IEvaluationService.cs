using core.DTOs;

namespace core.Interfaces
{
    public interface IEvaluationService
    {
        Task<EvaluationSummaryDto> EvaluateExamAsync(
            int examId,
            int ownerUserId,
            CancellationToken cancellationToken = default);

        EvaluationProgressDto GetProgress(int examId);
    }
}