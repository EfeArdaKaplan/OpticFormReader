using core.DTOs;
using core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using WebApi.Hubs;

namespace WebApi.Services
{
    public class EvaluationNotificationService : IEvaluationNotificationService
    {
        private readonly IHubContext<EvaluationHub> _hubContext;

        public EvaluationNotificationService(IHubContext<EvaluationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task BroadcastProgressAsync(EvaluationProgressDto progress)
        {
            try
            {
                await _hubContext.Clients
                    .Group($"exam_{progress.ExamId}")
                    .SendAsync("ProgressUpdate", progress);
            }
            catch
            {
                // SignalR iletilemese dahi ana akışı bozma
            }
        }
    }
}
