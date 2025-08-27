using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hobow_Server.Migrations
{
    /// <inheritdoc />
    public partial class changeHeroAddname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Health",
                table: "Hero");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Hero",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Hero");

            migrationBuilder.AddColumn<float>(
                name: "Health",
                table: "Hero",
                type: "float",
                nullable: false,
                defaultValue: 0f);
        }
    }
}
