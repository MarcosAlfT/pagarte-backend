# PaymentSwitch.Worker

## Purpose

`PaymentSwitch.Worker` continues payment processing asynchronously after
Processor successfully charges a card.

## Owns

- Payment-request consumption.
- Company payment delivery.
- Payment-delivery success and failure handling.
- Refund execution and retry scheduling.
- Failed-refund alert publication.
- Payment email message handling.

## Does Not Own

- Credit-card registration or charging.
- Public HTTP or gRPC endpoints.
- Quote creation.
- Catalogue ownership.
- Processor domain or persistence assemblies.

## Runtime Boundary

```text
RabbitMQ
  -> PaymentSwitch.Worker consumers
  -> Application use cases
  -> ExternalConnections adapters
  -> PaymentDb status updates
  -> RabbitMQ follow-up messages
```

## Projects and Layers

Worker is one deployable project with internal layers:

- `Consumers` receives and deserializes RabbitMQ messages.
- `Application/UseCases` owns workflow decisions.
- `Application/Abstractions` defines gateways and repositories.
- `Services` implements SQL access, email delivery, and retry dispatch.

## Main Use Cases

- `ProcessPaymentRequestUseCase` sends a charged payment to the company,
  publishes success work, or creates a refund request.
- `ProcessRefundRequestUseCase` executes a refund, persists retries, and
  publishes an alert after retries are exhausted.
- `SendPaymentEmailUseCase` handles payment email messages.

## Domain Rules

- The worker runs only after Processor has charged the card.
- Refund retry intent is persisted through `RetryCount` and
  `NextRetryAt` before background redispatch.
- Time-dependent decisions use `IClock`.
- Status values use the shared `PaymentTransactionStatus` contract.

## Data Ownership

Processor owns `PaymentDb`. Worker updates payment and refund status through
`IPaymentStatusRepository` and direct SQL without referencing the Processor
project.

## Integrations

- Consumes and publishes messages defined by `PaymentSwitch.Messaging`.
- Uses `Infrastructure.RabbitMQ`.
- Uses company-payment and payment-operator adapters from
  `ExternalConnections`.
- Uses configured email infrastructure.

## Change Guidance

Keep consumers and polling services thin. Add workflow decisions to Application
use cases, retain retry state before relying on delayed work, and do not add a
direct project reference to Processor.
