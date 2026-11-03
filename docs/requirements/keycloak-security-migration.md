# Requirement: Replace Client Security with Keycloak

## Requirement Status

| Field | Value |
| --- | --- |
| Status | Backend implemented and locally integrated; release acceptance pending |
| Priority | High, before production onboarding |
| Scope | Client authentication, account security, API token validation, and privileged roles |
| Production status | Requester confirmed on 2026-10-18 that Pagarte is not in production |
| Migration assumption | No production identity, profile, card, quote, or payment data exists; verify this before destructive cutover |
| Baseline authority | `Identity.Client.Api` with OpenIddict |
| Target authority | Keycloak using OpenID Connect and OAuth 2.0 |

This document defines the target and acceptance criteria. The backend now has
Keycloak validation, role policies, profile identity mapping, and local AppHost
resources. [Architecture](../architecture.md) and
[Authentication Flow](../flows/authentication.md) describe the current branch.
The requester confirmed that no browser or native client applications exist
yet. Local container and API integration checks passed on 2026-10-19. Release
acceptance remains open for the data inventory, account-lifecycle checks,
and retirement listed below.

| Implementation area | Current branch state |
| --- | --- |
| Local Keycloak and PostgreSQL | AppHost launched both pinned containers, imported the realm, and served discovery at the configured issuer; a separate PostgreSQL-backed restart preserved users and signing keys |
| API validation and policies | Real PKCE tokens passed the intended audiences and policies; wrong-audience and anonymous requests were rejected; client and administrator routes remained separate |
| Pagarte client mapping | The EF migration applied on isolated SQL Express; profile creation, protected lookup, retry, concurrent creation, and deletion behavior were exercised |
| Card and quote ownership | AppHost card listing and quote creation used the same resolved internal GUID; legacy stored records still require deliberate reset or conversion |
| Existing realm update | The pinned reconciliation script applied and restored a test lifespan change while preserving the existing user |
| Client applications and retirement | The requester confirmed no browser or native client apps exist yet; synthetic browser and native PKCE flows passed; old Identity.Client source remains until release acceptance and data inventory are verified |

### Local validation on 2026-10-19

- `dotnet test pagarte-backend.sln -p:NuGetAudit=false` passed 21 tests;
  AppHost and its referenced projects built with no warnings or errors.
- Keycloak 26.8.1 imported the versioned realm on PostgreSQL 17.6. Browser
  and native public clients completed synthetic Authorization Code + S256 PKCE
  logins. Tokens had the expected issuer, type, verified-email claim, role,
  and requested audience; Direct Access Grants returned HTTP 400.
- A new verified client created one profile and mapping on SQL Express.
  Concurrent creation produced one client row. The resolver rejected requests
  without its workload key and returned only the GUID for a trusted request.
  Deleting a profile made its mapping unavailable without assigning a new GUID.
- AppHost served all three public APIs. A mapped client listed cards and
  created a quote whose `ClientId` matched the ClientProfiles GUID. Maintenance
  rejected anonymous and client tokens; an admin-only token passed its policy
  but was denied access to client quotes.
- Reconciliation changed a test realm's access-token lifetime from 900 to 840
  seconds and back, preserving the user. A PostgreSQL-backed Keycloak restart
  preserved that user and its signing key IDs.

Release acceptance still requires an inventory and deliberate handling of any
nonproduction legacy subject-owned data, configured SMTP and tested email
verification/recovery/logout flows, remaining card/payment execution checks
with their external dependencies, and retirement of the old Identity.Client
source and database. No actual browser or native application can be tested
until one is built.

## Problem Statement

Pagarte currently owns password authentication, registration, email
confirmation, password recovery, refresh-token rotation, lockout policy, and
OpenIddict token issuance inside `Identity.Client`. The public APIs validate
those tokens. Profile ownership uses a separate `Client.Id`; card, quote, and
payment ownership currently uses the identity user's token subject.

The current boundary requires Pagarte to build and maintain security features
that a dedicated identity provider can own. It also does not yet provide the
administrator and support identity model required by operational
transactions. The planned replacement assumes that no production data exists;
that assumption must be checked before removing either authority or data.

## Objective

Keycloak shall become the sole identity provider and authorization server for
Pagarte users. Pagarte services shall consume standards-based access tokens,
enforce domain authorization, and retain ownership of client profiles and
payment data without storing user passwords or refresh tokens.

