using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eirora.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class KeyTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KeyTransfers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiverPublicKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    SealedKey = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeyTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KeyTransfers_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KeyTransfers_AccountId",
                table: "KeyTransfers",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_KeyTransfers_ExpiresAt",
                table: "KeyTransfers",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KeyTransfers");
        }
    }
}
