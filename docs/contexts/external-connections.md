# ExternalConnections

## Purpose

`ExternalConnections` isolates third-party company and payment-operator
contracts from application and domain workflows.

## Owns

- Provider-specific request authentication and HTTP mapping.
- Company catalogue snapshots, balance inquiry HTTP calls, and safe normalized outcomes.
- Tokenized setup/status/notification adaptation, stored-card readiness, charge status, charge, and refund calls.
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
HTTP timeout, parses signed 64-bit minor units and provider billing period,
and rejects malformed evidence. PayableServices validates currency, identity,
references, timestamps, and expiry. Creation expects USD. At verification a
different well-formed currency invalidates the quoted bill.
Read-only inquiry timeouts normalize to `TemporarilyUnavailable`; balance evidence
is absent for failures. Latency and timeout counters use .NET metrics.
Catalogue parsing/transport failure yields no snapshot, preserving local state.
The company-inquiry mock configuration is disabled by default and rejected
outside Development. Card-operator mock use is also restricted to Development.

## Change Guidance

Fresh bill verification uses the existing inquiry adapter with a new operation
key. The Development notification adapter validates the entire acknowledgment
against the pinned request and looks up the original operation on recovery.
Keep service amount separate from full card total:
USD 100 bill plus USD 2 fee means a USD 102 charge and a USD 100 bill confirmation.
Notification transfers no funds. Normalize matching OK, definitive NOT OK, and
unknown/transient outcomes so Processor can complete, refund, or reconcile.
Refund/status contracts must support full-charge recovery without duplicate
refunds. The Development operator journals the original charge and compensation
operation, and confirms a refund only when it matches the full charge.
Live notification and refund recovery contracts require acceptance.
Price and key strategies belong to PayableServices.

Use the existing factory/resolver and central integration registry. Processor pins
provider/account/environment/version/credential identity; Worker loads it read-only.
New defaults or registration disablement must preserve original routes for existing work.

The Development-only mock has independent provider state and optional idempotent removal.
Configured dLocal clients use matching account credentials, fixed HTTPS hosts, separate
login/transaction/signing keys, and bounded signed HTTP without redirects, retries, or
HTTP logging. Known-card readiness/removal and offline vault/signature contracts are
implemented; public setup remains gated by unconfirmed binding/recovery/challenge contracts.

Keep provider mappings here and return normalized evidence to Application use cases.
[Card flow](../flows/card-registration.md) covers lifecycle semantics; the
[test guide](../../tests/PaymentSwitch.Processor.Tests/README.md) owns configuration and
fixtures; [Current Problems](../current-problems.md#6-payment-method-acceptance-and-recovery-gaps)
tracks acceptance. Company catalogue/inquiry ownership remains in
[PayableServices](payable-services.md), using [Mock Company](mock-company.md).
Follow [ADR 0003](../decisions/0003-quote-execution-saga.md) and
[ADR 0004](../decisions/0004-money-handling.md) for execution/money changes.
