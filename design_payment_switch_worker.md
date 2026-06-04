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

## Notes

- `PaymentSwitch.Worker` does not charge cards.
- It only runs after `PaymentSwitch.Processor` has already charged the card.
- It should keep the company and operator adapters behind `ExternalConnections`.
- It should not reference `PaymentSwitch.Processor` directly to avoid a circular dependency.
