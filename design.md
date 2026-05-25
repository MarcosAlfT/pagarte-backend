# Pagarte Backend Design

Pagarte is split into client identity, client profile management, a public payment API, an internal services project, and an async engine. AppHost is only local development orchestration; production should run each service independently.

## Runtime Shape

```text
ClientIdentityService  ---- issues OpenIddict access tokens ----+
                                                                 |
Clients.API       <---- validates issuer/audience ---------------+
Pagarte.API       <---- validates issuer/audience ---------------+
     |
     | gRPC
     v
Pagarte.Services  ---- SQL Server: PagarteDb
     |
     | publishes payment messages
     v
RabbitMQ
     |
     v
Pagarte.Engine
```

`AppHost` is local development orchestration only. Production should run each service independently and should provide production infrastructure such as RabbitMQ, SQL Server, logs, metrics, and secrets outside Aspire. AppHost does not provision RabbitMQ; it passes the configured external RabbitMQ connection string to the services that need it.

## General Architecture

Backend projects should follow the Application Use Case Pattern. Controllers and endpoints are thin HTTP adapters. Business workflows live in focused Application use cases. Domain entities own business rules and state transitions. Infrastructure and Persistence implement technical details behind Application interfaces.

Identity, Clients, and Pagarte are separate bounded contexts:

- `ClientIdentityService` owns client-user authentication and token issuance.
- `Clients.API` owns client profile data.
- `Pagarte.API` is the public payment HTTP boundary.
- `Pagarte.Services` owns payment persistence and synchronous card/payment operations.
- `Pagarte.Engine` owns asynchronous post-charge orchestration.

## Projects

### `ClientIdentityService`

`ClientIdentityService` is the refactored client-user identity API. The old `IdentityService` should move to this name and structure:

```text
ClientIdentityService
  ClientIdentity.Api
  ClientIdentity.Application
  ClientIdentity.Domain
  ClientIdentity.Infrastructure
  ClientIdentity.Persistence
```

Layer responsibilities:

- `ClientIdentity.Api`: controllers/endpoints only. It receives HTTP input, validates basic request shape, calls Application use cases, returns HTTP responses, and exposes the OpenAPI JSON document when the API runs.
- `ClientIdentity.Application`: use cases, DTOs, interfaces, authentication flow orchestration, password validation rules, and policy access through `IPolicyProvider`.
- `ClientIdentity.Domain`: entities, domain rules, aggregate behavior, and static factory methods.
- `ClientIdentity.Infrastructure`: `ConfigurationPolicyProvider`, `ConsoleNotificationPublisher`, password hashing, token helpers, OpenIddict-backed token services, and external technical services.
- `ClientIdentity.Persistence`: EF Core `ClientIdentityDbContext`, entity configurations, repositories, migrations, soft-delete query filters, and unit of work.

The Application layer must not depend directly on EF Core or ASP.NET `HttpContext`. It should define interfaces such as:

```text
IUserRepository
IRefreshTokenRepository
IEmailConfirmationTokenRepository
IPasswordResetTokenRepository
IPolicyProvider
IPasswordHasher
ITokenService
ITokenHasher
INotificationPublisher
IUnitOfWork
IClock
ICurrentActorProvider
```

`ICurrentActorProvider` exists to populate audit fields such as `CreatedBy`, `UpdatedBy`, `DeletedBy`, and `RevokedBy` without coupling Application or Domain code to ASP.NET. It can return values such as `system`, an authenticated user id, or another request actor.

#### Identity Scope

This project is for client identity only.

Do not implement:

- Roles or permissions.
- Internal users, admin users, or support users.
- RabbitMQ.
- Real email sending.
- Google, Microsoft, or other external login providers.
- Policy database tables.
- Policy APIs.
- Message-code/message-provider infrastructure.

Hardcoded user-facing messages are acceptable for the first version.

#### Identity Entities

All persistent identity entities use GUID ids and include:

```text
CreatedAt
CreatedBy
UpdatedAt
UpdatedBy
DeletedAt
DeletedBy
```

Soft delete is required across the identity project. Records should not be physically deleted unless explicitly requested.

`User` is the aggregate root. Recommended fields:

```text
Id
Email
NormalizedEmail
PasswordHash
Status
EmailConfirmedAt
LastLoginAt
FailedLoginAttempts
LastFailedLoginAt
LockoutEndAt
SecurityStamp
ConcurrencyStamp
CreatedAt
CreatedBy
UpdatedAt
UpdatedBy
DeletedAt
DeletedBy
```

