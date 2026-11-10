using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sims.Shared.Migrations
{
    /// <inheritdoc />
    public partial class B1_InitialScanModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "scan");

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "scan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Scans",
                schema: "scan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Verdict = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scans_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "scan",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Components",
                schema: "scan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Purl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Components", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Components_Scans_ScanId",
                        column: x => x.ScanId,
                        principalSchema: "scan",
                        principalTable: "Scans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Findings",
                schema: "scan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VulnId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cve = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsKev = table.Column<bool>(type: "bit", nullable: false),
                    Epss = table.Column<double>(type: "float", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Findings_Components_ComponentId",
                        column: x => x.ComponentId,
                        principalSchema: "scan",
                        principalTable: "Components",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Findings_Scans_ScanId",
                        column: x => x.ScanId,
                        principalSchema: "scan",
                        principalTable: "Scans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Components_ScanId",
                schema: "scan",
                table: "Components",
                column: "ScanId");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_ComponentId",
                schema: "scan",
                table: "Findings",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_ScanId",
                schema: "scan",
                table: "Findings",
                column: "ScanId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TeamId",
                schema: "scan",
                table: "Projects",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Scans_ProjectId",
                schema: "scan",
                table: "Scans",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Findings",
                schema: "scan");

            migrationBuilder.DropTable(
                name: "Components",
                schema: "scan");

            migrationBuilder.DropTable(
                name: "Scans",
                schema: "scan");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "scan");
        }
    }
}
