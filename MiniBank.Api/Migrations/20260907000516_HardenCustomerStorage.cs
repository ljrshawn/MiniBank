using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniBank.Api.Migrations
{
    /// <inheritdoc />
    public partial class HardenCustomerStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check legacy duplicates before SQLite begins rebuilding either table.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "__Upgrade_Customers_Email"
                    ON "Customers" (trim("Email") COLLATE NOCASE);
                CREATE UNIQUE INDEX "__Upgrade_Accounts_AccountNumber"
                    ON "Accounts" ("AccountNumber");
                DROP INDEX "__Upgrade_Customers_Email";
                DROP INDEX "__Upgrade_Accounts_AccountNumber";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Customers_CustomerId",
                table: "Accounts");

            migrationBuilder.RenameColumn(
                name: "PassWord",
                table: "Customers",
                newName: "PasswordHash");

            // EF's synchronous/asynchronous seeding callbacks hash these marked values.
            migrationBuilder.Sql("""
                UPDATE "Customers"
                SET "PasswordHash" = 'legacy:' || "PasswordHash",
                    "Email" = trim("Email");
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Customers",
                type: "TEXT",
                maxLength: 254,
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountNumber",
                table: "Accounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Customers_CustomerId",
                table: "Accounts",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Password hashing cannot be reversed. Restore a pre-upgrade backup to return to the original schema."
            );
        }
    }
}
