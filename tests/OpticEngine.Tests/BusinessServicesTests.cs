using business.Services;
using core.DTOs;
using core.Entities;
using core.interfaces;
using core.Interfaces;
using QuestPDF.Infrastructure;
using System.Text;
using Xunit;

namespace OpticEngine.Tests
{
    public class BusinessServicesTests
    {
        public BusinessServicesTests()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        [Fact]
        public async Task ExamTemplateService_CreateAndGeneratePdf_ProducesValidLayoutAndPdfBytes()
        {
            // Arrange
            var repo = new InMemoryTemplateRepository();
            var formGen = new FormGeneratorService();
            var service = new ExamTemplateService(repo, formGen);

            var createDto = new CreateExamTemplateDto
            {
                Name = "Fizik 1. Vize",
                InstitutionName = "Mühendislik Fakültesi",
                StudentNoDigits = 8,
                Sections = new List<CreateExamSectionDto>
                {
                    new() { Name = "Mekanik", QuestionCount = 15, OptionCount = 5 },
                    new() { Name = "Elektromanyetizma", QuestionCount = 10, OptionCount = 5 }
                }
            };

            // Act
            var created = await service.CreateAsync(ownerUserId: 42, createDto);
            var pdfBytes = await service.GenerateFormPdfAsync(created.Id, ownerUserId: 42);

            // Assert
            Assert.Equal("Fizik 1. Vize", created.Name);
            Assert.Equal(25, created.TotalQuestionCount);
            Assert.Equal(2, created.Sections.Count);
            Assert.NotNull(pdfBytes);
            Assert.True(pdfBytes.Length > 1000);

            string pdfHeader = Encoding.ASCII.GetString(pdfBytes, 0, 5);
            Assert.Equal("%PDF-", pdfHeader);
        }

        [Fact]
        public async Task ExamTemplateService_Validation_RejectsInvalidInputs()
        {
            // Arrange
            var repo = new InMemoryTemplateRepository();
            var service = new ExamTemplateService(repo, new FormGeneratorService());

            // 150 soru sınırını aşan şablon
            var tooManyQuestionsDto = new CreateExamTemplateDto
            {
                Name = "Aşırı Büyük Sınav",
                StudentNoDigits = 8,
                Sections = new List<CreateExamSectionDto>
                {
                    new() { Name = "Bölüm 1", QuestionCount = 100, OptionCount = 5 },
                    new() { Name = "Bölüm 2", QuestionCount = 60, OptionCount = 5 }
                }
            };

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => service.CreateAsync(1, tooManyQuestionsDto));
        }

        [Fact]
        public async Task ExamTemplateService_OwnershipCheck_PreventsOtherUserModification()
        {
            // Arrange
            var repo = new InMemoryTemplateRepository();
            var service = new ExamTemplateService(repo, new FormGeneratorService());

            var dto = new CreateExamTemplateDto
            {
                Name = "Özel Şablon",
                StudentNoDigits = 8,
                Sections = new List<CreateExamSectionDto>
                {
                    new() { Name = "Matematik", QuestionCount = 10, OptionCount = 5 }
                }
            };

            var created = await service.CreateAsync(ownerUserId: 10, dto);

            // Act & Assert: Kullanıcı 99, Kullanıcı 10'un şablonunu silemez ve güncelleyemez
            await Assert.ThrowsAsync<Exception>(() => service.DeleteAsync(created.Id, ownerUserId: 99));
            await Assert.ThrowsAsync<Exception>(() => service.UpdateAsync(created.Id, ownerUserId: 99, dto));
            var unauthorizedGet = await service.GetByIdAsync(created.Id, ownerUserId: 99);
            Assert.Null(unauthorizedGet);
        }

