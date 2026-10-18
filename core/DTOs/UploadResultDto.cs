namespace core.DTOs
{
    public class UploadResultDto
    {
        public int TotalFiles { get; set; }
        public int TotalSheets { get; set; }
        public List<UploadBatchDto> Batches { get; set; } = new();
        public List<StudentSheetDto> Sheets { get; set; } = new();
    }
}