The change shall keep authentication infrastructure replaceable. Domain
records must not depend directly on a Keycloak-specific identifier format.

## Baseline Before This Migration

- `Identity.Client.Api` exposes registration, email-confirmation, password,
  login, refresh, and logout operations.
- `Identity.Client` stores users, password hashes, one-time token hashes,
  refresh tokens, account state, and passkey records in `IdentityClientDb`.
- `ClientProfiles.Api`, `Payments.Api`, and `PayableServices.Api` use
  OpenIddict validation with a configured issuer and audience.
- `Identity.Client` issues `sub` as the identity user's GUID. ClientProfiles
  stores that value as `Client.UserId` and independently creates a GUID
  `Client.Id` for the profile. There is no database uniqueness constraint on
  `Client.UserId` today.
- Payments.Api and PayableServices.Api pass the `sub` string as `ClientId`;
  stored cards, quotes, and payments use that string for ownership. These are
  not the ClientProfiles `Client.Id` GUID.
- The Aspire AppHost starts `Identity.Client.Api` and supplies its endpoint as
  the authentication authority to the public APIs.
- The workload API key used between PayableServices and
  PaymentSwitch.Processor is independent from client authentication.

## Target Boundary

```text
Client application
  -> Authorization Code + PKCE
  -> Keycloak
      -> registration, email verification, recovery; later MFA/passkeys
      -> credentials, sessions, refresh tokens, roles
      -> signed access token

Client application with access token
  -> ClientProfiles.Api creates a profile and identity mapping
  -> ClientProfiles.Api resolves its own client requests locally
  -> Payments.Api / PayableServices.Api
      -> validate token and endpoint policy
      -> resolve (issuer, subject) through a protected ClientProfiles boundary
      -> pass the internal Pagarte ClientId to owned use cases and Processor

Aspire AppHost for local development
  -> PostgreSQL container dedicated to Keycloak
  -> Keycloak container
  -> Pagarte APIs
```

Keycloak shall own authentication and identity-provider concerns. Pagarte
shall continue to own client profiles, stored-card ownership, quotes, payments,
and all payment business authorization.

In this document, `sub` means the Keycloak user subject, `ClientId` means the
Pagarte-owned client GUID, and an OIDC client means a browser or native
application registered in Keycloak. These identifiers are never interchangeable.

## Functional Requirements

### `KC-001` Keycloak Authority

- Keycloak shall be the only issuer of interactive user access tokens accepted
  by Pagarte public APIs.
- Each environment shall configure an explicit trusted issuer URL.
- Realm, client, scope, audience, role, and authentication-flow configuration
  shall be reproducible through version-controlled deployment configuration.
- The deployment shall pin a tested Keycloak version. Startup realm import may
  bootstrap a new local realm; versioned changes to an existing realm shall be
  applied through a repeatable, reviewed configuration process.
- Secrets and environment-specific credentials shall not be stored in the
  realm configuration committed to source control.

### `KC-002` Browser and Native Authentication

- Interactive clients shall use the OpenID Connect Authorization Code flow
  with Proof Key for Code Exchange (PKCE).
- Browser and native applications shall have separate public-client
  registrations with exact environment-specific redirect and post-logout URIs.
  They shall not contain a client secret.
- Pagarte applications shall redirect users to Keycloak for authentication and
  shall not collect or proxy user passwords.
- The Resource Owner Password Credentials flow, called Direct Access Grants in
  Keycloak, shall be disabled.
- Clients shall validate authorization response state and nonce according to
  the OpenID Connect client library in use.

### `KC-003` Account Lifecycle

Keycloak shall provide:

- self-registration for client users;
- unique email-based identity according to the configured realm policy;
- email verification before protected Pagarte transactions are permitted;
- forgotten-password and credential-reset flows;
- authenticated password changes;
- logout and session termination;
- brute-force protection and configurable lockout behavior;
- a path for later MFA and passkey enrollment without adding credential
  storage to Pagarte databases.

SMTP settings, message templates, redirect URIs, and required actions shall be
configured per environment. The realm shall require email verification. Access
tokens used for Pagarte operations shall carry `email_verified`; protected
profile, card, quote, payment, and operational policies shall require it to be
`true`. An unverified user can continue the verification flow in Keycloak but
cannot perform those Pagarte transactions. MFA and passkey enrollment are not
release criteria for this migration and remain tracked by `AUTH-12`.

