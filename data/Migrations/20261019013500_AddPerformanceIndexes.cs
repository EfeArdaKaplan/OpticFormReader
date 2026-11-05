using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using data.Context;

#nullable disable

namespace data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261019013500_AddPerformanceIndexes")]
    public partial class AddPerformanceIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Users_Email\" ON \"Users\" (\"Email\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Exams_OwnerUserId\" ON \"Exams\" (\"OwnerUserId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_ExamTemplates_OwnerUserId\" ON \"ExamTemplates\" (\"OwnerUserId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_StudentSheets_ExamId\" ON \"StudentSheets\" (\"ExamId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_StudentSheets_UploadBatchId\" ON \"StudentSheets\" (\"UploadBatchId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_StudentAnswers_StudentSheetId\" ON \"StudentAnswers\" (\"StudentSheetId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_ExamResults_StudentSheetId\" ON \"ExamResults\" (\"StudentSheetId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_UploadBatches_ExamId\" ON \"UploadBatches\" (\"ExamId\");");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Users_Email\";");
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
