# Pagarte.Services Design

`Pagarte.Services` is the internal payment core and owner of `PagarteDb`. It is the synchronous payment boundary behind the public API and the place where card operations, quote confirmation, and payment persistence live.

## Scope

This project owns:

- Credit card registration and card persistence.
- Service catalog access used by the current Pagarte payment model.
- Payment quote creation and confirmation.
- Payment operator resolution.
- Synchronous card charge during confirmation.
- SQL outbox publishing for post-charge messages.
- Internal gRPC contracts consumed by `Pagarte.API` and `PaymentServices`.

## Runtime Boundary

```text
Pagarte.Services
  -> gRPC server
  -> PagarteDb
  -> RabbitMQ
  -> ExternalConnections.CardOperators
  -> Pagarte.Contracts
  -> Pagarte.Messaging
  -> Responses
```

## Responsibilities

- Resolve the active payment operator from the database model.
- Register cards through the selected operator.
- Charge a card synchronously when a quote is confirmed.
- Mark the quote as paid only after a successful charge.
- Store the operator provider that was used.
- Record the post-charge `PaymentRequestMessage` in the SQL outbox.
- Publish pending outbox messages to RabbitMQ with retries.

## gRPC Surface

`Pagarte.Services` exposes the internal service contracts used by the rest of the system, including:

- `CreditCardService`
- `PaymentService`
- `PaymentExecutionService`
- `ServiceCatalogService`

## Notes

- `Pagarte.Services` is not a public HTTP API.
- It should not hardcode operator selection in business flow.
- It should keep credit card charging synchronous.
- It should keep async company-delivery work in `Pagarte.Engine`.
