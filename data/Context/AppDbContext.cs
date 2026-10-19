using core.Entities;
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

            modelBuilder.Entity<ExamSection>()
                .HasOne<ExamTemplate>()
                .WithMany(t => t.Sections)
                .HasForeignKey(s => s.ExamTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AnswerKey>()
                .HasIndex(k => new { k.ExamId, k.ExamSectionId, k.Booklet })
                .IsUnique();

            // Performans İndeksleri (Postgres Best Practices & SQL Optimization)
            modelBuilder.Entity<Exam>()
                .HasIndex(e => e.OwnerUserId)
                .HasDatabaseName("IX_Exams_OwnerUserId");

            modelBuilder.Entity<ExamTemplate>()
                .HasIndex(t => t.OwnerUserId)
                .HasDatabaseName("IX_ExamTemplates_OwnerUserId");

            modelBuilder.Entity<StudentSheet>()
                .HasIndex(s => s.ExamId)
                .HasDatabaseName("IX_StudentSheets_ExamId");

            modelBuilder.Entity<StudentSheet>()
                .HasIndex(s => s.UploadBatchId)
                .HasDatabaseName("IX_StudentSheets_UploadBatchId");

            modelBuilder.Entity<StudentAnswer>()
                .HasIndex(a => a.StudentSheetId)
                .HasDatabaseName("IX_StudentAnswers_StudentSheetId");

            modelBuilder.Entity<ExamResult>()
                .HasIndex(r => r.StudentSheetId)
                .HasDatabaseName("IX_ExamResults_StudentSheetId");

            modelBuilder.Entity<UploadBatch>()
                .HasIndex(b => b.ExamId)
                .HasDatabaseName("IX_UploadBatches_ExamId");
        }
    }
}