using core.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace WebApi.Hubs
{
    [Authorize]
    public class EvaluationHub : Hub
    {
        private readonly IExamRepository _examRepository;

        public EvaluationHub(IExamRepository examRepository)
        {
            _examRepository = examRepository;
        }

        // Frontend belirli bir sınavın odasına katılır (Sadece sınavın sahibi katılabilir)
        public async Task JoinExamRoom(int examId)
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                throw new HubException("Oturum kimliği doğrulanamadı.");
            }

            var exam = await _examRepository.GetByIdAsync(examId);
            if (exam == null || exam.OwnerUserId != userId)
            {
                throw new HubException("Bu sınav odasına erişim yetkiniz yok.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"exam_{examId}");
        }

        public async Task LeaveExamRoom(int examId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"exam_{examId}");
        }
    }
}
