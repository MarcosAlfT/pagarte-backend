# ClientIdentityService Design

`ClientIdentityService` is the client-user identity bounded context. It issues OpenIddict tokens for the public APIs and owns the user lifecycle, authentication flow, and token policy rules.

## Scope

This project is for client-user identity only.

Do not add:

- Roles or permissions.
- Internal users, admin users, or support users.
- External login providers.
- Policy tables or policy APIs.
- Biometric storage.

## Runtime Boundary

```text
ClientIdentity.Api
  -> HTTP + OpenAPI
  -> OpenIddict token issuance and validation

ClientIdentity.Application
  -> use cases, DTOs, and application interfaces

ClientIdentity.Domain
  -> entities, rules, and aggregate behavior

ClientIdentity.Infrastructure
  -> token helpers, password hashing, notification publisher, policy provider

ClientIdentity.Persistence
  -> DbContext, EF configs, repositories, migrations, unit of work
```

## Responsibilities

- Register users.
- Confirm email.
- Login with password.
- Refresh and revoke tokens.
- Support password reset and password change.
- Enforce configuration-based password, token, lockout, email confirmation, and password reset policies.

## Domain Notes

- `User` is the aggregate root.
- New users start as `PendingEmailConfirmation`.
- Tokens are issued only to `Active` users.
- Refresh, email confirmation, and password reset tokens store hashes only.
- Soft delete applies to persistent identity entities.
- Audit fields use `ICurrentActorProvider` so Application and Domain do not depend on ASP.NET.

## Integrations

- `ClientIdentity.Api` is the token authority.
- `Clients.API` validates the `clients-api` audience.
- `Pagarte.API` validates the `pagarte-api` audience.
- `ConsoleNotificationPublisher` is the first notification implementation.