### `KC-004` Token and Session Ownership

- Keycloak shall own access-token issuance, refresh tokens, browser sessions,
  revocation, signing keys, and key rotation.
- Pagarte APIs shall not persist access tokens or refresh tokens.
- Production access-token lifetime shall be at most 15 minutes. Refresh-token
  and session lifetimes shall be explicit versioned realm or client policy,
  not application constants.
- Signing keys shall persist across service restarts and be managed separately
  for each environment.
- A restart or redeployment shall not silently invalidate all active sessions
  because an ephemeral signing key was regenerated.

### `KC-005` API Audiences

Keycloak access tokens shall carry only the audiences needed by the requesting
OIDC client. The initial protected audiences are:

| Audience | Consumer |
| --- | --- |
| `client-profiles-api` | `ClientProfiles.Api` |
| `payments-api` | `Payments.Api` |
| `payable-services-api` | `PayableServices.Api` |

Each API shall reject a token with an invalid issuer, invalid signature,
expired lifetime, or missing required audience. Receiving a valid token for a
different Pagarte API shall not grant access. Version-controlled Keycloak
client scopes and audience mappers shall let browser and native applications
request the audiences they need. APIs shall reject ID tokens as API credentials.

### `KC-006` Standard API Validation

- Public APIs shall use standard ASP.NET Core JWT bearer validation against
  Keycloak's OpenID Connect discovery metadata and JSON Web Key Set.
- Issuer and audience configuration shall come from environment configuration.
- Production validation shall require HTTPS metadata and must fail closed when
  metadata, signatures, or required claims cannot be validated.
- Each protected user-facing endpoint shall require a nonempty `sub`,
  `email_verified=true`, and the application role specified in `KC-008`, in
  addition to issuer, signature, lifetime, and audience validation. Internal
  workload endpoints shall use their own scoped authentication policies.
- Controllers shall remain thin. Authentication and current-actor extraction
  belong in transport infrastructure; domain ownership checks remain in
  Application use cases and domain rules.

### `KC-007` Stable Pagarte Client Identity

- Keycloak's `(iss, sub)` pair shall be treated as an external identity key,
  not as the Pagarte domain `ClientId` itself.
- ClientProfiles shall own a persisted, active mapping from `(iss, sub)` to its
  GUID `Client.Id`. The initial model is one identity per client and one client
  per identity. Database uniqueness constraints on both `(iss, sub)` and
  `ClientId` shall enforce this under concurrent requests; email is not a key.
- A verified user with the `client` role creates a person or organization
  profile through ClientProfiles. Profile and mapping creation shall commit in
  one transaction. Retried or concurrent creation shall return the existing
  mapping or a safe conflict, never create a second client. Deleting a profile
  shall deactivate its mapping; a deleted profile shall not silently acquire a
  new `ClientId` on the next request.
- ClientProfiles shall resolve its own client requests from the validated
  token. Payments.Api and PayableServices.Api shall call a read-only internal
  ClientProfiles identity-resolution endpoint using a dedicated, scoped
  workload credential over TLS. Only those API workloads may call it. Each
  caller shall derive `(iss, sub)` from its validated user token, not from a
  client-supplied identifier. The endpoint shall return an active `ClientId`
  or a missing-mapping result and shall not expose profile data.
- ClientProfiles owns the mapping; other APIs shall not read ClientProfilesDb
  directly or maintain another identity-to-client table. The initial design
  shall not cache mappings across requests. A missing or inactive mapping shall
  return `403 PROFILE_REQUIRED` at card and quote endpoints; resolution failure
  or ClientProfiles unavailability shall fail closed with `503` and shall not
  call Processor. Public profile creation is the only protected client
  operation allowed before the mapping exists.
- The API transport layer shall supply the resolved GUID through a current
  actor abstraction. PayableServices has an existing abstraction to adapt;
  ClientProfiles and Payments.Api need equivalent integration. Client-owned
  Application and Domain use cases shall receive only the internal `ClientId`;
  the mapping use case receives a transport-neutral external identity key.
- Card, quote, payment, and execution ownership in PaymentDb and
  PayableServicesDb, and the cross-service contracts that carry ownership,
  shall use the internal `ClientId` consistently. Processor shall compare the
  resolved identifier with stored-card and payment ownership as it does today.
- Application and Domain projects shall not parse the Keycloak `sub` claim,
  depend on `HttpContext`, or depend on Keycloak packages.
