using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceSemanticV2.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAgentSessionsForAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StartedAt",
                table: "AgentSessions",
                newName: "LoginTime");

            migrationBuilder.RenameColumn(
                name: "EndedAt",
                table: "AgentSessions",
                newName: "LogoutTime");

            migrationBuilder.RenameColumn(
                name: "SessionId",
                table: "AgentSessions",
                newName: "AgentSessionId");

            migrationBuilder.AddColumn<string>(
                name: "ConnectionId",
                table: "AgentSessions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "AgentSessions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AgentSessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityTime",
                table: "AgentSessions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AgentSessions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "AgentSessions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_ConnectionId",
                table: "AgentSessions",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_IsActive",
                table: "AgentSessions",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentSessions_ConnectionId",
                table: "AgentSessions");

            migrationBuilder.DropIndex(
                name: "IX_AgentSessions_IsActive",
                table: "AgentSessions");

            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "AgentSessions");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "AgentSessions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AgentSessions");

            migrationBuilder.DropColumn(
                name: "LastActivityTime",
                table: "AgentSessions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AgentSessions");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "AgentSessions");

            migrationBuilder.RenameColumn(
                name: "LogoutTime",
                table: "AgentSessions",
                newName: "EndedAt");

            migrationBuilder.RenameColumn(
                name: "LoginTime",
                table: "AgentSessions",
                newName: "StartedAt");

            migrationBuilder.RenameColumn(
                name: "AgentSessionId",
                table: "AgentSessions",
                newName: "SessionId");
        }
    }
}
