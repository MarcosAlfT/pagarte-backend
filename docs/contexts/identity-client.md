# Identity.Client

## Purpose

`Identity.Client` is the client-user identity bounded context. It owns the user
lifecycle, authentication workflows, and token policy.

## Owns

- Client-user registration and account state.
- Email confirmation.
- Password login, reset, and change.
- Access-token issuance through OpenIddict.
- Refresh-token issuance, hashing, rotation, and revocation.
- Password, token, lockout, email-confirmation, and password-reset policies.

## Does Not Own

- Client profile data.
- Roles or permissions.
- Internal, administrator, or support users.
- External login providers.
- Payment authorization or business workflows.
- Biometric data storage.

## Runtime Boundary

```text
Identity.Client.Api
  -> HTTP and OpenAPI
  -> OpenIddict token endpoints

Identity.Client.Application
  -> use cases, DTOs, policies, and abstractions

Identity.Client.Domain
  -> users, tokens, passkeys, and domain rules

Identity.Client.Infrastructure
  -> password hashing, token helpers, policies, and notifications

Identity.Client.Persistence
  -> EF Core, SQL Server, repositories, migrations, and unit of work
```

## Main Use Cases

- Register a user.
- Confirm an email address.
- Log in with a password.
- Refresh and revoke tokens.
- Request and complete a password reset.
- Change a password.

## Domain Rules

- `User` is the aggregate root.
- New users start in `PendingEmailConfirmation`.
- Tokens are issued only to active users.
- Refresh, email-confirmation, and password-reset tokens store hashes only.
- Persistent identity entities support soft deletion.
- Application and Domain use `ICurrentActorProvider` instead of depending on
  ASP.NET request state.

## Data Ownership

`Identity.Client` owns `IdentityClientDb` and is the only context that should
write identity-user and identity-token state.

## Integrations

- Issues tokens accepted by `ClientProfiles.Api` for the
  `client-profiles-api` audience.
- Issues tokens accepted by `Payments.Api` for the `payments-api` audience.
- Uses `ConsoleNotificationPublisher` as the initial notification
  implementation.
- Uses `Utilities.Responses` at the public HTTP boundary.

## Change Guidance

Add business workflows as Application use cases and keep controllers focused on
transport validation and response mapping. Keep token secrets and password
material out of logs, responses, and committed configuration.
