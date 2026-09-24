using core.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace data.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<ExamTemplate> ExamTemplates { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<AnswerKey> AnswerKeys { get; set; }
        public DbSet<StudentSheet> StudentSheets { get; set; }
        public DbSet<StudentAnswer> StudentAnswers { get; set; }
        public DbSet<ExamResult> ExamResults { get; set; }
    }
}
