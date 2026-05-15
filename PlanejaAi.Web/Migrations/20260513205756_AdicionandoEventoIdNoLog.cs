using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanejaAi.Web.Migrations
{
    /// <inheritdoc />
    public partial class AdicionandoEventoIdNoLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "evento_id",
                table: "logs",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "evento_id",
                table: "logs");
        }
    }
}
