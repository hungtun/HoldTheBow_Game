using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hobow_Server.Migrations
{
    /// <inheritdoc />
    public partial class updateEnemySpawnPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Y",
                table: "EnemySpawnPoints",
                newName: "SpawnRadius");

            migrationBuilder.RenameColumn(
                name: "X",
                table: "EnemySpawnPoints",
                newName: "CenterY");

            migrationBuilder.AddColumn<float>(
                name: "CenterX",
                table: "EnemySpawnPoints",
                type: "float",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CenterX",
                table: "EnemySpawnPoints");

            migrationBuilder.RenameColumn(
                name: "SpawnRadius",
                table: "EnemySpawnPoints",
                newName: "Y");

            migrationBuilder.RenameColumn(
                name: "CenterY",
                table: "EnemySpawnPoints",
                newName: "X");
        }
    }
}
