using core.Entities;
using core.Interfaces;
using data.Context;
using Microsoft.EntityFrameworkCore;

namespace data.Repositories
{
    public class UploadBatchRepository : IUploadBatchRepository
    {
        private readonly AppDbContext _context;

        public UploadBatchRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UploadBatch> AddAsync(UploadBatch batch)
        {
            _context.UploadBatches.Add(batch);
            await _context.SaveChangesAsync();
            return batch;
        }

        public async Task<UploadBatch?> GetByIdAsync(int id)
        {
            return await _context.UploadBatches.FindAsync(id);
        }

        public async Task<List<UploadBatch>> GetByExamIdAsync(int examId)
        {
            return await _context.UploadBatches
                .Where(b => b.ExamId == examId)
                .OrderByDescending(b => b.UploadedAt)
                .ToListAsync();
        }

        public async Task UpdateAsync(UploadBatch batch)
        {
            _context.UploadBatches.Update(batch);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var batch = await _context.UploadBatches.FindAsync(id);
            if (batch != null)
            {
                _context.UploadBatches.Remove(batch);
                await _context.SaveChangesAsync();
            }
        }
    }
}