# Repository Instructions

- Use Windows line endings (`CRLF`) for edited and newly created files.

## Application Use Case Pattern

For backend/API projects, prefer the Application Use Case Pattern to organize business workflows. This is also known as the Interactor Pattern or Application Service Pattern in Clean Architecture.

# General Design

ClientIdentityService / ClientIdentity.Api
  -> Responses

Clients / Clients.API
  -> Responses
  -> ClientIdentityService (runtime auth reference)

Pagarte.API
  -> Responses
  -> Pagarte.Contracts
  -> ClientIdentityService (runtime auth reference)
  -> Pagarte.Services (runtime gRPC calls)

Pagarte.Services
  -> RabbitMQ
  -> Responses
  -> Pagarte.Connections
  -> Pagarte.Contracts
  -> Pagarte.Messaging

Pagarte.Engine
  -> RabbitMQ
  -> Pagarte.Connections
  -> Pagarte.Messaging


RabbitMQ
  -> Used at runtime by Pagarte.Services and Pagarte.Engine

Pagarte.Connections
  -> Referenced by Pagarte.Services and Pagarte.Engine

Pagarte.Contracts
  -> Referenced by Pagarte.API and Pagarte.Services

Pagarte.Messaging
  -> Referenced by Pagarte.Services and Pagarte.Engine

Responses
  -> Referenced by IdentityService, Clients.API, Pagarte.API, and Pagarte.Services

# ClientIdentityService instructions

- Use this solution structure:
  - `ClientIdentity.Api`
  - `ClientIdentity.Application`
  - `ClientIdentity.Domain`
  - `ClientIdentity.Infrastructure`
  - `ClientIdentity.Persistence`
- This project is for client-user identity only.
- Do not add roles, permissions, internal users, admin users, support users
- Keep controllers/endpoints thin. Controllers should only receive HTTP input, validate basic request shape, call Application use cases, and return HTTP responses.
- Put use cases, DTOs, interfaces, authentication flow orchestration, and policy access through `IPolicyProvider` in `ClientIdentity.Application`.
- Put entities, domain rules, static factory methods, and aggregate behavior in `ClientIdentity.Domain`.
- Put configuration policy provider, console notification publisher, token helpers, password hashing, and other external technical services in `ClientIdentity.Infrastructure`.
- Put EF Core `ClientIdentityDbContext`, entity configurations, repositories, migrations, soft-delete filters, and unit of work implementation in `ClientIdentity.Persistence`.
- The Application layer must not depend on EF Core or ASP.NET `HttpContext`.
- Define Application interfaces such as `IUserRepository`, `IRefreshTokenRepository`, `IEmailConfirmationTokenRepository`, `IPasswordResetTokenRepository`, `IPolicyProvider`, `IPasswordHasher`, `ITokenService`, `ITokenHasher`, `INotificationPublisher`, `IUnitOfWork`, `IClock`, and `ICurrentActorProvider`.
- Use `ICurrentActorProvider` to populate audit fields such as `CreatedBy`, `UpdatedBy`, `DeletedBy`, `RevokedBy`, and `CreatedByIp` without coupling Application or Domain to ASP.NET.
- Use OpenIddict as the token authority. Keep `ITokenService` as an Application abstraction and implement it in Infrastructure using OpenIddict/token helper code.
- Keep audience-based API authorization. Identity should issue tokens for configured audiences such as `clients-api` and `pagarte-api`; each API should validate its own configured audience.
- Keep OpenAPI JSON generation available when the API runs by using `AddOpenApi()` and `MapOpenApi()` in `ClientIdentity.Api`.
- Do not issue access or refresh tokens to users that are not `Active`.
- Store only hashes for refresh tokens, email confirmation tokens, and password reset tokens. Never store raw token values.
- Do not expose `UserId` in confirmation or reset URLs.
- Do not store biometric data. Passkeys/WebAuthn/FIDO2 may be supported later, but never store fingerprint, face, or phone biometric information.
- Use soft delete across persistent entities. Do not physically delete records unless explicitly requested.
- All persistent entities should use GUID ids and include `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `DeletedAt`, and `DeletedBy`.
- `User` is the aggregate root. New users must be created through `User.Create`, start as `PendingEmailConfirmation`, and must not receive access or refresh tokens.
- User statuses are `PendingEmailConfirmation`, `Active`, `Locked`, `Suspended`, and `Deleted`.
- Deleted, suspended, and currently locked users cannot login. Locked users can login only after lockout expiry is handled.
- Refresh tokens must use rotation. A refresh token is active only when `RevokedAt` is null, `DeletedAt` is null, and `ExpiresAt` is in the future.
- Email confirmation and password reset token `IsActive` values are calculated, not stored.
- After a successful password reset, update `PasswordHash`, update `SecurityStamp`, mark the reset token as used, and revoke all active refresh tokens.
- Update `SecurityStamp` after sensitive account changes such as password changed, password reset, email changed, account suspended, account deleted, and logout all devices.
- Policies are configuration-based for now through `IPolicyProvider`; do not create policy database tables or APIs.
- Policy groups are `PasswordPolicy`, `TokenPolicy`, `LockoutPolicy`, `EmailConfirmationPolicy`, `PasswordResetPolicy`, and `PasskeyPolicy`.
- Use a builder pattern to build password policy and rule/strategy classes to validate passwords. Return all relevant password validation errors when possible.
- Prefer focused use cases instead of one large auth service. First version use cases are `RegisterUser`, `ConfirmEmail`, `LoginWithPassword`, `RefreshToken`, `Logout`, `ForgotPassword`, `ResetPassword`, and `ChangePassword`.
- Use a simple explicit login flow, not a generic pipeline framework. The preferred shape is:

```csharp
public sealed class LoginWithPasswordUseCase
{
    public async Task<LoginResponse> ExecuteAsync(LoginWithPasswordRequest request)
    {
        var user = await FindUserAsync(request.Email);
        await ValidatePasswordAsync(user, request.Password);
        await ValidateAccountStatusAsync(user);
        await TrackSuccessfulLoginAsync(user);
        var tokens = await IssueTokensAsync(user);
        await AuditLoginAsync(user);

        return tokens;
    }
}
```

- Use `INotificationPublisher` for notifications. The first implementation should be `ConsoleNotificationPublisher`, which writes generated email confirmation and password reset URLs to the console.

# Pagarte Projects instructions

- Do not hardcode payment operator selection in business flow. Use configuration only to seed or configure operators, then resolve the active operator through the application/database model.
- Keep credit card charge synchronous during payment confirmation. The async Engine flow starts after the card is charged.
- Store the operator provider used for card tokens and payments, because operator tokens are not portable across processors.


## GitHub Workflow Instructions

When making changes in this project, the AI agent must follow this GitHub workflow:

- Check the current branch before committing.
- Do not commit directly to `main` unless explicitly requested.
- Use small, focused commits.
- Use clear commit messages.
- Do not commit secrets, tokens, passwords, connection strings, `.env` files, or local configuration files.
- Before pushing, verify that `origin` points to the expected GitHub repository.
- Before committing, verify the repository Git identity is configured with the expected user name and email.
- Push only the branch related to the current work.