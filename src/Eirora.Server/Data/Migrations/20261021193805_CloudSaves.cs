using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eirora.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class CloudSaves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CloudSaves",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    KeyEpoch = table.Column<int>(type: "integer", nullable: false),
                    World = table.Column<byte[]>(type: "bytea", nullable: false),
                    Contributions = table.Column<byte[]>(type: "bytea", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloudSaves", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_CloudSaves_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LockedContributions",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyEpoch = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Contributions = table.Column<byte[]>(type: "bytea", nullable: false),
                    LockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LockedContributions", x => new { x.AccountId, x.KeyEpoch });
                    table.ForeignKey(
                        name: "FK_LockedContributions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LockedContributions_LockedAt",
                table: "LockedContributions",
                column: "LockedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CloudSaves");

            migrationBuilder.DropTable(
                name: "LockedContributions");
        }
    }
}