        [Fact]
        public async Task ExamService_CreateExam_CalculatesPointPerQuestionAccurately()
        {
            // Arrange
            var templateRepo = new InMemoryTemplateRepository();
            var examRepo = new InMemoryExamRepository();
            var templateService = new ExamTemplateService(templateRepo, new FormGeneratorService());
            var examService = new ExamService(examRepo, templateRepo);

            var tpl = await templateService.CreateAsync(ownerUserId: 7, new CreateExamTemplateDto
            {
                Name = "Kimya Sınavı",
                StudentNoDigits = 8,
                Sections = new List<CreateExamSectionDto>
                {
                    new() { Name = "Genel Kimya", QuestionCount = 25, OptionCount = 5 }
                }
            });

            // Act: 100 puan, %100 oran, 25 soru -> Soru başı 4.0 puan olmalı
            var exam = await examService.CreateAsync(ownerUserId: 7, new CreateExamDto
            {
                ExamTemplateId = tpl.Id,
                Title = "Kimya Vize",
                TotalScore = 100,
                PercentRatio = 100,
                PointPerQuestion = 0
            });

            // Assert
            Assert.Equal(4.0, exam.PointPerQuestion, precision: 2);
        }

        [Fact]
        public async Task ExamService_OwnershipCheck_IsolatesTenantData()
        {
            // Arrange
            var templateRepo = new InMemoryTemplateRepository();
            var examRepo = new InMemoryExamRepository();
            var templateService = new ExamTemplateService(templateRepo, new FormGeneratorService());
            var examService = new ExamService(examRepo, templateRepo);

            var tpl = await templateService.CreateAsync(ownerUserId: 1, new CreateExamTemplateDto
            {
                Name = "Şablon 1",
                StudentNoDigits = 8,
                Sections = new List<CreateExamSectionDto>
                {
                    new() { Name = "Ders 1", QuestionCount = 10, OptionCount = 5 }
                }
            });

            var exam = await examService.CreateAsync(ownerUserId: 1, new CreateExamDto
            {
                ExamTemplateId = tpl.Id,
                Title = "Kullanıcı 1 Sınavı",
                TotalScore = 100,
                PercentRatio = 100
            });

            // Act & Assert: Kullanıcı 2 bu sınavı göremez veya silemez
            Assert.Null(await examService.GetByIdAsync(exam.Id, ownerUserId: 2));
            await Assert.ThrowsAsync<ArgumentException>(() => examService.DeleteAsync(exam.Id, ownerUserId: 2));
        }

        [Fact]
        public async Task AnswerKeyService_SaveAndRetrieve_NormalizesOptionsAndEnforcesValidation()
        {
            // Arrange
            var templateRepo = new InMemoryTemplateRepository();
            var examRepo = new InMemoryExamRepository();
            var keyRepo = new InMemoryAnswerKeyRepository();

            var templateService = new ExamTemplateService(templateRepo, new FormGeneratorService());
            var examService = new ExamService(examRepo, templateRepo);
            var answerKeyService = new AnswerKeyService(keyRepo, examRepo, templateRepo);

            var tpl = await templateService.CreateAsync(1, new CreateExamTemplateDto
            {
                Name = "Test Şablon",
                StudentNoDigits = 8,
                Sections = new List<CreateExamSectionDto>
                {
                    new() { Name = "Matematik", QuestionCount = 3, OptionCount = 4 } // 4 şıklı (A, B, C, D)
                }
            });

            var exam = await examService.CreateAsync(1, new CreateExamDto
            {
                ExamTemplateId = tpl.Id,
                Title = "Test Sınav",
                TotalScore = 100,
                PercentRatio = 100
            });

            int secId = tpl.Sections[0].Id;

            // Act: Küçük harfli ve boşluklu şıklar gönderildiğinde normalize edilmeli
            var saved = await answerKeyService.SaveManualAsync(exam.Id, 1, new SaveAnswerKeyDto
            {
                Booklet = "a",
                Sections = new List<SectionAnswersDto>
                {
                    new()
                    {
                        ExamSectionId = secId,
                        Answers = new Dictionary<int, string>
                        {
                            { 1, " a " },
                            { 2, "b" },
                            { 3, "D" }
                        }
                    }
                }
            });

            // Assert
            Assert.Single(saved);
            Assert.Equal("A", saved[0].Booklet);
            Assert.Equal("A", saved[0].Answers[1]);
            Assert.Equal("B", saved[0].Answers[2]);
            Assert.Equal("D", saved[0].Answers[3]);

            // 4 şıklı sınavda 'E' şıkkı gönderilirse ArgumentException fırlatmalı
            await Assert.ThrowsAsync<ArgumentException>(() =>
                answerKeyService.SaveManualAsync(exam.Id, 1, new SaveAnswerKeyDto
                {
                    Booklet = "A",
                    Sections = new List<SectionAnswersDto>
                    {
                        new()
                        {
                            ExamSectionId = secId,
                            Answers = new Dictionary<int, string> { { 1, "E" }, { 2, "A" }, { 3, "B" } }
                        }
                    }
                }));
        }

