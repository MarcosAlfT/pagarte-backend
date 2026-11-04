# PaymentSwitch.Worker

## Purpose

`PaymentSwitch.Worker` handles later refund and notification work. It no longer
delivers a charged payment to the company for confirmed-quote execution.

## Owns

- Refund-request consumption and refund retry scheduling.
- Failed-refund alert publication.
- Payment email message handling.

## Does Not Own

- Quote execution, company payment, final card charge, or company rollback.
- Credit-card registration.
- Public HTTP or gRPC endpoints.
- Quote or catalogue ownership.
- Processor domain or persistence assemblies.

## Runtime Boundary

```text
RabbitMQ refund/notification messages
  -> PaymentSwitch.Worker consumers
  -> application use cases
  -> payment-operator refund adapter / email provider
  -> PaymentDb refund status updates
```

## Main Use Cases

- `ProcessRefundRequestUseCase` refunds the payment's full persisted amount,
  persists retry state, and publishes an alert after retries are exhausted.
- `SendPaymentEmailUseCase` handles payment email messages.

The legacy `payment.request` consumer and post-charge company-delivery use case
have been removed.

## Domain Rules

- Refund messages and gateway calls use exact minor units and currency.
- Refund retry intent is persisted before background redispatch.
- Time-dependent decisions use `IClock`.
- Status values use the shared `PaymentTransactionStatus` contract.

## Data Ownership

Processor owns `PaymentDb`. Worker updates only refund-related fields and
statuses through `IPaymentStatusRepository` and direct SQL, without referencing
the Processor project. It also reads Processor-owned PaymentOperatorRoutes to
validate retained integration identities; it never creates or updates route pins.

## Integrations

Refunds use the payment's retained integration ID through the shared factory, not the
current default. Worker loads Processor-owned identity/credential pins before consumers,
waits briefly for schema/pins, and rejects repointed routes. AppHost orders it after
Processor and retains mock-a's configuration. Keep routes until dependent work is resolved.

Configured dLocal accounts use separate credentials; refunds require explicit SUCCESS.
The [test guide](../../tests/PaymentSwitch.Processor.Tests/README.md) owns credential setup.
[Card removal](../flows/card-registration.md#stored-cards-and-payment-ordering) preserves
accepted execution/refunds. Live provider acceptance remains in
[Current Problems](../current-problems.md#6-payment-method-acceptance-and-recovery-gaps).

## Change Guidance

Keep consumers and polling services thin. Do not reintroduce company delivery
after card charge or place card tokens, API keys, or credentials in messages.
