namespace Klowee.Api.Common;

/// <summary>
/// What the API does to the database as it starts, by environment. One place,
/// so "production never migrates on startup" is a rule a test can hold.
///
///   Development  migrate, then seed owners and the sample data
///   Production   seed the two owners only; migrations run in the deploy
///                workflow (docs/decisions/013-hosting-and-config.md)
///   anything else (Testing)  nothing; the test fixture builds its own database
/// </summary>
public record StartupPlan(bool Migrate, bool SeedOwners, bool SeedSampleData)
{
    public static StartupPlan For(IHostEnvironment environment) =>
        environment.IsDevelopment() ? new StartupPlan(Migrate: true, SeedOwners: true, SeedSampleData: true)
        : environment.IsProduction() ? new StartupPlan(Migrate: false, SeedOwners: true, SeedSampleData: false)
        : new StartupPlan(Migrate: false, SeedOwners: false, SeedSampleData: false);
}
