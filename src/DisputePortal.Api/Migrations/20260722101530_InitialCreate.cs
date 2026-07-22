using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DisputePortal.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    merchant_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    merchant_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "ZAR"),
                    transaction_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_transactions_users_customer_id",
                        column: x => x.customer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "disputes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    customer_description = table.Column<string>(type: "text", nullable: false),
                    extracted_fields_json = table.Column<string>(type: "jsonb", nullable: true),
                    assigned_to_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disputes", x => x.id);
                    table.ForeignKey(
                        name: "FK_disputes_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_disputes_users_assigned_to_id",
                        column: x => x.assigned_to_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_disputes_users_customer_id",
                        column: x => x.customer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispute_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispute_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispute_events_disputes_dispute_id",
                        column: x => x.dispute_id,
                        principalTable: "disputes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispute_events_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "resolutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    internal_notes = table.Column<string>(type: "text", nullable: false),
                    customer_summary = table.Column<string>(type: "text", nullable: true),
                    resolved_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resolutions", x => x.id);
                    table.ForeignKey(
                        name: "FK_resolutions_disputes_dispute_id",
                        column: x => x.dispute_id,
                        principalTable: "disputes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resolutions_users_resolved_by_id",
                        column: x => x.resolved_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispute_events_actor_id",
                table: "dispute_events",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_events_dispute_id",
                table: "dispute_events",
                column: "dispute_id");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_assigned_to_id",
                table: "disputes",
                column: "assigned_to_id");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_customer_id",
                table: "disputes",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_priority_status",
                table: "disputes",
                columns: new[] { "priority", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_disputes_reference",
                table: "disputes",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_disputes_status",
                table: "disputes",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_transaction_id",
                table: "disputes",
                column: "transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resolutions_dispute_id",
                table: "resolutions",
                column: "dispute_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resolutions_resolved_by_id",
                table: "resolutions",
                column: "resolved_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_customer_id",
                table: "transactions",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_reference",
                table: "transactions",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispute_events");

            migrationBuilder.DropTable(
                name: "resolutions");

            migrationBuilder.DropTable(
                name: "disputes");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
