# Payments.Api

## Purpose

`Payments.Api` is the public HTTP boundary for client credit cards.

## Owns

- Client card/session HTTP routes, including policy, defaults, and removal status.
- Bounded provider callback transport.
- VerifiedAdmin review/list/audit routes with a distinct workload credential.
- Authentication, identity resolution, request validation, and response mapping.

## Does Not Own

- Card persistence or payment-operator calls.
- Catalogue browsing.
- Quote creation or confirmation.
- Card charging.
- Company payment execution or refunds.

## Runtime Boundary

```text
Client
  -> Payments.Api HTTP
  -> Keycloak JWT validation and VerifiedClient policy
  -> protected ClientProfiles identity resolution
  -> workload-authenticated PaymentSwitch.Processor CreditCardService gRPC

Provider notification (no client JWT)
  -> bounded Payments.Api callback transport
  -> workload-authenticated CreditCardService gRPC
  -> ExternalConnections signature validation + Processor session/inbox handling
```

## Main Use Cases

The API translates public credit-card requests into calls to
`PaymentSwitch.Processor`. It does not currently expose catalogue, quote, or
payment-history HTTP endpoints.

## Domain Rules

Business rules remain behind the Processor boundary. Client-facing card/session
controllers validate transport shape, obtain the authenticated client identity,
call the matching gRPC operation, and map results to Utilities.Responses. The
provider callback has its separate signature-verification path below.

## Data Ownership

`Payments.Api` owns no database. Processor owns the credit-card records.

## Integrations

- Validates Keycloak `payments-api` access tokens. Before each client-owned card/session operation,
  resolves `(iss, sub)` to the internal client GUID and passes that GUID to
  Processor. Missing mappings return `403 PROFILE_REQUIRED`; resolver failure
  returns 503 without a Processor call.
- Uses `PaymentSwitch.Contracts` for gRPC calls.
- Uses `Utilities.Responses` for public responses.

## Change Guidance

[Card lifecycle](../flows/card-registration.md) use cases remain in Processor.
The API rejects unknown fields and retired raw-card submissions, derives client
identity, and returns safe views with meaningful HTTP statuses. Provider callbacks
forward bounded raw bytes/allowlisted headers; adapters verify them.

Admin routes derive actor from authenticated issuer/subject and accept coded reasons,
expected state, action, and request key. CardOperationsAuth must differ from the general
workload key. Requests cannot supply actor, success evidence, or provider references.

Keep transports thin. See the [test guide](../../tests/PaymentSwitch.Processor.Tests/README.md)
for routes/configuration and [remaining acceptance](../current-problems.md#6-payment-method-acceptance-and-recovery-gaps)
for dLocal/mobile work.
