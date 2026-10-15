using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace data.Migrations
{
    /// <inheritdoc />
    public partial class MultiSectionTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OptionCount",
                table: "ExamTemplates");

            migrationBuilder.RenameColumn(
                name: "QuestionCount",
                table: "ExamTemplates",
                newName: "StudentNoDigits");

            migrationBuilder.AddColumn<string>(
                name: "LayoutJson",
                table: "ExamTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "ExamTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ExamSectionId",
                table: "AnswerKeys",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ExamSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamTemplateId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    OptionCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamSections_ExamTemplates_ExamTemplateId",
                        column: x => x.ExamTemplateId,
                        principalTable: "ExamTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamSections_ExamTemplateId",
                table: "ExamSections",
                column: "ExamTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamSections");

            migrationBuilder.DropColumn(
                name: "LayoutJson",
                table: "ExamTemplates");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "ExamTemplates");

            migrationBuilder.DropColumn(
                name: "ExamSectionId",
                table: "AnswerKeys");

            migrationBuilder.RenameColumn(
                name: "StudentNoDigits",
                table: "ExamTemplates",
                newName: "QuestionCount");

            migrationBuilder.AddColumn<int>(
                name: "OptionCount",
                table: "ExamTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
