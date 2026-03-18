using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reports.Migrations
{
    /// <inheritdoc />
    public partial class signatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "CEOSignature",
                table: "Certificates",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "TrainerSignature",
                table: "Certificates",
                type: "varbinary(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CEOSignature",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "TrainerSignature",
                table: "Certificates");
        }
    }
}
