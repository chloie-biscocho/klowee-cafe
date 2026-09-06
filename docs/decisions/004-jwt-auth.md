# 004 — The API issues its own JWTs

## Context

Two co-owners need to sign in to `admin/`; the public `client/` site stays
anonymous. Three options were on the table: ASP.NET Core Identity, Supabase Auth,
or hand-rolled tokens issued by this API.

ASP.NET Core Identity brings a large schema (roles, claims, logins, tokens,
two-factor, lockout) and a UI story we do not need for two accounts that are
provisioned by hand. Supabase Auth would put identity in a second system, split
the `users` table from the rest of the schema, and couple the API's auth story to
the database host we happen to use.

## Decision

The API issues and validates its own JSON Web Tokens.

- **Issuing:** `POST /api/auth/login` checks the password and returns a signed
  HS256 token (`Services/JwtTokenService.cs`). Claims: `sub` (user id), `email`,
  `name`, `role`. Issuer and audience are both `klowee-cafe`.
- **Validating:** `Microsoft.AspNetCore.Authentication.JwtBearer` verifies the
  signature, issuer, audience and expiry on every request. Inbound claim mapping
  is turned off so `sub` stays `sub` instead of being rewritten to a legacy
  WS-Federation URI.
- **Lifetime:** 8 hours, no refresh tokens. A shift at the cart is shorter than
  that, and refresh tokens are meaningful complexity (rotation, revocation,
  storage) for a two-person admin panel.
- **Passwords:** hashed with `PasswordHasher<User>` from
  `Microsoft.Extensions.Identity.Core` — the standalone hasher (PBKDF2-HMAC-SHA512,
  100k iterations, per-password random salt), not the full Identity stack. That
  type ships in the ASP.NET Core shared framework, so it needs no package
  reference.
- **Accounts:** created by the seeder from `dotnet user-secrets`
  (`Seed:Owners:0:*`, `Seed:Owners:1:*`), never through an endpoint. There is no
  registration, no password reset, and no role beyond `Owner`.
- **Signing key:** `Jwt:Key`, at least 32 characters, from user-secrets in
  development and an environment variable in production. The app refuses to start
  without it.
- **Closed by default:** a global authorization *fallback policy* requires an
  authenticated user for every endpoint that does not say otherwise. Public
  endpoints opt out with `[AllowAnonymous]`.

## Consequences

- One identity system, one `users` table, no second vendor in the login path.
- The server can trust a token without a database round-trip, because it verifies
  a signature it produced itself.
- Trade-off: a token cannot be revoked before it expires. With an 8-hour lifetime
  and two known users, the exposure is small; if that changes, the answer is
  short-lived access tokens plus refresh tokens, not a session table.
- Trade-off: we own the auth code. It is small and standard, but it is ours to
  keep correct.
- Adding a second role later means adding policies; the fallback policy only
  requires *authentication*, not any particular role.