User statuses:

```text
PendingEmailConfirmation
Active
Locked
Suspended
Deleted
```

New users must be created through `User.Create`. New users start as `PendingEmailConfirmation`, with no email confirmation date, zero failed login attempts, new security and concurrency stamps, and no deletion data. New users do not receive access or refresh tokens.

Deleted users cannot login or refresh tokens. Suspended users cannot login. Locked users cannot login until lockout expires and the account is made eligible again.

#### Identity Tokens

Use OpenIddict as the token authority. Keep `ITokenService` as the Application abstraction, and implement it in Infrastructure using OpenIddict/token helper code. This preserves clean architecture while keeping token issuance and validation on a proven OAuth/OpenID Connect library.

Keep audience-based API authorization. Identity issues access tokens for configured audiences such as:

```text
clients-api
pagarte-api
```

Each resource API validates the Identity issuer and only its own configured audience.

Refresh tokens use rotation. Store only refresh token hashes, never raw refresh tokens. A refresh token is active only when:

```text
RevokedAt is null
DeletedAt is null
ExpiresAt is in the future
```

Recommended refresh token fields:

```text
Id
UserId
TokenHash
ExpiresAt
RevokedAt
RevokedBy
ReplacedByTokenId
DeviceId
DeviceName
UserAgent
CreatedByIp
RevokedByIp
CreatedAt
CreatedBy
UpdatedAt
UpdatedBy
DeletedAt
DeletedBy
```

Email confirmation and password reset tokens also store only token hashes. Do not expose `UserId` in confirmation or reset URLs.

URL formats:

```text
https://your-client-app.com/confirm-email?token=<raw-token>
https://your-client-app.com/reset-password?token=<raw-token>
```

Token `IsActive` values are calculated, not stored. A confirmation or reset token is active only when:

```text
UsedAt is null
RevokedAt is null
DeletedAt is null
ExpiresAt is in the future
```

After a successful password reset:

1. Update `PasswordHash`.
2. Update `SecurityStamp`.
3. Mark the reset token as used.
4. Revoke all active refresh tokens.

Update `SecurityStamp` after sensitive account changes:

- Password changed.
- Password reset.
- Email changed.
- Account suspended.
- Account deleted.
- Logout all devices.

#### Identity Policies

Policies are configuration-based for now. Do not create policy database tables or policy APIs.

Policy access flow:

```text
ClientIdentity.Application
  -> IPolicyProvider
  -> ClientIdentity.Infrastructure
  -> ConfigurationPolicyProvider
  -> appsettings.json
```

Policy groups:

```text
PasswordPolicy
TokenPolicy
LockoutPolicy
EmailConfirmationPolicy
PasswordResetPolicy
PasskeyPolicy
```

Recommended values:

```text
PasswordPolicy:
  MinimumLength = 10
  MaximumLength = 128
  RequireUppercase = true
  RequireLowercase = true
  RequireNumber = true
  RequireSymbol = true
  RejectEmailAsPassword = true
  RejectCommonPasswords = true

TokenPolicy:
  AccessTokenMinutes = 15
  RefreshTokenDays = 14
  RefreshTokenRotationEnabled = true

LockoutPolicy:
  MaxFailedLoginAttempts = 5
  LockoutMinutes = 15
  FailedAttemptWindowMinutes = 30

EmailConfirmationPolicy:
  TokenExpirationHours = 24
  MaxResendAttemptsPerHour = 3

PasswordResetPolicy:
  TokenExpirationMinutes = 30
  MaxResetRequestsPerHour = 3

PasskeyPolicy:
  MaxPasskeysPerUser = 10
  RequirePasswordBeforeAddingPasskey = true
  ChallengeExpirationMinutes = 5
```

Password validation should use a builder pattern to build the password policy and rule/strategy classes to validate it. Return all relevant password validation errors when possible.

Possible password rules:

```text
MinimumLengthRule
MaximumLengthRule
UppercaseRule
LowercaseRule
NumberRule
SymbolRule
NotSameAsEmailRule
CommonPasswordRule
```

#### Identity Use Cases

Use focused use cases instead of one large authentication service.

First version use cases:

```text
RegisterUser
ConfirmEmail
LoginWithPassword
RefreshToken
Logout
ForgotPassword
ResetPassword
ChangePassword
```

Later use cases:

```text
RegisterPasskey
LoginWithPasskey
RemovePasskey
LogoutAllDevices
ResendEmailConfirmation
```

