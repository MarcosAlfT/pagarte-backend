# Payment Lifecycle

## Purpose

This document describes the implemented customer quote-to-payment flow and the
later refund path.

## Card Management

```text
Client
  -> Payments.Api HTTP
  -> PaymentSwitch.Processor CreditCardService gRPC
  -> payment-operator registration adapter
  -> PaymentDb
```

A stored card belongs to one client. The execution boundary verifies ownership
again before accepting a confirmed quote. A card issued outside the service
country is not rejected merely for being foreign; operator capability and
currency rules determine whether it can be used.

## Company-First Quote Execution

```text
Authenticated client
  -> PayableServices: create immutable quote
  -> PayableServices: confirm quote with stored-card ID
  -> authenticated Processor gRPC
      -> validate package, expiry, total, idempotency, and card ownership
      -> persist Payment + snapshot + retry policy
      -> check card readiness with operator
          -> definitive decline: fail; create a new quote to retry
      -> pay company service using service code and reference values
          -> definitive rejection: fail without charging card
          -> unknown: reconcile or retry with the same idempotency key
      -> charge card once for the full quote total
          -> success: complete
          -> retryable failure: persist schedule and retry
          -> unknown: reconcile; at cap require manual intervention
          -> definitive/exhausted retryable failure: roll back company now
      -> rollback company for the full service payment
          -> success: finish CompanyRolledBack
          -> retryable/unknown: persist retry state
          -> definitive/cap: require manual intervention
  -> PayableServices records Paid, PaymentFailed, or Processing
```

Processor's scheduler polls durable `NextAttemptAt` state. Each execution gets a
fresh dependency-injection and EF Core scope. Row-version concurrency protects
the transition that claims an operation, and the transition is committed before
the external call.

## Idempotency and Recovery

- `SourceQuoteId` has a unique database index.
- The immutable package, including selected card, has a deterministic
  fingerprint.
- Identical confirmation calls return the existing payment; conflicting reuse
  is rejected.
- Company payment, card charge, and rollback use separate stable idempotency
  keys.
- Attempt caps, interval, counters, next attempt, external references, and
  outcomes are stored on the payment.
- Unknown charge outcomes are not treated as failures because doing so could
  roll back a service after the card was actually charged.

## Money

The quote's finalized components are evidence for how the price was built.
Processor validates their checked sum but performs one company payment and one
card charge for the full total. Contracts and messages use signed 64-bit minor
units plus ISO currency; external adapters convert through `MoneyValue`.

## Later Refund Flow

```text
refund.request
  -> PaymentSwitch.Worker
  -> payment-operator refund adapter using full persisted payment total
      -> success: persist refunded status
      -> failure: persist retry schedule
      -> retries exhausted: publish alert
```

The legacy `payment.request -> company delivery` flow is removed.

## Deployment Rule

Before applying the Processor saga migration, use the old Worker to drain the
legacy RabbitMQ `payment.request` queue, legacy payments in active statuses,
and pending `payment.request` outbox messages. The migration checks the SQL
states and throws instead of silently converting those in-flight operations.

## Related Documentation

- [Quote Lifecycle](quote-lifecycle.md)
- [Quote Execution Saga](../decisions/0003-quote-execution-saga.md)
- [Global Money Handling](../decisions/0004-money-handling.md)
