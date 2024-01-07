using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuardRelay.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PowerSnapshots",
                columns: table => new
                {
                    Timestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    PowerLine1 = table.Column<double>(type: "REAL", nullable: false),
                    PowerLine2 = table.Column<double>(type: "REAL", nullable: false),
                    PowerLine3 = table.Column<double>(type: "REAL", nullable: false),
                    Duration = table.Column<long>(type: "INTEGER", nullable: false),
                    EnergyLine1 = table.Column<double>(type: "REAL", nullable: false),
                    EnergyLine2 = table.Column<double>(type: "REAL", nullable: false),
                    EnergyLine3 = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PowerSnapshots", x => x.Timestamp);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PowerSnapshots");
        }
    }
}