Password login should use a simple explicit flow, not a generic pipeline framework. Preferred shape:

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

Notification is abstracted through `INotificationPublisher`. The first implementation is `ConsoleNotificationPublisher`, which writes generated email confirmation and password reset URLs to the console. This keeps the use cases ready for RabbitMQ or real email delivery later without adding those features now.

#### Passkeys

Passkeys/WebAuthn/FIDO2 are later scope. Do not store biometric data. Never store fingerprint, face, or phone biometric information.

`UserPasskey` is a child entity of `User` and should be managed through:

```text
User.AddPasskey(...)
User.RemovePasskey(...)
```

Recommended fields:

```text
Id
UserId
CredentialId
PublicKey
DeviceName
SignCount
LastUsedAt
CreatedAt
CreatedBy
UpdatedAt
UpdatedBy
DeletedAt
DeletedBy
```

### `Pagarte.API`

Public HTTP entry point for Pagarte. It validates OpenIddict access tokens, extracts the authenticated client/user identity, and calls `Pagarte.Services` over gRPC.

It does not own payment persistence or call payment operators directly.

### `Pagarte.Services`

Internal gRPC service and owner of `PagarteDb`.

Responsibilities:

- Credit card registration and card persistence.
- Service catalog access.
- Payment quote/preview creation.
- Payment confirmation.
- Payment and quote persistence.
- Payment operator resolution for card registration and payment confirmation.
- Payment operator charge call during confirmation.
- Recording payment messages in the SQL outbox after a card charge succeeds.
- Publishing outbox messages to RabbitMQ with retries.

This project was previously named `Pagarte.Worker`. It is not only a background worker, so `Pagarte.Services` is the clearer name.

### `Pagarte.Engine`

Async payment orchestration after the payment operator charge has succeeded.

Responsibilities:

- Consume `payment.request`.
- Send payment to the company.
- Mark payment as completed when the company accepts it.
- Mark payment as `CompanyPaymentFailed` and trigger refund when the company rejects it.
- Consume refund requests and retry failed refunds.
- Publish alert messages when refund fails after maximum retries.

Engine updates payment status through SQL and does not reference `Pagarte.Services`, avoiding a circular dependency.

### `Pagarte.Messaging`

Shared Pagarte payment message contracts and RabbitMQ topology.

This project owns Pagarte-specific exchanges, queues, routing keys, and messages such as:

- `PaymentRequestMessage`
- `RefundRequestMessage`
- `AlertMessage`

Notification messaging should become a separate generic area later, for example:

```text
Notifications.Messaging
Notifications.Worker
```

That notification system should own its own topology and support multiple channels such as email, SMS, WhatsApp, push, and admin alerts.

### `Pagarte.Connections`

External adapters for payment operators and company integrations.

Payment operator selection is not hardcoded in the payment flow. `Pagarte.Services` resolves the active operator from the database, then uses `Pagarte.Connections` to obtain the correct adapter.

`PaymentOperator:Provider` in configuration is used for initial/local setup and seeding, not as a permanent business rule. No default provider should be silently assumed.

Current payment operator adapter shape:

```text
IPaymentOperatorAdapter
  RegisterCard
  Charge
  Refund

IPaymentOperatorAdapterFactory
  provider code -> adapter
```

Cards and payments store the operator provider that was used. This matters because card tokens and payment ids belong to the operator that created them and cannot safely be reused by another operator.

### `RabbitMQ`

Shared RabbitMQ connection, publisher, and base consumer infrastructure.

## Payment Quote Flow

Payment is now a two-step flow.

### 1. Quote / Preview

The client asks for a quote before paying.

```text
POST /api/payment/quote
```

Input:

```text
service id
currency
```

`Pagarte.Services` calculates and stores:

- service amount
- payment operator fee
- company fee
- Pagarte fee
- taxes, if any
- discounts, if any
- total amount
- currency
- expiration time
- quote id
- status: `Unpaid`

Quotes expire after 60 minutes. A quote is not a payment yet. It is persisted for traceability, statistics, and to ensure the confirmed payment uses the same values shown to the client.

### 2. Confirm / Pay

The client confirms a quote and chooses a card.

```text
POST /api/payment/confirm
```

Input:

```text
quote id
credit card id
```

`Pagarte.Services` then:

