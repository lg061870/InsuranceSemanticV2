using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceSemanticV2.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentTrackingToLeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastModifiedByAgentId",
                table: "Leads",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_LastModifiedByAgentId",
                table: "Leads",
                column: "LastModifiedByAgentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Agents_LastModifiedByAgentId",
                table: "Leads",
                column: "LastModifiedByAgentId",
                principalTable: "Agents",
                principalColumn: "AgentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Agents_LastModifiedByAgentId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_LastModifiedByAgentId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "LastModifiedByAgentId",
                table: "Leads");
        }
    }
}
