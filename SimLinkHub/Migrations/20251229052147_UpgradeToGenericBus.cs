using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimLinkHub.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeToGenericBus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Arduinos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FriendlyName = table.Column<string>(type: "TEXT", nullable: false),
                    I2CAddress = table.Column<byte>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arduinos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Configs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProfileName = table.Column<string>(type: "TEXT", nullable: false),
                    ComPort = table.Column<string>(type: "TEXT", nullable: false),
                    BaudRate = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoConnect = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ArduinoDeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    SimVarName = table.Column<string>(type: "TEXT", nullable: false),
                    Units = table.Column<string>(type: "TEXT", nullable: false),
                    TelemetryPrefix = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    DataIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    SignalPin = table.Column<int>(type: "INTEGER", nullable: false),
                    PinType = table.Column<string>(type: "TEXT", nullable: false),
                    InputMin = table.Column<double>(type: "REAL", nullable: false),
                    InputMax = table.Column<double>(type: "REAL", nullable: false),
                    OutputMin = table.Column<byte>(type: "INTEGER", nullable: false),
                    OutputMax = table.Column<byte>(type: "INTEGER", nullable: false),
                    IsInverted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Instruments_Arduinos_ArduinoDeviceId",
                        column: x => x.ArduinoDeviceId,
                        principalTable: "Arduinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_ArduinoDeviceId",
                table: "Instruments",
                column: "ArduinoDeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Configs");

            migrationBuilder.DropTable(
                name: "Instruments");

            migrationBuilder.DropTable(
                name: "Arduinos");
        }
    }
}
