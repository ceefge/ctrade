using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTrader.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 2,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 4,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 5,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 6,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 7,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 8,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 9,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 10,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 11,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 12,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 13,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 14,
                column: "UpdatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.CreateIndex(
                name: "IX_Trades_ExecutedAt",
                table: "Trades",
                column: "ExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_Symbol",
                table: "Trades",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_RegimeAnalyses_AnalyzedAt",
                table: "RegimeAnalyses",
                column: "AnalyzedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_PublishedAt",
                table: "NewsArticles",
                column: "PublishedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trades_ExecutedAt",
                table: "Trades");

            migrationBuilder.DropIndex(
                name: "IX_Trades_Symbol",
                table: "Trades");

            migrationBuilder.DropIndex(
                name: "IX_RegimeAnalyses_AnalyzedAt",
                table: "RegimeAnalyses");

            migrationBuilder.DropIndex(
                name: "IX_NewsArticles_PublishedAt",
                table: "NewsArticles");

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3724));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 2,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3730));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3731));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 4,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3733));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 5,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3734));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 6,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3737));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 7,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3738));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 8,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3739));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 9,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3740));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 10,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3742));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 11,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3743));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 12,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3744));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 13,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3745));

            migrationBuilder.UpdateData(
                table: "Parameters",
                keyColumn: "Id",
                keyValue: 14,
                column: "UpdatedAt",
                value: new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3747));
        }
    }
}
