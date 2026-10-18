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
                .OrderBy(a => a.Booklet)
                .ThenBy(a => a.ExamSectionId)
                .ToListAsync();
        }

        public async Task<List<AnswerKey>> ReplaceForExamAndBookletAsync(
            int examId,
            string booklet,
            List<AnswerKey> newKeys)
        {
            booklet = booklet.Trim().ToUpperInvariant();

            var oldKeys = await _context.AnswerKeys
                .Where(a =>
                    a.ExamId == examId &&
                    a.Booklet == booklet)
                .ToListAsync();

            var incomingSectionIds = newKeys
                .Select(k => k.ExamSectionId)
                .ToHashSet();

            // Bu kitapçıkta artık bulunmayan ders kayıtlarını kaldır.
            var obsoleteKeys = oldKeys
                .Where(k => !incomingSectionIds.Contains(k.ExamSectionId))
                .ToList();

            _context.AnswerKeys.RemoveRange(obsoleteKeys);

            var savedKeys = new List<AnswerKey>();

            foreach (var newKey in newKeys)
            {
                var existingKey = oldKeys.FirstOrDefault(
                    k => k.ExamSectionId == newKey.ExamSectionId);

                if (existingKey != null)
                {
                    // Var olan dersin cevaplarını güncelle.
                    existingKey.AnswersJson = newKey.AnswersJson;
                    existingKey.IsScanned = newKey.IsScanned;

                    savedKeys.Add(existingKey);
                }
                else
                {
                    _context.AnswerKeys.Add(newKey);
                    savedKeys.Add(newKey);
                }
            }

            await _context.SaveChangesAsync();

            return savedKeys;
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