using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TradeBook.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    base_currency = table.Column<string>(type: "char(3)", nullable: false),
                    owner_subject = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "instruments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    instrument_type = table.Column<byte>(type: "tinyint", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    tick_size = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    lot_size = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instruments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "instrument_prices",
                columns: table => new
                {
                    instrument_id = table.Column<int>(type: "int", nullable: false),
                    last_price = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    as_of_utc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instrument_prices", x => x.instrument_id);
                    table.ForeignKey(
                        name: "FK_instrument_prices_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trades",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account_id = table.Column<int>(type: "int", nullable: false),
                    instrument_id = table.Column<int>(type: "int", nullable: false),
                    side = table.Column<byte>(type: "tinyint", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    price = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    executed_at_utc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    external_ref = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    captured_by_subject = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    captured_at_utc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trades", x => x.id);
                    table.ForeignKey(
                        name: "FK_trades_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trades_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account_id = table.Column<int>(type: "int", nullable: false),
                    instrument_id = table.Column<int>(type: "int", nullable: false),
                    net_quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    average_cost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    realised_pnl = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    last_trade_id = table.Column<long>(type: "bigint", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_positions", x => x.id);
                    table.ForeignKey(
                        name: "FK_positions_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_positions_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_positions_trades_last_trade_id",
                        column: x => x.last_trade_id,
                        principalTable: "trades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "accounts",
                columns: new[] { "id", "base_currency", "code", "created_at_utc", "is_active", "name", "owner_subject" },
                values: new object[,]
                {
                    { 1, "USD", "EQ-DESK-1", new DateTime(2026, 9, 15, 0, 0, 0, 0, DateTimeKind.Utc), true, "Equities Desk 1", "0f8fad5b-d9cb-469f-a165-70867728950e" },
                    { 2, "USD", "EQ-DESK-2", new DateTime(2026, 9, 15, 0, 0, 0, 0, DateTimeKind.Utc), true, "Equities Desk 2", "7c9e6679-7425-40de-944b-e07fc1f90ae7" }
                });

            migrationBuilder.InsertData(
                table: "instruments",
                columns: new[] { "id", "currency", "instrument_type", "is_active", "lot_size", "name", "symbol", "tick_size" },
                values: new object[,]
                {
                    { 1, "USD", (byte)1, true, 1, "Apple Inc.", "AAPL", 0.01m },
                    { 2, "USD", (byte)1, true, 1, "Microsoft Corp.", "MSFT", 0.01m },
                    { 3, "USD", (byte)1, true, 1, "Tesla Inc.", "TSLA", 0.01m },
                    { 4, "USD", (byte)1, true, 1, "NVIDIA Corp.", "NVDA", 0.01m }
                });

            migrationBuilder.InsertData(
                table: "instrument_prices",
                columns: new[] { "instrument_id", "as_of_utc", "last_price" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 9, 15, 0, 0, 0, 0, DateTimeKind.Utc), 11.02m },
                    { 2, new DateTime(2026, 9, 15, 0, 0, 0, 0, DateTimeKind.Utc), 8.50m },
                    { 3, new DateTime(2026, 9, 15, 0, 0, 0, 0, DateTimeKind.Utc), 42.10m },
                    { 4, new DateTime(2026, 9, 15, 0, 0, 0, 0, DateTimeKind.Utc), 27.35m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_code",
                table: "accounts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_owner_subject",
                table: "accounts",
                column: "owner_subject");

            migrationBuilder.CreateIndex(
                name: "IX_instruments_symbol",
                table: "instruments",
                column: "symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_positions_account_id_instrument_id",
                table: "positions",
                columns: new[] { "account_id", "instrument_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_positions_instrument_id",
                table: "positions",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "IX_positions_last_trade_id",
                table: "positions",
                column: "last_trade_id");

            migrationBuilder.CreateIndex(
                name: "IX_trades_account_id",
                table: "trades",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_trades_executed_at_utc",
                table: "trades",
                column: "executed_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_trades_external_ref",
                table: "trades",
                column: "external_ref",
                unique: true,
                filter: "[external_ref] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_trades_instrument_id",
                table: "trades",
                column: "instrument_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "instrument_prices");

            migrationBuilder.DropTable(
                name: "positions");

            migrationBuilder.DropTable(
                name: "trades");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "instruments");
        }
    }
}
