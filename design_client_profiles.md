# ClientProfiles.Api Design

`ClientProfiles.Api` owns client profile management. It is the public API for client data, not the identity service.

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
  -> EF Core / SQL Server
  -> local repository and service layer
  -> Responses
```

## Responsibilities

- Validate the `Identity.Client` token before handling requests.
- Expose HTTP controllers for profile CRUD workflows.
- Translate HTTP DTOs into service calls.
- Persist client profile data through the local repository layer.

## Project Shape

`ClientProfiles.Api` is currently a single web project that contains its own:

- Controllers.
- DTOs.
- Domain models.
- Service layer.
- Infrastructure repository layer.
- Migrations.

That shape is acceptable here because the project is focused on client profile management rather than a deeper bounded-context split.

## Dependencies

- `Responses` for the shared API response format.
- `Identity.Client` as the runtime auth reference.
- SQL Server for persistence.
- AppHost service defaults for local development.
