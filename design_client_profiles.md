# ClientProfiles Design

`ClientProfiles` owns client profile management. `ClientProfiles.Api` is the public HTTP API for client data, not the identity service.

## Scope

`ClientProfiles.Api` handles client profile workflows such as:

- Clients.
- Persons and organizations.
- Addresses.
- Phones.

It does not issue tokens and it should not take ownership of payment workflows.

## Runtime Boundary

```text
ClientProfiles.Api
  -> HTTP API
  -> OpenIddict validation
  -> Responses

ClientProfiles.Application
  -> use cases, DTOs, repository abstractions, and unit of work contract

ClientProfiles.Domain
  -> profile entities, enums, and business behavior

ClientProfiles.Persistence
  -> EF Core / SQL Server
  -> DbContext, repositories, migrations, and unit of work implementation
```

## Responsibilities

- Validate the `Identity.Client` token before handling requests.
- Expose HTTP controllers for profile CRUD workflows.
- Translate HTTP DTOs into application use case calls.
- Persist client profile data through the persistence repository layer.
- Commit successful write workflows once through the application unit of work.

## Project Shape

`ClientProfiles` is split into:

- `ClientProfiles.Api` for controllers, authentication, OpenAPI, and HTTP wiring.
- `ClientProfiles.Application` for use cases, DTOs, repository abstractions, response mapping, messages, and `IUnitOfWork`.
- `ClientProfiles.Domain` for `Client`, `Person`, `Organization`, `Address`, `Phone`, and related enums.
- `ClientProfiles.Persistence` for `ClientProfilesDbContext`, EF repositories, dependency injection, migrations, and the `IUnitOfWork` implementation.

Repositories track entity changes and do not call `SaveChangesAsync()` directly. Application write workflows call `IUnitOfWork.SaveChangesAsync()` once after all related changes are staged, so person and organization client creation persist the base `Client` row and profile-specific row atomically.

## Primary Contact Rules

- A client can have multiple active addresses and phones.
- The first active address and first active phone created for a client become primary automatically.
- Additional active addresses and phones can be non-primary.
- Normal address and phone create/update requests do not change an existing primary record.
- Primary changes are explicit business actions exposed through `PUT /api/client/address/{addressId}/primary` and `PUT /api/client/phone/{phoneId}/primary`.
- Setting an active address or phone as primary unsets the previous active primary item of the same kind.
- Persistence owns the safe primary-switch mechanics through `SetPrimaryAddressAsync` and `SetPrimaryPhoneAsync`, which unset the old active primary before setting the selected active record as primary.
- Soft-deleted addresses and phones are excluded from current primary logic.
- Soft delete preserves `IsPrimary` as historical evidence of whether the record was primary when deleted.
- There is no restore flow for deleted addresses or phones; the client creates a new record if the same information is needed again.
- SQL Server enforces one active primary address and one active primary phone per client with filtered unique indexes on `IsPrimary = 1 AND IsDeleted = 0`.
- Payment readiness checks are owned by the payment workflow, not by `ClientProfiles`; payments should validate that a client has an active primary address and phone before creating or processing a payment.

## Domain Encapsulation

- Address and phone state is changed through domain methods such as `UpdateAddress`, `UpdatePhone`, `SetPrimary`, `DeleteAddress`, and `DeletePhone`.
- Important state such as primary flags, soft-delete flags, timestamps, and editable contact fields should not be mutated directly outside the domain entity.

## Dependencies

- `Utilities.Responses` for the shared API response format.
- `Identity.Client` as the runtime auth reference.
- SQL Server for persistence.
- AppHost service defaults for local development.