- Neither a token subject nor a caller-supplied `ClientId` shall override the
  resolved owner in a request body, route, or query parameter.
- A new Keycloak identity, even with an email address used before, shall not
  inherit another subject's mapping. Account recovery or relinking requires an
  explicit, separately authorized workflow.

This mapping is required even though there is no production data. It prevents
a future identity-provider change from rewriting payment ownership records.
The existing subject-string ownership columns and contracts must still change
as part of this migration; adding the mapping alone does not switch ownership.

### `KC-008` Roles and Authorization Policies

Keycloak shall issue the following Pagarte application realm roles in the
access-token `realm_access.roles` claim. These roles are separate from
Keycloak's own realm-administration permissions:

| Role | Intended authority |
| --- | --- |
| `client` | Operate only on the authenticated client's own profile, cards, quotes, and payments |
| `support` | Perform only separately approved support operations; none are exposed by this migration |
| `admin` | Invoke approved maintenance and operational commands; no implicit client-data access |

- Self-registration shall assign only `client`. Only authorized identity
  administrators may assign or remove `support` and `admin`. A realm-wide
  default role must not grant `client` to operator accounts.
- APIs shall map only these exact application role values to named ASP.NET Core
  policies. `VerifiedClient` requires `client` and verified email;
  `VerifiedAdmin` requires `admin` and verified email. Ordinary profile, card,
  and quote endpoints shall use `VerifiedClient` and the resolved owner.
- The existing catalogue-sync and payment-route-activation endpoints shall use
  `VerifiedAdmin` in this migration. They currently have no authorization
  boundary; the migration is incomplete while either remains anonymous.
- APIs shall deny privileged endpoints by default. An authenticated `support`
  user has no access to `VerifiedClient` or `VerifiedAdmin` endpoints solely
  because of that role.
- Possession of an `admin` or `support` role shall not bypass payment domain
  invariants or create an unrestricted database-access path.
- Catalogue maintenance, route activation, refunds, manual review, and support
  search shall each receive an explicit policy before they are exposed. The
  latter three operations are outside this migration and remain unexposed.
- Keycloak administration credentials shall never be issued to browser or
  mobile clients.

### `KC-009` Local Aspire and Docker Deployment

- The Aspire AppHost shall run Keycloak and a dedicated PostgreSQL database as
  container resources for local development.
- PostgreSQL data shall use a persistent Docker volume.
- The Keycloak container shall wait for PostgreSQL readiness before starting.
- Keycloak shall use its own database and credentials. Existing Pagarte SQL
  Server databases shall not be converted to PostgreSQL as part of this
  requirement.
- AppHost references or environment mappings shall supply connection and
  service-discovery data and the identity-resolution workload credential
  without committing passwords or credentials.
- Local realm initialization shall be deterministic enough for a clean
  checkout to create the required realm, clients, audiences, and roles.
- The realm shall advertise one canonical issuer URL that the local browser
  and all validating APIs can use consistently. Internal metadata or key
  retrieval may use a separate reachable address only if issuer validation
  remains bound to that canonical URL.

### `KC-010` Production Deployment

- Production shall run Keycloak in production mode, not development mode.
- The public realm issuer and reverse-proxy hostname shall be explicit and
  stable across browser redirects, tokens, discovery metadata, and API
  validation.
- Keycloak and PostgreSQL shall use durable storage, backup, recovery, and
  monitored health appropriate for an authentication dependency.
- TLS shall protect browser-to-Keycloak, API metadata, token, and
  administrative traffic.
- Administrative endpoints shall not be exposed more broadly than required.
- Database, bootstrap-administrator, SMTP, OIDC confidential-client, and
  internal identity-resolution credentials shall come from the deployment
  secret store.
- Keycloak availability and readiness shall be monitored because new logins
  and token refreshes depend on it.
- Existing realm configuration shall be reconciled through a reviewed,
  repeatable deployment step. Startup import alone is insufficient because it
  does not update a realm that already exists. Reconciliation shall preserve
  user and session state and shall have a rollback procedure.

### `KC-011` Logout and Revocation

- Client logout shall terminate the relevant Keycloak session and clear local
  client authentication state.
- Compromised users or administrators shall be able to revoke active sessions.
- APIs may continue accepting an already-issued access token until its short
  expiry unless an explicit online-revocation requirement is accepted later.
