using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FtsCredit.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    monthly_income = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    risk_level = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "credit_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    installments = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    product_type = table.Column<string>(type: "text", nullable: false),
                    approved_limit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_requests_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "risk_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    approved_limit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    risk_level = table.Column<string>(type: "text", nullable: false),
                    analysed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    engine_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_analyses", x => x.id);
                    table.ForeignKey(
                        name: "FK_risk_analyses_credit_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "credit_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_credit_requests_customer_id",
                table: "credit_requests",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_customers_document",
                table: "customers",
                column: "document",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_risk_analyses_request_id",
                table: "risk_analyses",
                column: "request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "risk_analyses");

            migrationBuilder.DropTable(
                name: "credit_requests");

            migrationBuilder.DropTable(
                name: "customers");
        }
    }
}
