using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Intermediaries",
                columns: table => new
                {
                    IntermediaryId = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Intermediaries", x => x.IntermediaryId);
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExternalActivityId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IntermediaryId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IngestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                    table.UniqueConstraint("UQ_Activities_SourceSystem_ExternalActivityId", x => new { x.SourceSystem, x.ExternalActivityId });
                    table.ForeignKey(
                        name: "FK_Activities_Intermediaries_IntermediaryId",
                        column: x => x.IntermediaryId,
                        principalTable: "Intermediaries",
                        principalColumn: "IntermediaryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_IntermediaryId_OccurredAt",
                table: "Activities",
                columns: new[] { "IntermediaryId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "Intermediaries");
        }
    }
}
