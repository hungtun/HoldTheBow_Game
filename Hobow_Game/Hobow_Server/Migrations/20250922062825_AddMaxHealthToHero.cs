using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hobow_Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxHealthToHero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxHealth",
                table: "Heroes",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxHealth",
                table: "Heroes");
        }
    }
}
