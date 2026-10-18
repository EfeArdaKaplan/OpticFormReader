using Microsoft.AspNetCore.SignalR;

namespace WebApi.Hubs
{
    public class EvaluationHub : Hub
    {
        // Frontend belirli bir sınavın odasına katılır (Örn: exam_5)
        public async Task JoinExamRoom(int examId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"exam_{examId}");
        }

        public async Task LeaveExamRoom(int examId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"exam_{examId}");
        }
    }
}