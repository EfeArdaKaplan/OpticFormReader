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
        }
    }
}