using core.DTOs;

namespace core.Interfaces
{
    public interface IResultService
    {
        Task<ExamResultSummaryDto> GetExamResultsAsync(int examId, int ownerUserId);
        Task<StudentDetailedReportDto> GetStudentReportAsync(int examId, int sheetId, int ownerUserId);
        Task<StudentResultItemDto> UpdateSheetManualAsync(int examId, int sheetId, int ownerUserId, ManualCorrectionDto request);
    }
}