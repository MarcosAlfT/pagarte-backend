# Payment Lifecycle

## Purpose

This document describes the implemented customer quote-to-payment flow and the
later refund path.

## Card Management

The [card-registration flow](card-registration.md) implements owned durable
sessions, token-based setup with the Development operator, challenge/status
recovery, signed notification replay protection, and local removal. The mobile
dLocal module and live operator acceptance remain pending.

```text
Verified client
  -> Payments.Api HTTP (session ID + temporary token; no PAN/CVV)
  -> ClientProfiles resolves internal Client.Id
  -> workload-authenticated Processor CreditCardService gRPC
  -> application-owned session/lease transitions
  -> ExternalConnections tokenized setup/status adapter outside SQL transactions
  -> verified reusable reference + safe metadata in PaymentDb
```

Cards, sessions, accepted payments, and refunds retain the selected integration.
Changing the configured default affects only new registrations. Referenced
registry identities must remain unchanged; complete real-provider routing and
detachment acceptance is tracked in [Current Problems](../current-problems.md#6-payment-method-acceptance-and-recovery-gaps). Removal and
payment acceptance share an owner lock: removed cards reject new acceptance,
while already accepted work retains its references. Default clearing and
selection are atomic and database-enforced. No replacement default is chosen.

A stored card belongs to one client. The execution boundary verifies ownership
again before accepting a confirmed quote, along with setup verification, expiry,
and declared country/currency support. A card issued outside the service
country is not rejected merely for being foreign; operator capability and
currency rules determine whether it can be used.

## Development Charge-First Execution

New inquiry-backed bills on the configured Development company and mock
operator route pin charge-first execution at acceptance. Existing payments
retain their recorded mode. Processor starts the first charge only with fresh
bill evidence and a ready card.

PayableServices sends the finalized monetary leaves and approved quote total.
Processor validates their sum and uses the verified service-amount leaf for
company acknowledgment. Display subtotals and pricing rules stay in the quote.

```text
Accepted payment + reserved bill
  -> verify readiness and freshness
  -> persist charge attempt and charge the full quote total
      -> unknown: recover with the same charge key or recorded operator reference
      -> definitive decline: fail without notifying the company
  -> persist charge evidence and notify company using the service amount
      -> matching acknowledgment: complete and retain the bill reservation
      -> unknown: look up or retry the original notification
      -> matching definitive rejection: refund the full charge
          -> unconfirmed refund: retain RefundPending and the bill reservation
          -> confirmed refund: report Refunded and require new unpaid evidence for another attempt
```

Each operation commits its in-flight transition before contacting the provider.
A capped unknown charge or company acknowledgment remains Processing with an
operational error. An exhausted or definitively failed refund remains
RefundPending with an operational error. Processor owns the refund operation
and replays its original key. Worker accepts only eligible legacy refund work.

### Operator Recovery

`GET /api/admin/payments/recovery` lists stalled charge-first operations, oldest
first. A verified admin can submit the listed payment version, a reason
(`ProviderIncidentResolved` or `ProviderStatusAvailable`),
and an idempotency key to `POST /api/admin/payments/recovery/{paymentId}`.
Processor commits the audit and schedules one additional attempt. Retries of
the command return the same audit. A stale payment version conflicts.

The dispatcher reuses the persisted charge, acknowledgment, or refund key.
Provider evidence determines the result. A still-unconfirmed operation returns
to the queue after the attempt. The scheduling audit is available at
`GET /api/admin/payments/recovery/audit/{id}`. Configure
`PaymentRecoveryAuth:ApiKey` separately on Payments.Api and Processor.
Development AppHost supplies the `payment-recovery-workload-api-key` parameter.

## Company-First Quote Execution

The company-first flow applies to payments pinned to that mode. Both modes
bind one card and immutable submission package on a timely Pay claim.
Leased recovery can resume that intent after the original quote deadline.

```text
Authenticated client
  -> PayableServices: create immutable quote
  -> PayableServices: confirm quote with stored-card ID
  -> for an inquiry-backed quote, verify the original unpaid bill with a fresh operation key
  -> authenticated Processor gRPC
      -> replay recorded outcome or validate timely bound claim, total, and card
      -> persist Payment + snapshot + retry policy
      -> reserve the verified bill key in the same transaction as acceptance
      -> check card readiness with operator
          -> definitive decline: fail; create a new quote to retry
      -> if evidence is stale, wait for fresh evidence before the first company payment
      -> pay company service using service code and reference values
          -> definitive rejection: fail without charging card
          -> unknown: reconcile or retry with the same idempotency key
      -> charge card once for the full quote total
          -> success: complete
          -> retryable failure: persist schedule and retry
          -> unknown: reconcile; at cap require manual intervention
          -> definitive failure: roll back company now
          -> exhausted retryable failure for a bill-backed payment: manual review
      -> rollback company for the full service payment
          -> success: finish CompanyRolledBack
          -> retryable/unknown: persist retry state
          -> definitive/cap: require manual intervention
  -> PayableServices records Paid, PaymentFailed, Invalidated, NoLongerDue, or Processing
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
- One active reservation per bill key covers all clients and quote IDs.
  Successful or uncertain work holds it. Failure before the first company
  operation, definitive rejection of that first operation, or confirmed company
  rollback releases it. A new attempt needs verification after release.
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

Worker reads Processor-owned PaymentOperatorRoutes before consuming refund work;
configured adapter calls reject changes to the persisted integration identity and
credential reference. dLocal account clients resolve separate signing/transaction keys;
status mapping requires explicit PAID/SUCCESS and rejects mismatched payment evidence.
Card removal retains historical references and defers provider detachment until
accepted work is complete and payment-ID refunds are explicitly guaranteed to
survive removal. See [Card Registration and Removal](card-registration.md) for
cleanup states, unsupported capabilities, and the legacy-route limitation.

## Deployment Rule

Deploy the charge-first migration and updated Processor, Worker and
PayableServices before enabling `ChargeFirst:Enabled`. AppHost enables this
setting for the mock company and supplies `ChargeFirst:MockCompanyBaseUrl`.
Older Worker instances must be stopped before enabling the flag. Rollback of the
schema is blocked once charge-first payment evidence exists. Keep the original
company endpoint and operator integration available for accepted payments.

Before applying the historical company-first migration, use the old Worker to drain the
legacy RabbitMQ `payment.request` queue, legacy payments in active statuses,
and pending `payment.request` outbox messages. The migration checks the SQL
states and throws instead of silently converting those in-flight operations.

## Related Documentation

- [Quote Lifecycle](quote-lifecycle.md)
- [Quote Execution Saga](../decisions/0003-quote-execution-saga.md)
- [Global Money Handling](../decisions/0004-money-handling.md)
