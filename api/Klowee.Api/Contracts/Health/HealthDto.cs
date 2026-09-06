namespace Klowee.Api.Contracts.Health;

public record HealthDto(string Status, string Database, DateTimeOffset Utc);
