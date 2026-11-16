using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V0_37_0_LamMoiTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ServiceKeyCreatedAt",
                table: "AgentEnvironment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceKeyCreatedByName",
                table: "AgentEnvironment",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceKeyHash",
                table: "AgentEnvironment",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceKeyHint",
                table: "AgentEnvironment",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentEnvironment_ServiceKeyHash",
                table: "AgentEnvironment",
                column: "ServiceKeyHash",
                unique: true,
                filter: "\"ServiceKeyHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentEnvironment_ServiceKeyHash",
                table: "AgentEnvironment");

            migrationBuilder.DropColumn(
                name: "ServiceKeyCreatedAt",
                table: "AgentEnvironment");

            migrationBuilder.DropColumn(
                name: "ServiceKeyCreatedByName",
                table: "AgentEnvironment");

            migrationBuilder.DropColumn(
                name: "ServiceKeyHash",
                table: "AgentEnvironment");

            migrationBuilder.DropColumn(
                name: "ServiceKeyHint",
                table: "AgentEnvironment");
        }
    }
}
