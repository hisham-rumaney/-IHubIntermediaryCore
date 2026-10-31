using IHub.Data.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IHub.Data.Migrations;

[DbContext(typeof(IHubDbContext))]
[Migration("20261031191556_SeedSampleData")]
public partial class SeedSampleData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO "Intermediaries" ("IntermediaryId", "CreatedAtUtc")
            VALUES
                ('BROKER-123', TIMESTAMPTZ '2026-10-01 00:00:00+00'),
                ('BROKER-456', TIMESTAMPTZ '2026-10-01 00:00:00+00'),
                ('BROKER-TEST', TIMESTAMPTZ '2026-10-01 00:00:00+00');

            INSERT INTO "Activities"
                ("Id", "SourceSystem", "ExternalActivityId", "IntermediaryId", "OccurredAt", "Amount", "Currency", "IngestedAt")
            VALUES
                ('00000000-0000-0000-0000-000000000001', 'SAMPLE', 'SAMPLE-0001', 'BROKER-123', TIMESTAMPTZ '2026-10-15 10:30:00+00', 1250.50, 'ZAR', TIMESTAMPTZ '2026-10-30 12:00:00+00'),
                ('00000000-0000-0000-0000-000000000002', 'SAMPLE', 'SAMPLE-0002', 'BROKER-123', TIMESTAMPTZ '2026-10-20 09:00:00+00', 250.00, 'ZAR', TIMESTAMPTZ '2026-10-30 12:05:00+00'),
                ('00000000-0000-0000-0000-000000000003', 'SAMPLE', 'SAMPLE-0003', 'BROKER-456', TIMESTAMPTZ '2026-10-18 16:00:00+00', 875.25, 'ZAR', TIMESTAMPTZ '2026-10-30 12:10:00+00');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "Activities"
            WHERE "SourceSystem" = 'SAMPLE'
              AND "ExternalActivityId" IN ('SAMPLE-0001', 'SAMPLE-0002', 'SAMPLE-0003');

            DELETE FROM "Intermediaries" AS intermediary
            WHERE intermediary."IntermediaryId" IN ('BROKER-123', 'BROKER-456', 'BROKER-TEST')
              AND NOT EXISTS (
                  SELECT 1
                  FROM "Activities" AS activity
                  WHERE activity."IntermediaryId" = intermediary."IntermediaryId");
            """);
    }
}