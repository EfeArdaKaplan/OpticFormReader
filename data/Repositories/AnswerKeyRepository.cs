using core.Entities;
using core.Interfaces;
using data.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace data.Repositories
{
    public class AnswerKeyRepository : IAnswerKeyRepository
    {
        private readonly AppDbContext _context;

        public AnswerKeyRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AnswerKey?> GetByExamIdAsync(int examId)
        {
            return await _context.AnswerKeys.FirstOrDefaultAsync(a => a.ExamId == examId);
        }

        public async Task<AnswerKey> AddAsync(AnswerKey answerKey)
        {
            _context.AnswerKeys.Add(answerKey);
            await _context.SaveChangesAsync();
            return answerKey;
        }

        public async Task UpdateAsync(AnswerKey answerKey)
        {
            _context.Entry(answerKey).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var answerKey = await _context.AnswerKeys.FindAsync(id);
            if (answerKey != null)
            {
                _context.AnswerKeys.Remove(answerKey);
                await _context.SaveChangesAsync();
            }
        }
    }
}
