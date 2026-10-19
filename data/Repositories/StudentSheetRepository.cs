using core.Entities;
using core.Interfaces;
using data.Context;
using Microsoft.EntityFrameworkCore;

namespace data.Repositories
{
    public class StudentSheetRepository : IStudentSheetRepository
    {
        private readonly AppDbContext _context;

        public StudentSheetRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<StudentSheet> AddAsync(StudentSheet sheet)
        {
            _context.StudentSheets.Add(sheet);
            await _context.SaveChangesAsync();
            return sheet;
        }

        public async Task AddRangeAsync(IEnumerable<StudentSheet> sheets)
        {
            await _context.StudentSheets.AddRangeAsync(sheets);
            await _context.SaveChangesAsync();
        }

        public async Task<StudentSheet?> GetByIdAsync(int id)
        {
            return await _context.StudentSheets.FindAsync(id);
        }

        public async Task<List<StudentSheet>> GetByExamIdAsync(int examId)
        {
            return await _context.StudentSheets
                .Where(s => s.ExamId == examId)
                .OrderBy(s => s.Id)
                .ToListAsync();
        }

        public async Task<List<StudentSheet>> GetByBatchIdAsync(int uploadBatchId)
        {
            return await _context.StudentSheets
                .AsNoTracking()
                .Where(s => s.UploadBatchId == uploadBatchId)
                .OrderBy(s => s.PageNumber)
                .ToListAsync();
        }

        public async Task UpdateAsync(StudentSheet sheet)
        {
            _context.StudentSheets.Update(sheet);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var sheet = await _context.StudentSheets.FindAsync(id);
            if (sheet != null)
            {
                _context.StudentSheets.Remove(sheet);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteByExamIdAsync(int examId)
        {
            var sheets = await _context.StudentSheets
                .Where(s => s.ExamId == examId)
                .ToListAsync();

            if (sheets.Count > 0)
            {
                _context.StudentSheets.RemoveRange(sheets);
                await _context.SaveChangesAsync();
            }
        }
    }
}