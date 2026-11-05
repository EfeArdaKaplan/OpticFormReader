using core.Entities;
using data.Security;
using Microsoft.EntityFrameworkCore;

namespace data.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<ExamTemplate> ExamTemplates { get; set; }
        public DbSet<ExamSection> ExamSections { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<AnswerKey> AnswerKeys { get; set; }
        public DbSet<StudentSheet> StudentSheets { get; set; }
        public DbSet<StudentAnswer> StudentAnswers { get; set; }
        public DbSet<ExamResult> ExamResults { get; set; }
        public DbSet<UploadBatch> UploadBatches { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var piiConverter = SensitiveDataHasher.CreatePersonalDataConverter();
            var emailConverter = SensitiveDataHasher.CreateNormalizedEmailConverter();

            // 1. Users Tablosu: Kullanıcı Adı (FullName) ve E-Posta (Email) veritabanında $h256$... olarak hash'lenir
            // PasswordHash zaten BCrypt ($2a$11$...) ile hash'lenmektedir.
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.FullName).HasConversion(piiConverter);
                entity.Property(u => u.Email).HasConversion(emailConverter);
            });

            // 2. StudentSheets Tablosu: Öğrenci No, Ad, Soyad, TC Kimlik No ve Görsel Anahtarı hash'lenir
            modelBuilder.Entity<StudentSheet>(entity =>
            {
                entity.Property(s => s.StudentNumber).HasConversion(piiConverter);
                entity.Property(s => s.FirstName).HasConversion(piiConverter);
                entity.Property(s => s.LastName).HasConversion(piiConverter);
                entity.Property(s => s.TcNo).HasConversion(piiConverter);
                entity.Property(s => s.ImageKey).HasConversion(piiConverter);
            });

            // 3. UploadBatches Tablosu: Yüklenen Dosya Adı ve Depolama Anahtarı hash'lenir
            modelBuilder.Entity<UploadBatch>(entity =>
            {
                entity.Property(b => b.OriginalFileName).HasConversion(piiConverter);
                entity.Property(b => b.FileKey).HasConversion(piiConverter);
            });

            // 4. ExamTemplates Tablosu: Kurum Adı hash'lenir
            modelBuilder.Entity<ExamTemplate>(entity =>
            {
                entity.Property(t => t.InstitutionName).HasConversion(piiConverter);
            });

            modelBuilder.Entity<ExamSection>()
                .HasOne<ExamTemplate>()
                .WithMany(t => t.Sections)
                .HasForeignKey(s => s.ExamTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AnswerKey>()
                .HasIndex(k => new { k.ExamId, k.ExamSectionId, k.Booklet })
                .IsUnique();
        }
    }
}
