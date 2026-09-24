using core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace core.interfaces
{
    public interface IExamService
    {
        Task<Exam> CreateExamAsync(Exam exam);
        Task<ExamResult> CalculateResultAsync(int studentSheetId);
    }
}
