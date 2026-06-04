# Payments.Api Design

`Payments.Api` is the public payments HTTP boundary. It is intentionally narrow and currently exposes only the credit-card surface.

## Scope

This project owns the public HTTP endpoints for:

- Listing client credit cards.
- Reading one client credit card.
- Registering a credit card.
- Updating credit-card metadata.
- Deleting a credit card.

It does not own catalogue browsing, quote creation, quote confirmation, card charging, or asynchronous company delivery.

## Runtime Boundary

```text
Payments.Api
  -> HTTP + OpenAPI
  -> Identity.Client token validation
  -> gRPC client to PaymentSwitch.Processor
  -> Utilities.Responses
  -> PaymentSwitch.Contracts
```

## Responsibilities

- Validate client access tokens issued by `Identity.Client`.
- Keep the public credit-card route surface stable.
- Call `PaymentSwitch.Processor` over gRPC for credit-card operations.
- Return the standard `Utilities.Responses` wrapper.

## Notes

- `Payments.Api` should stay thin and avoid payment-core business rules.
- It should not reference persistence directly.
- Quote and catalogue workflows belong to `PayableServices`.