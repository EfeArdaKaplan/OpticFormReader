using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using data.Context;

#nullable disable

namespace data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent and zero-downtime PostgreSQL indexes
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Exams_OwnerUserId\" ON \"Exams\" (\"OwnerUserId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_ExamTemplates_OwnerUserId\" ON \"ExamTemplates\" (\"OwnerUserId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_StudentSheets_ExamId\" ON \"StudentSheets\" (\"ExamId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_StudentSheets_UploadBatchId\" ON \"StudentSheets\" (\"UploadBatchId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_StudentAnswers_StudentSheetId\" ON \"StudentAnswers\" (\"StudentSheetId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_ExamResults_StudentSheetId\" ON \"ExamResults\" (\"StudentSheetId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_UploadBatches_ExamId\" ON \"UploadBatches\" (\"ExamId\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Exams_OwnerUserId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_ExamTemplates_OwnerUserId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_StudentSheets_ExamId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_StudentSheets_UploadBatchId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_StudentAnswers_StudentSheetId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_ExamResults_StudentSheetId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_UploadBatches_ExamId\";");
        }
    }
}
