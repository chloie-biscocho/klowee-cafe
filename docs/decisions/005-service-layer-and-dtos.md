# 005 — Service layer and DTOs

## Context

Handover 01's `HealthController` talked to the `DbContext` directly. That is fine
for one endpoint and a trap at twenty: business rules end up spread across
controllers, they cannot be reused or tested without HTTP, and entities leak
straight onto the wire.

## Decision

Three layers, with a rule about what each may touch.

- **Controllers** (`Controllers/`) do HTTP only: route, bind, validate, return a
  status code and a DTO. A controller never references `KloweeDbContext`.
- **Services** (`Services/`) hold the business rules and are the only layer that
  talks to the `DbContext`. Every service has an interface and is registered
  scoped in `Program.cs`. Cross-cutting request identity arrives through
  `ICurrentUser`, backed by `IHttpContextAccessor`, so services never reach for
  `HttpContext` themselves.
- **Contracts** (`Contracts/`, grouped by feature) are the wire types. They are
  `record`s. Requests carry data-annotation attributes and are rejected with an
  automatic 400 by `[ApiController]` before a service ever sees them.

Entities never cross the HTTP boundary in either direction.

Errors are raised, not returned: services throw `NotFoundException`,
`ConflictException`, `ValidationFailedException` or `InvalidCredentialsException`
(`Common/ApiExceptions.cs`), and one `IExceptionHandler` turns them into
`ProblemDetails` with the right status. Controllers contain no error-shaping code.

## Consequences

- Business rules are testable without spinning up HTTP, and reusable from more
  than one endpoint.
- The API's shape is decided deliberately in `Contracts/` instead of falling out
  of the database schema. Renaming a column does not silently change the API, and
  `PasswordHash` cannot leak by accident.
- One error shape everywhere, decided in one file.
- Trade-off: more files, and a mapping step between entity and DTO for every
  endpoint. For a schema this size that is a small, mechanical cost.
- Trade-off: exceptions as control flow for expected outcomes (404, 409). It keeps
  services and controllers uncluttered; the alternative — a result type threaded
  through every signature — buys little at this scale.
