# ClientProfiles

## Purpose

`ClientProfiles` owns client profile management. It is the public HTTP API for
client data and is separate from the identity service.

## Owns

- Clients.
- Person and organization profiles.
- Addresses.
- Phone numbers.
- Primary-contact selection.
- The unique `(issuer, subject)` to internal `Client.Id` mapping.

## Does Not Own

- User authentication or token issuance.
- Payment readiness decisions.
- Credit cards, quotes, or payment execution.

## Runtime Boundary

```text
ClientProfiles.Api
  -> HTTP, OpenAPI, Keycloak JWT validation, and internal identity resolution

ClientProfiles.Application
  -> use cases, DTOs, repository abstractions, and IUnitOfWork

ClientProfiles.Domain
  -> profile entities, enums, and business behavior

ClientProfiles.Persistence
  -> EF Core, SQL Server, repositories, migrations, and unit of work
```

## Main Use Cases

- Create and maintain person and organization clients.
- Resolve a verified external identity to an active internal client GUID.
- Create, update, delete, and select primary addresses.
- Create, update, delete, and select primary phone numbers.

## Domain Rules

- A client can have multiple active addresses and phone numbers.
- The first active address and phone become primary automatically.
- Later contacts may be non-primary.
- Ordinary create and update requests do not replace an existing primary
  contact.
- Primary changes are explicit operations.
- Selecting an active primary contact unsets the previous active primary of the
  same kind.
- Soft-deleted contacts do not participate in current-primary selection.
- Soft deletion retains the historical `IsPrimary` value.
- Deleted contacts are not restored; a replacement contact is created.
- SQL Server filtered unique indexes enforce at most one active primary address
  and one active primary phone per client.
- Address and phone state changes go through domain methods rather than direct
  property mutation.
- A database unique index prevents two clients from sharing an external
  `(issuer, subject)` key. The client primary key enforces the other side of
  the one-to-one relationship. Soft deletion makes the mapping inactive and
  does not reassign it.

## Data Ownership

`ClientProfiles` owns `ClientProfilesDb`. Repositories stage entity changes;
Application write use cases call `IUnitOfWork.SaveChangesAsync` once after
staging all related changes.

## Integrations

- Validates Keycloak access tokens with the `client-profiles-api` audience and
  requires `VerifiedClient` for profile and contact operations.
- Exposes a read-only internal resolver that requires a dedicated workload key
  and returns only the active `Client.Id`.
- Locally resolves the same mapping before profile reads, updates, deletion,
  and contact operations. Only person or organization profile creation can
  proceed before the mapping exists.
- Uses `Utilities.Responses` for public responses.
- Uses Aspire service defaults during local development.

## Change Guidance

Keep controllers thin, place workflow coordination in Application use cases,
and place contact invariants in Domain entities. Payment workflows may query
profile readiness, but they should not move payment decisions into this
context.
