using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingAccountInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_invites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_invites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_invites_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_account_invites_users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_account_invites_users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_account_invites_users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_invites_AcceptedByUserId",
                table: "account_invites",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_account_invites_AccountId_Email_Status",
                table: "account_invites",
                columns: new[] { "AccountId", "Email", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_account_invites_Email_Status_ExpiresUtc",
                table: "account_invites",
                columns: new[] { "Email", "Status", "ExpiresUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_account_invites_InvitedByUserId",
                table: "account_invites",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_account_invites_RevokedByUserId",
                table: "account_invites",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_account_invites_TokenHash",
                table: "account_invites",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_invites");
        }
    }
}
