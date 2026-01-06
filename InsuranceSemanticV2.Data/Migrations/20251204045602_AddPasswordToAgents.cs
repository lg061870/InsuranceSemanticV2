using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceSemanticV2.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordToAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "Agents",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Password",
                table: "Agents");
        }
    }
}
