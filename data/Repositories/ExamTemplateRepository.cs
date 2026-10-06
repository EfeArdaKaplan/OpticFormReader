using core.Entities;
using core.Interfaces;
using data.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;

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
            return await _context.ExamTemplates.FindAsync(id);
        }

        public async Task<List<ExamTemplate>> GetByOwnerUserIdAsync(int ownerUserId)
        {
            return await _context.ExamTemplates
                .Where(e => e.OwnerUserId == ownerUserId)
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
            _context.ExamTemplates.Update(examTemplate);
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
