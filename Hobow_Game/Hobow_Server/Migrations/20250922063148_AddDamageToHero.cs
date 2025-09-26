using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hobow_Server.Migrations
{
    /// <inheritdoc />
    public partial class AddDamageToHero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Damage",
                table: "Heroes",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Damage",
                table: "Heroes");
        }
    }
}
