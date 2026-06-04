# PaymentSwitch.Processor Design

`PaymentSwitch.Processor` is the internal payment processor and owner of `PaymentDb`. It is the synchronous payment boundary behind public APIs and payable-service quote confirmation.

## Scope

This project owns:

- Credit card registration and card persistence.
- Service catalog access used by the current payment model.
- Payment quote creation and confirmation.
- Payment operator resolution.
- Synchronous card charge during confirmation.
- SQL outbox publishing for post-charge messages.
- Internal gRPC contracts consumed by `Payments.Api` and `PayableServices`.

## Runtime Boundary

```text
PaymentSwitch.Processor
  -> gRPC server
  -> PaymentDb
  -> Infrastructure.RabbitMQ
  -> ExternalConnections.PaymentOperators
  -> PaymentSwitch.Contracts
  -> PaymentSwitch.Messaging
  -> Utilities.Responses
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

`PaymentSwitch.Processor` exposes the internal service contracts used by the rest of the system, including:

- `CreditCardService`
- `PaymentService`
- `PaymentExecutionService`
- `ServiceCatalogService`

## Notes

- `PaymentSwitch.Processor` is not a public HTTP API.
- It should not hardcode operator selection in business flow.
- It should keep credit card charging synchronous.
- It should keep async company-delivery work in `PaymentSwitch.Worker`.