- Security-stamp behavior currently implemented by `Identity.Client` shall not
  be copied into Pagarte after Keycloak owns session revocation.

### `KC-012` Audit and Sensitive Data

- Authentication success, failure, recovery, administrative changes, and
  session revocation shall be observable through Keycloak events or approved
  audit integration.
- Pagarte logs shall not contain passwords, authorization codes, access tokens,
  refresh tokens, client secrets, reset links, or complete authentication
  headers.
- Security logs shall contain enough correlation information to investigate a
  failed login or rejected API request without exposing credentials.
- Personally identifiable claims shall be limited to what each API needs.

### `KC-013` Retirement of the Custom Authority

After Keycloak authentication and API validation pass the acceptance criteria:

- remove `Identity.Client.Api` from AppHost orchestration;
- remove the `Identity.Client` projects and their solution references;
- remove `IdentityClientDb` and its migrations from the deployable system;
- remove OpenIddict server and validation packages that are no longer used;
- remove the custom password, email-confirmation, password-reset,
  refresh-token, and passkey persistence workflows;
- remove obsolete authentication configuration and secrets; and
- update architecture, context, flow, transaction, and operational
  documentation to describe the implemented Keycloak boundary.

The removal shall happen only after all consuming APIs and client applications
have moved to Keycloak. Pagarte shall not operate two authoritative identity
stores after the migration is complete.

## Migration Strategy

Before any destructive cutover, inventory every deployed environment and
confirm that no production identity, profile, card, quote, payment, or active
execution data exists. If that assumption is false, stop this migration and
approve a separate data-preserving plan. Development and test identity and
domain data may be reset deliberately, then reseeded; do not silently
reinterpret subject-string ownership values as Pagarte `ClientId` values.
Record the reset and keep any required nonproduction backups. No
production-user migration is planned under the verified assumption.

Implementation shall proceed in this order:

1. Verify the no-production-data assumption and record the nonproduction reset
   plan for existing subject-owned records.
2. Add PostgreSQL and a version-pinned Keycloak resource to the Aspire AppHost.
3. Add reproducible realm, public OIDC client, audience, role, redirect, issuer,
   email-verification, and SMTP configuration, including an existing-realm
   reconciliation path.
4. Implement atomic profile/mapping creation in ClientProfiles, its protected
   internal resolver, and current-actor resolution in the three public APIs.
5. Convert ownership fields and contracts in cards, quotes, payments, and
   execution handoffs to the internal `ClientId`; reset or migrate existing
   nonproduction data according to step 1.
6. Change public APIs to validate Keycloak access tokens and apply the named
   client and administrator policies, including existing maintenance routes.
7. Confirm the client-application inventory. The requester confirmed no browser
   or native clients exist yet, so validate the configured public clients with
   a synthetic Authorization Code + PKCE flow. Future applications must use
   Keycloak-owned registration, recovery, and logout before release.
8. Verify all acceptance criteria and negative security tests against Keycloak.
9. Remove `Identity.Client`, `IdentityClientDb`, and obsolete OpenIddict code.
10. Update the current-state documentation and transaction inventory in the
   same change that completes the migration.

Temporary coexistence is allowed only during implementation. A temporary
compatibility path shall be disabled by default and removed before the
requirement is considered complete.

## Acceptance Criteria

The requirement is complete when all of the following are true:

- A clean local environment starts PostgreSQL, Keycloak, and the Pagarte APIs
  through AppHost with the realm, OIDC clients, audience scopes, and roles
  provisioned without manual console steps; a user can then self-register.
- Browser and native OIDC clients use their configured redirects and PKCE. The
  same canonical issuer works for browser login and all three API validators.
- A versioned realm change can be applied to an existing environment without
  deleting users or sessions; a restart alone is not treated as reconciliation.
- A client can register, verify email, authenticate with Authorization Code +
  PKCE, refresh its session, recover credentials, change credentials, and log
  out through Keycloak-supported flows.
- Direct Access Grants are disabled.
- Each protected user-facing endpoint accepts a correctly signed, unexpired
  access token with its required issuer, audience, verified-email claim, and
  endpoint role.
- Each protected user-facing endpoint rejects tokens with a wrong issuer,
  wrong audience, invalid signature, expired lifetime, missing subject, or the
  wrong token type.
- An unverified user, a token without the required application role, and an
  ordinary client token cannot invoke protected or administrator operations.
