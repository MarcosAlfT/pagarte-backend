# Payments.Api

## Purpose

`Payments.Api` is the public HTTP boundary for client credit cards.

## Owns

- Public routes for listing credit cards.
- Reading one credit card.
- Registering a card.
- Updating card metadata.
- Deleting a card.
- Authentication and HTTP response mapping for those routes.

## Does Not Own

- Card persistence or payment-operator calls.
- Catalogue browsing.
- Quote creation or confirmation.
- Card charging.
- Asynchronous company delivery or refunds.

## Runtime Boundary

```text
Client
  -> Payments.Api HTTP
  -> OpenIddict token validation
  -> PaymentSwitch.Processor gRPC
```

## Main Use Cases

The API translates public credit-card requests into calls to
`PaymentSwitch.Processor`. It does not currently expose catalogue, quote, or
payment-history HTTP endpoints.

## Domain Rules

Business rules remain behind the Processor boundary. Controllers validate
transport shape, obtain the authenticated client identity, call the matching
gRPC client operation, and map the result to `Utilities.Responses`.

## Data Ownership

`Payments.Api` owns no database. Processor owns the credit-card records.

## Integrations

- Validates `payments-api` access tokens issued by `Identity.Client`.
- Uses `PaymentSwitch.Contracts` for gRPC calls.
- Uses `Utilities.Responses` for public responses.

## Change Guidance

Keep this API narrow. Add a new route here only when it is part of the public
card boundary or the team explicitly changes the bounded-context ownership.
