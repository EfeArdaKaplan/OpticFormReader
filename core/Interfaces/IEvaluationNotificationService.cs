using core.DTOs;

namespace core.Interfaces
{
    public interface IEvaluationNotificationService
    {
        Task BroadcastProgressAsync(EvaluationProgressDto progress);
    }
}