1. Validates the quote belongs to the client.
2. Validates the quote is still `Unpaid`.
3. Validates the quote is not expired.
4. Validates the card belongs to the client.
5. Creates a payment with status `Confirmed`.
6. Copies the quote details into payment details.
7. Updates payment to `ChargingCard`.
8. Uses the card's stored operator provider.
9. Calls the payment operator synchronously for the quote total.
10. If charge fails, marks payment `Failed`.
11. If charge succeeds, marks quote `Paid`.
12. Saves `OperatorProvider` and `OperatorPaymentId`.
13. Updates payment to `CardCharged`.
14. Records `PaymentRequestMessage` in the SQL outbox.
15. The outbox publisher sends the message to RabbitMQ with retries.

The credit card charge is intentionally synchronous. The client should know immediately whether the card was charged or rejected. The Engine only continues the asynchronous company/payment-delivery flow after a successful card charge.

## Payment Operators

Operators are card/payment processors, not companies.

Current model:

```text
PaymentOperator
  Code
  Name
  Scope: International | Local
  Priority
  IsActive
```

For now the system resolves the active `International` operator. This supports the current flow while leaving room for future routing by local/international transactions, country, currency, or priority.

When a card is registered:

```text
active international operator -> register card -> save OperatorProvider + OperatorCardToken
```

When a quote is confirmed:

```text
credit card -> OperatorProvider -> adapter factory -> charge synchronously
```

When a refund is needed:

```text
PaymentRequestMessage.OperatorProvider -> RefundRequestMessage.OperatorProvider -> adapter factory -> refund
```

This allows future operators such as `DLocal` or another processor without changing the payment confirmation flow.

## Companies

Companies are different from operators. A company is the recipient/provider behind the service being paid.

```text
Service -> Company -> Company connection strategy
```

The Engine should eventually resolve company connection details from `ServiceId`/`CompanyId`, not from hardcoded endpoint strings.

Expected company connection types:

```text
External
  Direct API
  Middleware API
  Web service

Internal
  Manual/physical information
  No external Pagarte.Connections call
```

External company integrations belong in `Pagarte.Connections`. Internal/manual payments should be resolved inside the application/engine workflow without pretending they are external API calls.

## Engine Flow

After the card is charged, Engine continues the async work.

```text
payment.request
    -> SendingPaymentToCompany
    -> call company
        -> Completed
        -> CompanyPaymentFailed
            -> Refunding
            -> refund.request
                -> Refunded
                -> RefundFailed
```

If the company accepts the payment, status becomes `Completed`.

If the company rejects the payment, status becomes `CompanyPaymentFailed`, then `Refunding`, and Engine publishes a refund request.

Refunds are retried up to three times. If all attempts fail, the payment becomes `RefundFailed` and an alert message is published.

Refund requests include the operator provider so the refund uses the same processor that charged the card.

## Payment Statuses

Payment statuses are:

```text
Confirmed
ChargingCard
CardCharged
SendingPaymentToCompany
Completed
CompanyPaymentFailed
Failed
Refunding
Refunded
RefundFailed
```

`Quoted` is intentionally not a payment status. Quote state belongs to `PaymentQuote`.

Quote statuses are:

```text
Unpaid
Paid
```

## RabbitMQ Topology

Current Pagarte queues:

```text
pagarte.payments exchange
  payment.request
  refund.request

pagarte.notifications exchange
  email.send
  alert.create
```

Current dead-letter queues:

```text
payment.request.dlq
refund.request.dlq
email.send.dlq
```

Both `Pagarte.Services` and `Pagarte.Engine` declare the Pagarte topology on startup, making startup idempotent.

`Pagarte.Services` publishes to RabbitMQ through a SQL outbox. Payment confirmation stores the post-charge `PaymentRequestMessage` in the same database save as the `CardCharged` payment state, then a hosted outbox publisher retries pending messages until RabbitMQ accepts them. This prevents a successful card charge from being lost when RabbitMQ is temporarily unavailable.

Production should provide RabbitMQ through environment variables, secrets, or platform configuration:

```text
RabbitMQ__Mode=FromEnvironment
ConnectionStrings__PagQueue=amqp://app_user:<password>@10.10.0.4:5672/%2F
```

Do not log RabbitMQ connection strings because they contain credentials.

## Local Development

AppHost wires local services:

- ClientIdentityService
- Clients API
- Pagarte API
- Pagarte Services
- Pagarte Engine

For local development, AppHost injects:

```text
AuthSettings__Authority
PagarteServices__GrpcUrl
RabbitMQ__Mode
ConnectionStrings__PagQueue
```

Production should configure these values through environment variables, secrets, or cloud service configuration.
