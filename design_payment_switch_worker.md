# PaymentSwitch.Worker Design

`PaymentSwitch.Worker` is the async worker that continues payment processing after a card has already been charged successfully.

## Scope

This project owns:

- Payment request consumption.
- Company payment delivery.
- Refund handling with retries.
- Alert publication when refunds fail.
- Payment status updates via SQL.

## Runtime Boundary

```text
PaymentSwitch.Worker
  -> RabbitMQ consumers
  -> ExternalConnections.CompanyPayments
  -> ExternalConnections.PaymentOperators
  -> PaymentSwitch.Messaging
  -> SQL access for status updates
```

## Responsibilities

- Consume `payment.request`.
- Send the payment to the company.
- Mark the payment as completed when the company accepts it.
- Mark the payment as failed and publish a refund request when the company rejects it.
- Consume `refund.request`.
- Retry refunds up to the configured maximum.
- Publish alerts when refund retries are exhausted.

## Internal Architecture

`PaymentSwitch.Worker` follows the Application Use Case Pattern inside the
worker project.

- RabbitMQ consumers are transport adapters that receive messages and delegate
  to focused use cases.
- `ProcessPaymentRequestUseCase` owns company payment delivery, success
  notification publishing, and refund request creation when company delivery
  fails.
- `ProcessRefundRequestUseCase` owns refund execution, retry scheduling, and
  failed-refund alert publishing.
- `SendPaymentEmailUseCase` owns email message handling.
- Refund execution lives behind `IRefundGateway` so the use case does not know
  payment-operator adapter factory details.
- Refund retries are persisted in PaymentDb through `RetryCount` and
  `NextRetryAt`; `RefundRetryDispatcherService` republishes due retry messages
  so restarts do not lose scheduled retries.
- Time-dependent workflows use `IClock`; use cases and repositories set message
  timestamps, retry timestamps, and payment update timestamps from the injected
  clock instead of relying on DTO defaults or direct system time.
- Payment status updates use the shared `PaymentTransactionStatus` contract from
  `PaymentSwitch.Messaging`.
- Payment status persistence stays behind `IPaymentStatusRepository`.

## Function Separation

- Transport: RabbitMQ consumers deserialize messages and call the matching use
  case.
- Application: `ProcessPaymentRequestUseCase`, `ProcessRefundRequestUseCase`,
  and `SendPaymentEmailUseCase` coordinate workflow decisions, gateway calls,
  publishing, and retry scheduling.
- Infrastructure: `PaymentStatusRepository` owns SQL updates and due-refund
  reads; `PaymentOperatorRefundGateway` owns payment-operator refund adapter
  calls; `EmailSenderService` owns email delivery.
- Hosted services: `RefundRetryDispatcherService` owns the polling loop for due
  retries and republishes persisted refund requests.
- Messaging contracts: message DTOs carry data only; producers set `CreatedAt`
  using `IClock`.

## New Requirement Guidance

- Add new async payment workflows as application use cases and keep consumers as
  thin transport adapters.
- Keep payment-operator and company-provider integrations behind gateways or
  `ExternalConnections` adapters.
- Persist retry intent before relying on background dispatch so server restarts
  do not lose work.
- Pass current time through `IClock`; do not add `DateTime.UtcNow` to message
  DTO defaults, repositories, or use cases.
- Keep status changes behind `IPaymentStatusRepository` unless the worker gets
  its own bounded persistence model.

## Notes

- `PaymentSwitch.Worker` does not charge cards.
- It only runs after `PaymentSwitch.Processor` has already charged the card.
- It should keep the company and operator adapters behind `ExternalConnections`.
- It should not reference `PaymentSwitch.Processor` directly to avoid a circular dependency.
