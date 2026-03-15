using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalsAndRecurringTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "goals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TargetDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LinkedAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_goals_accounts_LinkedAccountId",
                        column: x => x.LinkedAccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goals_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_transaction_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRunDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AutoCreateTransaction = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_transaction_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recurring_transaction_rules_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_transaction_rules_accounts_TransferAccountId",
                        column: x => x.TransferAccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_transaction_rules_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_transaction_rules_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "goal_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GoalAmountAfterEntry = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goal_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_goal_entries_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goal_entries_goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_goal_entries_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_transaction_executions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecurringTransactionRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledForDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_transaction_executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recurring_transaction_executions_recurring_transaction_rule~",
                        column: x => x.RecurringTransactionRuleId,
                        principalTable: "recurring_transaction_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recurring_transaction_executions_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_goal_entries_AccountId",
                table: "goal_entries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_goal_entries_GoalId_OccurredAtUtc",
                table: "goal_entries",
                columns: new[] { "GoalId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_goal_entries_UserId_OccurredAtUtc",
                table: "goal_entries",
                columns: new[] { "UserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_goals_LinkedAccountId",
                table: "goals",
                column: "LinkedAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_goals_UserId_LinkedAccountId",
                table: "goals",
                columns: new[] { "UserId", "LinkedAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_goals_UserId_Status_TargetDateUtc",
                table: "goals",
                columns: new[] { "UserId", "Status", "TargetDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transaction_executions_RecurringTransactionRuleId~",
                table: "recurring_transaction_executions",
                columns: new[] { "RecurringTransactionRuleId", "ScheduledForDateUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transaction_executions_Status_ProcessedAtUtc",
                table: "recurring_transaction_executions",
                columns: new[] { "Status", "ProcessedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transaction_executions_TransactionId",
                table: "recurring_transaction_executions",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transaction_rules_AccountId",
                table: "recurring_transaction_rules",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transaction_rules_CategoryId",
                table: "recurring_transaction_rules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transaction_rules_TransferAccountId",
                table: "recurring_transaction_rules",
                column: "TransferAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transaction_rules_UserId_AccountId",
                table: "recurring_transaction_rules",
                columns: new[] { "UserId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transaction_rules_UserId_CategoryId",
                table: "recurring_transaction_rules",
                columns: new[] { "UserId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transaction_rules_UserId_Status_NextRunDateUtc",
                table: "recurring_transaction_rules",
                columns: new[] { "UserId", "Status", "NextRunDateUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "goal_entries");

            migrationBuilder.DropTable(
                name: "recurring_transaction_executions");

            migrationBuilder.DropTable(
                name: "goals");

            migrationBuilder.DropTable(
                name: "recurring_transaction_rules");
        }
    }
}
