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

## Internal Architecture

`PaymentSwitch.Processor` follows the Application Use Case Pattern inside the
single processor project.

- gRPC services are transport adapters that validate transport shape, call
  focused use cases, and map contract DTOs.
- Application use cases own workflows and queries:
  `CreatePaymentQuoteUseCase`, `ConfirmPaymentQuoteUseCase`,
  `RegisterCreditCardUseCase`, `UpdateCreditCardUseCase`,
  `DeleteCreditCardUseCase`, `GetCreditCardsUseCase`, `GetCreditCardUseCase`,
  `GetPaymentUseCase`, `GetPaymentHistoryUseCase`,
  `GetServiceCatalogUseCase`, `GetServiceUseCase`, and
  `PublishPendingOutboxMessagesUseCase`.
- Pricing logic lives behind `IPaymentQuotePricingService`.
- External card registration, card authorization, and payment operator adapter
  selection live behind `ICreditCardRegistrationGateway` and
  `ICardAuthorizationGateway`.
- SQL outbox creation lives behind `IPaymentRequestOutbox` so payment
  confirmation does not know message serialization, exchanges, or routing keys.
- SQL outbox publishing runs through `PublishPendingOutboxMessagesUseCase`; the
  hosted service only owns the polling loop.
- Time-dependent workflows use `IClock`; application and infrastructure code
  pass the captured UTC value into domain methods and message creation instead
  of letting entities or DTOs read system time directly.
- Transaction status uses the shared `PaymentTransactionStatus` contract from
  `PaymentSwitch.Messaging`.
- Domain entities own state transitions such as marking quotes paid and moving
  payments between transaction statuses.

## Function Separation

- Transport: `CreditCardGrpcService`, `PaymentGrpcService`,
  `PaymentExecutionGrpcService`, and `ServiceCatalogGrpcService` expose gRPC
  operations and delegate to use cases.
- Application: use cases coordinate repositories, domain methods, gateways,
  `IClock`, unit-of-work commits, and outbox creation.
- Domain: entities keep state-transition rules only; they receive timestamps
  from callers.
- Infrastructure: repositories, gateways, and outbox publishers implement
  persistence, external operator access, RabbitMQ publishing, and SQL outbox
  storage.
- Hosted services: polling loops trigger application use cases and do not own
  business decisions.

## New Requirement Guidance

- Add new user-visible payment workflows as application use cases first, then
  expose them through gRPC only as transport adapters.
- Keep external provider calls behind application abstractions and implement
  them in infrastructure gateways.
- Keep RabbitMQ message construction behind outbox/application services so the
  confirmation flow remains transactionally clear.
- Pass current time through `IClock`; do not add `DateTime.UtcNow` to entities,
  DTO defaults, repositories, or use cases.
- Put new payment state transitions on domain entities when they change entity
  invariants; keep orchestration in the use case.

## Persistence Boundaries

Repositories track aggregate changes but do not commit database transactions by
themselves. Payment workflows own the unit-of-work boundary and call
`SaveChangesAsync` at explicit points.

Payment confirmation intentionally saves the local `Payment` in
`ChargingCard` state before calling the external card operator. After the
external charge returns, the final quote status, payment status, and SQL outbox
message are saved together so a successfully charged card has a durable
post-charge message for the worker.

Seed data and domain factories also receive time from `IClock`, keeping test
time and production time behind the same abstraction.

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
