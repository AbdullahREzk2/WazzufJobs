using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WazzufJobs.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCVExtractedText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "CVs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "CVs");
        }
    }
}
