# Pagarte.Engine Design

`Pagarte.Engine` is the async worker that continues payment processing after a card has already been charged successfully.

## Scope

This project owns:

- Payment request consumption.
- Company payment delivery.
- Refund handling with retries.
- Alert publication when refunds fail.
- Payment status updates via SQL.

## Runtime Boundary

```text
Pagarte.Engine
  -> RabbitMQ consumers
  -> ExternalConnections.CompanyPayments
  -> ExternalConnections.CardOperators
  -> Pagarte.Messaging
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

- `Pagarte.Engine` does not charge cards.
- It only runs after `Pagarte.Services` has already charged the card.
- It should keep the company and operator adapters behind `ExternalConnections`.
- It should not reference `Pagarte.Services` directly to avoid a circular dependency.
