# PaymentSwitch.Processor

## Purpose

`PaymentSwitch.Processor` is the internal synchronous payment boundary and
owner of `PaymentDb`. It registers cards, charges cards, records payment
state, and creates durable post-charge outbox messages.

The current implementation also contains a simpler service catalogue and a
second quote aggregate.

## Owns

- Credit-card registration and persistence.
- Payment-operator configuration and resolution.
- Synchronous card authorization.
- Payment records and transaction-status changes.
- SQL outbox creation and publication.
- Internal gRPC service implementations.
- Currently, local `Service`, `Company`, `PaymentQuote`, and
  `PaymentQuoteDetail` records.

## Does Not Own

- Public HTTP endpoints.
- Client identity or profile data.
- Asynchronous company delivery and refund orchestration.
- The rich PayableServices catalogue and route model.

Whether Processor should continue owning its local catalogue and quotes is
unresolved.

## Runtime Boundary

```text
Payments.Api and PayableServices
  -> PaymentSwitch.Contracts gRPC
  -> PaymentSwitch.Processor
      -> PaymentDb
      -> ExternalConnections.PaymentOperators
      -> SQL outbox
      -> Infrastructure.RabbitMQ
```

## Projects and Layers

Processor is one deployable project with internal layers:

- `GrpcServices` contains transport adapters.
- `Application/UseCases` coordinates workflows.
- `Domain` contains payment entities and state transitions.
- `Infrastructure` contains EF Core repositories, gateways, and outbox
  implementations.
- `Services` contains hosted loops, seeding, and operator resolution.

## Main Use Cases

- Register, update, delete, list, and read credit cards.
- Create and confirm Processor-owned payment quotes.
- Read payment details and history.
- Read the Processor-owned service catalogue.
- Publish pending SQL outbox messages.

## Domain Rules

- Card charging is synchronous.
- A payment is saved in `ChargingCard` state before the external operator call.
- A failed charge is persisted as failed.
- After a successful charge, quote status, payment status, and the outbox
  message are saved together.
- Domain methods receive time captured through `IClock`.
- External operator calls stay behind registration and authorization gateways.

## Data Ownership

`PaymentSwitch.Processor` owns `PaymentDb`, including cards, payments,
operators, fee configuration, outbox messages, and the currently duplicated
service and quote tables.

## Integrations

- Exposes `CreditCardService`, `PaymentService`,
  `PaymentExecutionService`, and `ServiceCatalogService`.
- Uses payment-operator adapters from
  `ExternalConnections.PaymentOperators`.
- Publishes messages through `Infrastructure.RabbitMQ` using contracts in
  `PaymentSwitch.Messaging`.

## Change Guidance

Read [Quote Lifecycle](../flows/quote-lifecycle.md),
[Payment Lifecycle](../flows/payment-lifecycle.md), and
[Current Problems](../current-problems.md) before changing quotes, services, or
execution contracts. Keep gRPC services thin and preserve the explicit
transaction boundary around card charging and outbox creation.
