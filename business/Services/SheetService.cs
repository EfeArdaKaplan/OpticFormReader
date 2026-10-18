using core.DTOs;
using core.Entities;
using core.interfaces;
using core.Interfaces;
using System.Text.RegularExpressions;

namespace business.Services
{
    public class SheetService : ISheetService
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".pdf"
        };

        private const long MaxFileSize = 50 * 1024 * 1024; // 50 MB

        private readonly IExamRepository _examRepository;
        private readonly IUploadBatchRepository _batchRepository;
        private readonly IStudentSheetRepository _sheetRepository;
        private readonly IFileStorage _storage;

        public SheetService(
            IExamRepository examRepository,
            IUploadBatchRepository batchRepository,
            IStudentSheetRepository sheetRepository,
            IFileStorage storage)
        {
            _examRepository = examRepository;
            _batchRepository = batchRepository;
            _sheetRepository = sheetRepository;
            _storage = storage;
        }

        public async Task<UploadResultDto> UploadSheetsAsync(
            int examId,
            int ownerUserId,
            List<UploadFileDto> files,
            CancellationToken cancellationToken = default)
        {
            await GetOwnedExamAsync(examId, ownerUserId);

            if (files == null || files.Count == 0)
            {
                throw new ArgumentException("Yüklenecek dosya seçilmedi.");
            }

            var result = new UploadResultDto();

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                if (file.Length > MaxFileSize)
                {
                    throw new ArgumentException($"{file.FileName} boyutu 50 MB sınırını aşıyor.");
                }

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                {
                    throw new ArgumentException(
                        $"{file.FileName} desteklenmeyen dosya türü. Sadece JPG, PNG ve PDF kabul edilir.");
                }

                bool isPdf = extension == ".pdf";
                string format = isPdf ? "PDF" : extension.TrimStart('.').ToUpperInvariant();

                // Dosyayı belleğe alarak sayfa sayısını ve SeaweedFS kaydını hazırlıyoruz
                using var memoryStream = new MemoryStream();
                await file.Content.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;

                int pageCount = 1;
                if (isPdf)
                {
                    pageCount = DetectPdfPageCount(memoryStream);
                    memoryStream.Position = 0;
                }

                // 1) SeaweedFS'e Kaydet
                string fileKey = await _storage.SaveAsync(
                    memoryStream,
                    extension,
                    file.ContentType,
                    $"exams/{examId}/batches",
                    cancellationToken);

                // 2) UploadBatch Kaydını Oluştur
                var batch = new UploadBatch
                {
                    ExamId = examId,
                    OriginalFileName = file.FileName,
                    Format = format,
                    FileKey = fileKey,
                    PageCount = pageCount,
                    ProcessingStatus = "Pending",
                    UploadedAt = DateTime.UtcNow
                };

                await _batchRepository.AddAsync(batch);
                result.Batches.Add(MapBatchToDto(batch));

                // 3) StudentSheet (Öğrenci Kağıdı) Kayıtlarını Oluştur
                var sheetsToCreate = new List<StudentSheet>();

                for (int page = 1; page <= pageCount; page++)
                {
                    sheetsToCreate.Add(new StudentSheet
                    {
                        ExamId = examId,
                        UploadBatchId = batch.Id,
                        PageNumber = page,
                        ImageKey = fileKey, // Görüntüde doğrudan görsel, PDF'te kaynak dosya
                        ProcessingStatus = "Pending",
                        NeedsReview = false,
                        UploadedAt = DateTime.UtcNow
                    });
                }

                await _sheetRepository.AddRangeAsync(sheetsToCreate);
                result.Sheets.AddRange(sheetsToCreate.Select(MapSheetToDto));
            }

            result.TotalFiles = result.Batches.Count;
            result.TotalSheets = result.Sheets.Count;

            return result;
        }

        public async Task<List<UploadBatchDto>> GetBatchesByExamIdAsync(int examId, int ownerUserId)
        {
            await GetOwnedExamAsync(examId, ownerUserId);
            var batches = await _batchRepository.GetByExamIdAsync(examId);
            return batches.Select(MapBatchToDto).ToList();
        }

        public async Task<List<StudentSheetDto>> GetSheetsByExamIdAsync(int examId, int ownerUserId)
        {
            await GetOwnedExamAsync(examId, ownerUserId);
            var sheets = await _sheetRepository.GetByExamIdAsync(examId);
            return sheets.Select(MapSheetToDto).ToList();
        }

        public async Task<StudentSheetDto?> GetSheetByIdAsync(int sheetId, int ownerUserId)
        {
            var sheet = await _sheetRepository.GetByIdAsync(sheetId);
            if (sheet == null) return null;

            await GetOwnedExamAsync(sheet.ExamId, ownerUserId);
            return MapSheetToDto(sheet);
        }

        public async Task DeleteSheetAsync(int sheetId, int ownerUserId)
        {
            var sheet = await _sheetRepository.GetByIdAsync(sheetId);
            if (sheet == null)
            {
                throw new ArgumentException("Kağıt bulunamadı.");
            }

            await GetOwnedExamAsync(sheet.ExamId, ownerUserId);
            await _sheetRepository.DeleteAsync(sheetId);
        }

        public async Task DeleteBatchAsync(int batchId, int ownerUserId)
        {
            var batch = await _batchRepository.GetByIdAsync(batchId);
            if (batch == null)
            {
                throw new ArgumentException("Dosya grubu bulunamadı.");
            }

            await GetOwnedExamAsync(batch.ExamId, ownerUserId);

            // İlişkili kağıtları sil
            var sheets = await _sheetRepository.GetByBatchIdAsync(batchId);
            foreach (var sheet in sheets)
            {
                await _sheetRepository.DeleteAsync(sheet.Id);
            }

            // SeaweedFS'teki asıl dosyayı sil
            await _storage.DeleteAsync(batch.FileKey);

            // Batch kaydını sil
            await _batchRepository.DeleteAsync(batchId);
        }

        private async Task<Exam> GetOwnedExamAsync(int examId, int ownerUserId)
        {
            var exam = await _examRepository.GetByIdAsync(examId);
            if (exam == null || exam.OwnerUserId != ownerUserId)
            {
                throw new ArgumentException("Sınav bulunamadı veya bu sınava erişim yetkiniz yok.");
            }
            return exam;
        }

        // PDF içerisindeki sayfa sayısını hızlıca okur
        private static int DetectPdfPageCount(Stream stream)
        {
            try
            {
                using var reader = new StreamReader(stream, leaveOpen: true);
                string content = reader.ReadToEnd();
                var matches = Regex.Matches(content, @"/Type\s*/Page[^s]");
                return matches.Count > 0 ? matches.Count : 1;
            }
            catch
            {
                return 1;
            }
        }

        private static UploadBatchDto MapBatchToDto(UploadBatch b) => new()
        {
            Id = b.Id,
            ExamId = b.ExamId,
            OriginalFileName = b.OriginalFileName,
            Format = b.Format,
            FileKey = b.FileKey,
            PageCount = b.PageCount,
            ProcessingStatus = b.ProcessingStatus,
            UploadedAt = b.UploadedAt
        };

        private static StudentSheetDto MapSheetToDto(StudentSheet s) => new()
        {
            Id = s.Id,
            ExamId = s.ExamId,
            UploadBatchId = s.UploadBatchId,
            PageNumber = s.PageNumber,
            StudentNumber = s.StudentNumber,
            FirstName = s.FirstName,
            LastName = s.LastName,
            TcNo = s.TcNo,
            Booklet = s.Booklet,
            ImageKey = s.ImageKey,
            ProcessingStatus = s.ProcessingStatus,
            NeedsReview = s.NeedsReview,
            ReviewReason = s.ReviewReason,
            UploadedAt = s.UploadedAt
        };
    }
}