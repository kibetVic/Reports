using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reports.Migrations
{
    /// <inheritdoc />
    public partial class createdby2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentVouchers_UserAccounts_CreatedById",
                table: "PaymentVouchers");

            migrationBuilder.DropIndex(
                name: "IX_PaymentVouchers_CreatedById",
                table: "PaymentVouchers");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "PaymentVouchers");

            migrationBuilder.AddColumn<int>(
                name: "UserAccountId",
                table: "PaymentVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_UserAccountId",
                table: "PaymentVouchers",
                column: "UserAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentVouchers_UserAccounts_UserAccountId",
                table: "PaymentVouchers",
                column: "UserAccountId",
                principalTable: "UserAccounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentVouchers_UserAccounts_UserAccountId",
                table: "PaymentVouchers");

            migrationBuilder.DropIndex(
                name: "IX_PaymentVouchers_UserAccountId",
                table: "PaymentVouchers");

            migrationBuilder.DropColumn(
                name: "UserAccountId",
                table: "PaymentVouchers");

            migrationBuilder.AddColumn<int>(
                name: "CreatedById",
                table: "PaymentVouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_CreatedById",
                table: "PaymentVouchers",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentVouchers_UserAccounts_CreatedById",
                table: "PaymentVouchers",
                column: "CreatedById",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
