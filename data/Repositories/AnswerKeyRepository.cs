using core.Entities;
using core.Interfaces;
using data.Context;
using Microsoft.EntityFrameworkCore;

namespace data.Repositories
{
    public class AnswerKeyRepository : IAnswerKeyRepository
    {
        private readonly AppDbContext _context;

        public AnswerKeyRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<AnswerKey>> GetByExamIdAsync(int examId)
        {
            return await _context.AnswerKeys
                .Where(a => a.ExamId == examId)
                .OrderBy(a => a.ExamSectionId)
                .ToListAsync();
        }

        public async Task<List<AnswerKey>> ReplaceForExamAsync(int examId, List<AnswerKey> newKeys)
        {
            var old = await _context.AnswerKeys
                .Where(a => a.ExamId == examId)
                .ToListAsync();

            _context.AnswerKeys.RemoveRange(old);
            _context.AnswerKeys.AddRange(newKeys);

            await _context.SaveChangesAsync();
            return newKeys;
        }

        public async Task DeleteByExamIdAsync(int examId)
        {
            var keys = await _context.AnswerKeys
                .Where(a => a.ExamId == examId)
                .ToListAsync();

            if (keys.Count > 0)
            {
                _context.AnswerKeys.RemoveRange(keys);
                await _context.SaveChangesAsync();
            }
        }
    }
}