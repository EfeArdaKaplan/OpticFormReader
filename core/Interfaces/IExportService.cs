namespace core.Interfaces
{
    public interface IExportService
    {
        Task<(byte[] FileBytes, string FileName, string ContentType)> ExportResultsCsvAsync(int examId, int ownerUserId);
    }
}