# ExternalConnections

## Purpose

`ExternalConnections` isolates third-party company and payment-operator
contracts from application and domain workflows.

## Owns

- Provider-specific request authentication and HTTP mapping.
- Stored-card registration, readiness, charge status, charge, and refund calls.
- Company service payment, status reconciliation, and rollback calls.
- Translation of provider responses into succeeded, definitive, retryable, or
  unknown outcomes.

## Does Not Own

- Payment, quote, catalogue, or client business state.
- Retry caps, retry scheduling, or workflow status decisions.
- External credentials in transport messages or payment snapshots.

## Runtime Boundary

```text
PaymentSwitch.Processor
  -> company adapter -> external company
  -> payment-operator adapter -> external operator

PaymentSwitch.Worker
  -> payment-operator adapter -> external operator refund
```

## External Operation Flow

```text
Processor operation request
  -> gateway resolves server-side API key from credential reference
  -> provider adapter maps the HTTP request and authentication
  -> external provider
  -> adapter normalizes the response
      -> Succeeded ---------> Processor records durable success
      -> DefinitiveFailure -> Processor ends or compensates the workflow
      -> RetryableFailure --> Processor schedules persisted retry
      -> Unknown -----------> Processor reconciles before another side effect
```

Adapters do not update payment state or choose retry limits. Processor owns
those decisions and supplies stable, operation-specific idempotency keys.

## Domain Rules

- Company calls receive the snapshotted external service code, reference
  values, full finalized amount, and stable idempotency key.
- Company and payment-operator results never decide application retries.
- Timeouts and transport failures are represented as unknown when the external
  side effect may have occurred.
- Credentials are resolved server-side.
- Provider monetary requests are created from the shared exact `MoneyValue`.

## Current Implementations

- The dLocal adapter checks stored-card status, uses a stable
  `X-Idempotency-Key` for charges, and retrieves charge state for
  reconciliation.
- The mock adapter provides deterministic idempotent behavior and explicit
  retry/decline scenarios for development.
- The company HTTP adapter supports payment, status, and rollback endpoints and
  stable `Idempotency-Key` headers.

The external catalogue/feed bridge remains unimplemented; PayableServices
currently syncs from Processor's retained internal catalogue.

## Change Guidance

The proposed [catalogue and amount-inquiry requirement](../requirements/mock-amount-inquiry.md)
adds mock company catalogue and balance HTTP adapters. The mock substitutes
for external companies; application workflows and persistence remain in
PayableServices. These adapters are not implemented yet.

Keep provider contracts here and expose stable internal outcomes. Read
[ADR 0003](../decisions/0003-quote-execution-saga.md) and
[ADR 0004](../decisions/0004-money-handling.md) before changing monetary or
execution semantics.
