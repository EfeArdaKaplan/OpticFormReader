using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace data.Migrations
{
    /// <inheritdoc />
    public partial class BookletAndSheetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StudentName",
                table: "StudentSheets",
                newName: "TcNo");

            migrationBuilder.RenameColumn(
                name: "ImagePath",
                table: "StudentSheets",
                newName: "LastName");

            migrationBuilder.AddColumn<string>(
                name: "Booklet",
                table: "StudentSheets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "StudentSheets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ImageKey",
                table: "StudentSheets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "NeedsReview",
                table: "StudentSheets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PageNumber",
                table: "StudentSheets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "StudentSheets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UploadBatchId",
                table: "StudentSheets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExamSectionId",
                table: "StudentAnswers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InstitutionName",
                table: "ExamTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "TotalScore",
                table: "Exams",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "ExamSectionId",
                table: "ExamResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Booklet",
                table: "AnswerKeys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "UploadBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamId = table.Column<int>(type: "integer", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    Format = table.Column<string>(type: "text", nullable: false),
                    FileKey = table.Column<string>(type: "text", nullable: false),
                    PageCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "text", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadBatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerKeys_ExamId_ExamSectionId_Booklet",
                table: "AnswerKeys",
                columns: new[] { "ExamId", "ExamSectionId", "Booklet" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UploadBatches");

            migrationBuilder.DropIndex(
                name: "IX_AnswerKeys_ExamId_ExamSectionId_Booklet",
                table: "AnswerKeys");

            migrationBuilder.DropColumn(
                name: "Booklet",
                table: "StudentSheets");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "StudentSheets");

            migrationBuilder.DropColumn(
                name: "ImageKey",
                table: "StudentSheets");

            migrationBuilder.DropColumn(
                name: "NeedsReview",
                table: "StudentSheets");

            migrationBuilder.DropColumn(
                name: "PageNumber",
                table: "StudentSheets");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "StudentSheets");

            migrationBuilder.DropColumn(
                name: "UploadBatchId",
                table: "StudentSheets");

            migrationBuilder.DropColumn(
                name: "ExamSectionId",
                table: "StudentAnswers");

            migrationBuilder.DropColumn(
                name: "InstitutionName",
                table: "ExamTemplates");

            migrationBuilder.DropColumn(
                name: "TotalScore",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ExamSectionId",
                table: "ExamResults");

            migrationBuilder.DropColumn(
                name: "Booklet",
                table: "AnswerKeys");

            migrationBuilder.RenameColumn(
                name: "TcNo",
                table: "StudentSheets",
                newName: "StudentName");

            migrationBuilder.RenameColumn(
                name: "LastName",
                table: "StudentSheets",
                newName: "ImagePath");
        }
    }
}
