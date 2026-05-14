namespace TaxReader.UnitTests.Auth;

/// <summary>
/// AUTH-01: Migration smoke. EF InMemory provider does NOT run Postgres DDL,
/// so real verification of the AddRefreshTokensTable_DropLegacyRefreshTokenColumns
/// migration shape against an actual Postgres 17 instance is deferred to Phase 7
/// QA-01 (Testcontainers). This file exists as a placeholder so the planning
/// trail is intact.
/// </summary>
public class MigrationTests
{
    [Fact(Skip = "Deferred to Phase 7 QA-01 (Testcontainers) — EF InMemory cannot run Postgres DDL")]
    public void Add_RefreshTokens_AndDropLegacy_SmokeTest()
    {
        // Placeholder. Real Postgres-backed migration verification lives in Phase 7.
    }
}
