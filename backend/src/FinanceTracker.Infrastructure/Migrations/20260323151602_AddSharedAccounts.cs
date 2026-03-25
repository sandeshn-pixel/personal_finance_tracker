using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transactions_AccountId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_TransferAccountId",
                table: "transactions");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE transactions SET \"CreatedByUserId\" = \"UserId\", \"UpdatedByUserId\" = \"UserId\" WHERE \"CreatedByUserId\" = '00000000-0000-0000-0000-000000000000' OR \"UpdatedByUserId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql("ALTER TABLE transactions ALTER COLUMN \"CreatedByUserId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE transactions ALTER COLUMN \"UpdatedByUserId\" DROP DEFAULT;");

            migrationBuilder.CreateTable(
                name: "account_memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_memberships_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_account_memberships_users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_account_memberships_users_LastModifiedByUserId",
                        column: x => x.LastModifiedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_account_memberships_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_AccountId_DateUtc",
                table: "transactions",
                columns: new[] { "AccountId", "DateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CreatedByUserId",
                table: "transactions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_TransferAccountId_DateUtc",
                table: "transactions",
                columns: new[] { "TransferAccountId", "DateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_UpdatedByUserId",
                table: "transactions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_account_memberships_AccountId_UserId",
                table: "account_memberships",
                columns: new[] { "AccountId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_memberships_InvitedByUserId",
                table: "account_memberships",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_account_memberships_LastModifiedByUserId",
                table: "account_memberships",
                column: "LastModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_account_memberships_UserId",
                table: "account_memberships",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_users_CreatedByUserId",
                table: "transactions",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_users_UpdatedByUserId",
                table: "transactions",
                column: "UpdatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_users_CreatedByUserId",
                table: "transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_users_UpdatedByUserId",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "account_memberships");

            migrationBuilder.DropIndex(
                name: "IX_transactions_AccountId_DateUtc",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_CreatedByUserId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_TransferAccountId_DateUtc",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_UpdatedByUserId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "transactions");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_AccountId",
                table: "transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_TransferAccountId",
                table: "transactions",
                column: "TransferAccountId");
        }
    }
}
