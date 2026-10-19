using core.Entities;
using core.Interfaces;
using data.Context;
using Microsoft.EntityFrameworkCore;

namespace data.Repositories
{
    public class ExamTemplateRepository : IExamTemplateRepository
    {
        private readonly AppDbContext _context;

        public ExamTemplateRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ExamTemplate?> GetByIdAsync(int id)
        {
            return await _context.ExamTemplates
                .Include(t => t.Sections)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<List<ExamTemplate>> GetByOwnerUserIdAsync(int ownerUserId)
        {
            return await _context.ExamTemplates
                .AsNoTracking()
                .Include(t => t.Sections)
                .Where(t => t.OwnerUserId == ownerUserId)
                .ToListAsync();
        }

        public async Task<ExamTemplate> AddAsync(ExamTemplate examTemplate)
        {
            _context.ExamTemplates.Add(examTemplate);
            await _context.SaveChangesAsync();
            return examTemplate;
        }

        public async Task UpdateAsync(ExamTemplate examTemplate)
        {
            // GetByIdAsync ile çekilen şablon zaten EF tarafından takip ediliyor,
            // yapılan değişiklikleri (yeni/silinen dersler dahil) SaveChanges kaydeder.
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var examTemplate = await _context.ExamTemplates.FindAsync(id);
            if (examTemplate != null)
            {
                _context.ExamTemplates.Remove(examTemplate);
                await _context.SaveChangesAsync();
            }
        }
    }
}