- Newly registered users receive only the `client` role. Catalogue sync and
  payment-route activation require `VerifiedAdmin`, including for anonymous
  requests and tokens carrying only `client` or `support`.
- A client cannot read or mutate another client's profile, card, quote, or
  payment by supplying identifiers in a request.
- Profile creation and `(iss, sub)` mapping creation are atomic. Retried and
  concurrent requests cannot create multiple client IDs for one identity.
- The same Keycloak identity resolves to the same internal GUID in
  ClientProfiles.Api, Payments.Api, PayableServices.Api, and Processor-owned
  card and payment records. A second identity cannot claim that GUID.
- Card and quote operations return `403 PROFILE_REQUIRED` when the identity has
  no active profile mapping. They fail closed with `503` when ClientProfiles
  resolution is unavailable; no Processor operation is started.
- The internal resolver rejects anonymous and unapproved workloads and returns
  no profile data. External callers cannot select another `(iss, sub)` or
  `ClientId` through a public API.
- Keycloak restart preserves realm configuration, users, keys, and sessions as
  required by the configured lifetime policies.
- Production access tokens expire within 15 minutes, bounding the period in
  which a logged-out or revoked user's already-issued token can remain valid.
- Secrets and tokens do not appear in committed configuration, logs, API
  responses, or test snapshots.
- Authentication and authorization integration tests run against a real
  Keycloak container using the versioned realm configuration, including
  verification, audiences, roles, restart, and issuer behavior.
- The nonproduction data reset or conversion is verified, and no stored card,
  quote, payment, or execution record remains owned by a former token subject.
- `Identity.Client.Api` is no longer started or deployed, and no active service
  depends on `IdentityClientDb` or OpenIddict token issuance.
- The quote-payment-execution saga retains its existing business transitions,
  idempotency, and card-ownership checks with the internal `ClientId`.

## Out of Scope

- Migrating production users or credentials under the verified no-data
  assumption.
- Converting Pagarte domain databases from SQL Server to PostgreSQL.
- Changing quote calculation or payment execution behavior.
- Replacing the PayableServices-to-Processor workload API key. `SEC-02`
  separately tracks authentication for the remaining Processor gRPC services;
  the new ClientProfiles resolver has its own scoped workload boundary.
- Changing the public/private decision for catalogue browsing, tracked by
  `SEC-04`. Existing catalogue maintenance and route activation are in scope.
- Requiring MFA or passkey enrollment for migration completion; `AUTH-12`
  tracks the client-facing enrollment and authentication transactions.
- Implementing KYC, AML, sanctions screening, fraud scoring, or payment-card
  authentication such as 3-D Secure.
- Granting Keycloak roles direct authority over database records without
  Pagarte application policies and domain checks.

## Required Documentation Updates During Implementation

When the code implements this requirement, update these documents in the same
pass:

- `docs/architecture.md` for the runtime map, ownership, integration style,
  data boundaries, and AppHost resources;
- `docs/contexts/identity-client.md` to retire or replace the current custom
  identity context description;
- `docs/contexts/client-profiles.md` for the external identity mapping;
- `docs/contexts/payments-api.md`, `docs/contexts/payable-services.md`, and
  `docs/contexts/payment-switch-processor.md` for resolved ownership IDs and
  the internal resolution and execution boundaries;
- `docs/flows/authentication.md` for Authorization Code + PKCE, Keycloak token
  issuance, audiences, roles, identity resolution, and logout;
- `docs/flows/quote-lifecycle.md` and `docs/flows/payment-lifecycle.md` for
  the internal `ClientId` carried across quote and execution boundaries;
- `docs/transactions.md` for authentication transaction ownership and security
  prerequisites; and
- `docs/current-problems.md` when the administrator/support authorization gap
  or internal workload boundary changes.

Do not update those current-state documents to claim Keycloak is implemented
until the application actually follows this requirement.

## References

- [Keycloak: Planning for securing applications and services](https://www.keycloak.org/securing-apps/overview)
- [Keycloak: OpenID Connect grant types](https://www.keycloak.org/securing-apps/oidc-layers)
- [Keycloak Server Administration Guide](https://www.keycloak.org/docs/latest/server_admin/)
- [Keycloak hostname configuration](https://www.keycloak.org/server/hostname)
- [Keycloak realm import behavior](https://www.keycloak.org/server/importExport)
- [Aspire PostgreSQL integration](https://learn.microsoft.com/dotnet/aspire/database/postgresql-integration)