        [Fact]
        public void FastOmrEngine_CorruptedStream_FlagsNeedsReviewGracefully()
        {
            // Arrange
            var engine = new FastOmrEngine();
            var formGen = new FormGeneratorService();
            var layout = formGen.BuildLayout(new ExamTemplate
            {
                Id = 1,
                Name = "Test",
                StudentNoDigits = 8,
                Sections = new List<ExamSection>
                {
                    new() { Id = 1, Name = "Ders", Order = 1, QuestionCount = 5, OptionCount = 5 }
                }
            });

            using var brokenStream = new MemoryStream(new byte[] { 0x00, 0xFF, 0x13, 0x37, 0x42 });

            // Act
            var res = engine.Process(brokenStream, layout);

            // Assert
            Assert.True(res.NeedsReview);
            Assert.NotNull(res.ReviewReason);
        }

        [Fact]
        public void FastOmrEngine_VectorSvgOrPdfStream_ResolvesDeterministically()
        {
            // Arrange
            var engine = new FastOmrEngine();
            var formGen = new FormGeneratorService();
            var layout = formGen.BuildLayout(new ExamTemplate
            {
                Id = 1,
                Name = "Test",
                StudentNoDigits = 8,
                Sections = new List<ExamSection>
                {
                    new() { Id = 1, Name = "Matematik", Order = 1, QuestionCount = 10, OptionCount = 5 }
                }
            });

            string svgContent = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"400\" height=\"560\"><rect width=\"400\" height=\"560\"/></svg>";
            using var svgStream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));

            // Act
            var res = engine.Process(svgStream, layout);

            // Assert
            Assert.False(res.NeedsReview);
            Assert.Equal("A", res.Booklet);
            Assert.Equal(8, res.StudentNumber.Length);
            Assert.Equal(10, res.Answers.Count);
        }

        [Fact]
        public async Task SheetService_UploadAndDeleteBatch_ValidatesExtensionsAndStoresCorrectly()
        {
            // Arrange
            var examRepo = new InMemoryExamRepository();
            await examRepo.AddAsync(new Exam { Id = 5, OwnerUserId = 1, ExamTemplateId = 1, Title = "Vize" });

            var batchRepo = new InMemoryBatchRepository();
            var sheetRepo = new InMemorySheetRepository();
            var storage = new InMemoryFileStorage();
            var sheetService = new SheetService(examRepo, batchRepo, sheetRepo, storage);

            string fakePdf = "%PDF-1.4\n1 0 obj << /Type /Page >> endobj\n2 0 obj << /Type /Page >> endobj";
            using var pdfStream = new MemoryStream(Encoding.UTF8.GetBytes(fakePdf));

            // Act: 2 sayfalı PDF yükle
            var uploadRes = await sheetService.UploadSheetsAsync(5, 1, new List<UploadFileDto>
            {
                new()
                {
                    FileName = "tarama.pdf",
                    ContentType = "application/pdf",
                    Length = pdfStream.Length,
                    Content = pdfStream
                }
            });

            // Assert: 1 Batch ve 2 StudentSheet oluşmalı
            Assert.Equal(1, uploadRes.TotalFiles);
            Assert.Equal(2, uploadRes.TotalSheets);

            // Desteklenmeyen uzantı (.exe) reddedilmeli
            using var badStream = new MemoryStream(new byte[] { 1, 2, 3 });
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sheetService.UploadSheetsAsync(5, 1, new List<UploadFileDto>
                {
                    new() { FileName = "malware.exe", ContentType = "application/octet-stream", Length = 3, Content = badStream }
                }));
        }

        [Fact]
        public async Task ExportService_ExportCsv_GeneratesValidUtf8BomExcelFile()
        {
            // Arrange
            var fakeResultService = new FakeResultService();
            var exportService = new ExportService(fakeResultService);

            // Act
            var (bytes, fileName, contentType) = await exportService.ExportResultsCsvAsync(examId: 1, ownerUserId: 1);

            // Assert: UTF-8 BOM (0xEF, 0xBB, 0xBF) ile başlamalı
            Assert.Equal("text/csv; charset=utf-8", contentType);
            Assert.EndsWith("_Sonuclar.csv", fileName);
            Assert.True(bytes.Length > 3);
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);

            string csvText = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            Assert.Contains("Kemal Sunal", csvText);
            Assert.Contains("Temel Matematik (Doğru)", csvText);
        }

        // --- In-Memory Test Doubles ---

        private sealed class InMemoryTemplateRepository : IExamTemplateRepository
        {
            private readonly List<ExamTemplate> _items = new();
            private int _nextId = 1;
            private int _nextSecId = 1;

            public Task<ExamTemplate> AddAsync(ExamTemplate examTemplate)
            {
                examTemplate.Id = _nextId++;
                foreach (var s in examTemplate.Sections)
                {
                    if (s.Id == 0) s.Id = _nextSecId++;
                    s.ExamTemplateId = examTemplate.Id;
                }
                _items.Add(examTemplate);
                return Task.FromResult(examTemplate);
            }

            public Task DeleteAsync(int id)
            {
                _items.RemoveAll(x => x.Id == id);
                return Task.CompletedTask;
            }

            public Task<ExamTemplate?> GetByIdAsync(int id) =>
                Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

            public Task<List<ExamTemplate>> GetByOwnerUserIdAsync(int ownerUserId) =>
                Task.FromResult(_items.Where(x => x.OwnerUserId == ownerUserId).ToList());

            public Task UpdateAsync(ExamTemplate examTemplate)
            {
                foreach (var s in examTemplate.Sections)
                {
                    if (s.Id == 0) s.Id = _nextSecId++;
                }
                return Task.CompletedTask;
            }
        }

        private sealed class InMemoryExamRepository : IExamRepository
        {
            private readonly List<Exam> _items = new();
            private int _nextId = 1;

            public Task<Exam> AddAsync(Exam exam)
            {
                if (exam.Id == 0) exam.Id = _nextId++;
                _items.Add(exam);
                return Task.FromResult(exam);
            }

            public Task DeleteAsync(int id)
            {
                _items.RemoveAll(x => x.Id == id);
                return Task.CompletedTask;
            }

            public Task<Exam?> GetByIdAsync(int id) =>
                Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

            public Task<List<Exam>> GetByOwnerUserIdAsync(int ownerUserId) =>
                Task.FromResult(_items.Where(x => x.OwnerUserId == ownerUserId).ToList());

            public Task UpdateAsync(Exam exam) => Task.CompletedTask;
        }

        private sealed class InMemoryAnswerKeyRepository : IAnswerKeyRepository
        {
            private readonly List<AnswerKey> _items = new();
            private int _nextId = 1;

            public Task DeleteByExamIdAsync(int examId)
            {
                _items.RemoveAll(k => k.ExamId == examId);
                return Task.CompletedTask;
            }

            public Task<List<AnswerKey>> GetByExamIdAsync(int examId) =>
                Task.FromResult(_items.Where(k => k.ExamId == examId).ToList());

            public Task<List<AnswerKey>> ReplaceForExamAndBookletAsync(int examId, string booklet, List<AnswerKey> newKeys)
            {
                _items.RemoveAll(k => k.ExamId == examId && k.Booklet == booklet);
                foreach (var k in newKeys)
                {
                    k.Id = _nextId++;
                    _items.Add(k);
                }
                return Task.FromResult(newKeys);
            }
        }

        private sealed class InMemoryBatchRepository : IUploadBatchRepository
        {
            private readonly List<UploadBatch> _items = new();
            private int _nextId = 1;

            public Task<UploadBatch> AddAsync(UploadBatch batch)
            {
                batch.Id = _nextId++;
                _items.Add(batch);
                return Task.FromResult(batch);
            }

            public Task DeleteAsync(int id)
            {
                _items.RemoveAll(b => b.Id == id);
                return Task.CompletedTask;
            }

            public Task<List<UploadBatch>> GetByExamIdAsync(int examId) =>
                Task.FromResult(_items.Where(b => b.ExamId == examId).ToList());

            public Task<UploadBatch?> GetByIdAsync(int id) =>
                Task.FromResult(_items.FirstOrDefault(b => b.Id == id));

            public Task UpdateAsync(UploadBatch batch) => Task.CompletedTask;
        }

        private sealed class InMemorySheetRepository : IStudentSheetRepository
        {
            private readonly List<StudentSheet> _items = new();
            private int _nextId = 1;

            public Task<StudentSheet> AddAsync(StudentSheet sheet)
            {
                sheet.Id = _nextId++;
                _items.Add(sheet);
                return Task.FromResult(sheet);
            }

            public Task AddRangeAsync(IEnumerable<StudentSheet> sheets)
            {
                foreach (var s in sheets)
                {
                    s.Id = _nextId++;
                    _items.Add(s);
                }
                return Task.CompletedTask;
            }

            public Task DeleteAsync(int id)
            {
                _items.RemoveAll(s => s.Id == id);
                return Task.CompletedTask;
            }

            public Task DeleteByExamIdAsync(int examId)
            {
                _items.RemoveAll(s => s.ExamId == examId);
                return Task.CompletedTask;
            }

            public Task<List<StudentSheet>> GetByBatchIdAsync(int uploadBatchId) =>
                Task.FromResult(_items.Where(s => s.UploadBatchId == uploadBatchId).ToList());

            public Task<List<StudentSheet>> GetByExamIdAsync(int examId) =>
                Task.FromResult(_items.Where(s => s.ExamId == examId).ToList());

            public Task<StudentSheet?> GetByIdAsync(int id) =>
                Task.FromResult(_items.FirstOrDefault(s => s.Id == id));

            public Task UpdateAsync(StudentSheet sheet) => Task.CompletedTask;
        }

        private sealed class InMemoryFileStorage : IFileStorage
        {
            private readonly Dictionary<string, byte[]> _store = new();

            public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
            {
                _store.Remove(key);
                return Task.CompletedTask;
            }

            public string GetPresignedUrl(string key, TimeSpan expiry, bool isUpload = false) =>
                $"http://localhost:8333/optic-sheets/{key}";

            public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
                Task.FromResult<Stream>(new MemoryStream(_store[key]));

            public async Task<string> SaveAsync(Stream content, string extension, string contentType, string folder, CancellationToken cancellationToken = default)
            {
                using var ms = new MemoryStream();
                await content.CopyToAsync(ms, cancellationToken);
                string key = $"{folder}/{Guid.NewGuid():N}{extension}";
                _store[key] = ms.ToArray();
                return key;
            }
        }

        private sealed class FakeResultService : IResultService
        {
            public Task<ExamResultSummaryDto> GetExamResultsAsync(int examId, int ownerUserId)
            {
                return Task.FromResult(new ExamResultSummaryDto
                {
                    ExamId = examId,
                    ExamTitle = "Matematik 1. Ara Sınav",
                    TotalStudents = 1,
                    TotalQuestions = 20,
                    ClassAverage = 85.0,
                    HighestScore = 85.0,
                    LowestScore = 85.0,
                    Students = new List<StudentResultItemDto>
                    {
                        new()
                        {
                            Rank = 1,
                            StudentSheetId = 1,
                            StudentNumber = "20261001",
                            FullName = "Kemal Sunal",
                            TcNo = "10000000146",
                            Booklet = "A",
                            CorrectCount = 17,
                            WrongCount = 2,
                            EmptyCount = 1,
                            NetScore = 17,
                            TotalScore = 85.0,
                            SectionResults = new List<SectionResultSummaryDto>
                            {
                                new() { SectionId = 1, SectionName = "Temel Matematik", CorrectCount = 17, WrongCount = 2, EmptyCount = 1, Score = 85.0 }
                            }
                        }
                    }
                });
            }

            public Task<StudentDetailedReportDto> GetStudentReportAsync(int examId, int sheetId, int ownerUserId) =>
                throw new NotImplementedException();

            public Task<StudentResultItemDto> UpdateSheetManualAsync(int examId, int sheetId, int ownerUserId, ManualCorrectionDto request) =>
                throw new NotImplementedException();
        }
    }
}
