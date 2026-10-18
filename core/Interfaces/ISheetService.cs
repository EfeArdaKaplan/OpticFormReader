using core.DTOs;

namespace core.Interfaces
{
    public interface ISheetService
    {
        Task<UploadResultDto> UploadSheetsAsync(
            int examId,
            int ownerUserId,
            List<UploadFileDto> files,
            CancellationToken cancellationToken = default);

        Task<List<UploadBatchDto>> GetBatchesByExamIdAsync(int examId, int ownerUserId);
        Task<List<StudentSheetDto>> GetSheetsByExamIdAsync(int examId, int ownerUserId);
        Task<StudentSheetDto?> GetSheetByIdAsync(int sheetId, int ownerUserId);
        Task DeleteSheetAsync(int sheetId, int ownerUserId);
        Task DeleteBatchAsync(int batchId, int ownerUserId);
    }
}