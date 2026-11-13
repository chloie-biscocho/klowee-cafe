namespace Klowee.Api.Common;

/// <summary>
/// What the API does to the database as it starts, by environment. One place,
/// so "production never migrates on startup" is a rule a test can hold.
///
///   Development  migrate, then seed owners and the sample data, then run
///                one-time data steps
///   Production   seed the two owners and run one-time data steps; migrations
///                run in the deploy workflow (docs/decisions/013-hosting-and-config.md)
///   anything else (Testing)  nothing; the test fixture builds its own database
///
/// One-time data steps (the loyalty carry-over, decision 020) each check a
/// marker row first, so after their first run they cost one query.
/// </summary>
public record StartupPlan(bool Migrate, bool SeedOwners, bool SeedSampleData, bool RunDataSteps)
{
    public static StartupPlan For(IHostEnvironment environment) =>
        environment.IsDevelopment() ? new StartupPlan(Migrate: true, SeedOwners: true, SeedSampleData: true, RunDataSteps: true)
        : environment.IsProduction() ? new StartupPlan(Migrate: false, SeedOwners: true, SeedSampleData: false, RunDataSteps: true)
        : new StartupPlan(Migrate: false, SeedOwners: false, SeedSampleData: false, RunDataSteps: false);
}
