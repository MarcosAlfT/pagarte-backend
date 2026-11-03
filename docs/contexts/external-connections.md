# ExternalConnections

## Purpose

`ExternalConnections` isolates third-party company and payment-operator
contracts from application and domain workflows.

## Owns

- Provider-specific request authentication and HTTP mapping.
- Company catalogue snapshots, balance inquiry HTTP calls, and safe normalized outcomes.
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
PayableServices
  -> CompanyInquiries adapter -> mock company catalogue/inquiry HTTP

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

`ExternalConnections.CompanyInquiries` implements Application-owned catalogue,
amount-inquiry, and trusted connection-configuration interfaces. The adapter
uses configured endpoints/API keys, disables redirects, applies a five-second
HTTP timeout, parses signed 64-bit USD minor units, and rejects malformed evidence.
PayableServices further validates identity, references, timestamps, and expiry.
Read-only inquiry timeouts normalize to `TemporarilyUnavailable`; balance evidence
is absent for failures. Latency and timeout counters use .NET metrics.
Catalogue parsing/transport failure yields no snapshot, preserving local state.
The mock adapter is disabled by default and rejected outside Development.

## Change Guidance

The draft [Payment Methods requirement](../requirements/payment-methods.md)
proposes payment-operator setup/token verification, challenge/status callbacks,
and optional detachment adapters. Its development operator mock is separate
from the company catalogue/balance mock; provider capabilities remain unverified.
Tokenization is selected to run from the mobile app directly with dLocal.
Pagarte's adapters will handle token-based card saving, verification, and
payment APIs; challenge handling depends on dLocal's supported mechanism.
`PM-009` requires replaceable provider adapters with normalized capabilities
and results. Provider payloads, signing, and callback parsing remain here;
application rules and public card contracts must not depend on dLocal shapes.

The implemented [catalogue and amount-inquiry requirement](../requirements/mock-amount-inquiry.md)
uses [Mock Company](mock-company.md) as the external substitute. Application
workflows, persistence, idempotency, and quote consumption remain in PayableServices.

Keep provider contracts here and expose stable internal outcomes. Read
[ADR 0003](../decisions/0003-quote-execution-saga.md) and
[ADR 0004](../decisions/0004-money-handling.md) before changing monetary or
execution semantics.
