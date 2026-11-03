# PaymentSwitch.Processor

## Purpose

`PaymentSwitch.Processor` is the authenticated internal payment boundary and
owner of `PaymentDb`. It manages stored cards and durably orchestrates confirmed
quote execution.

## Owns

- Credit-card registration and persistence.
- Stored-card ownership validation.
- Payment-operator configuration and selection.
- One payment and immutable execution snapshot per source quote.
- Company-payment, card-readiness, final-charge, reconciliation, retry, and
  company-rollback state transitions.
- Persisted retry policy, attempt counters, next-attempt times, external
  references, and idempotency keys.
- Internal gRPC implementations and the durable saga scheduler.
- SQL outbox infrastructure for remaining asynchronous messages.

## Does Not Own

- Public quote HTTP endpoints or client identity.
- Customer-visible quotes or quote status.
- Calculation of quote components or totals.
- External credentials in execution packages.
- Post-charge company delivery; the company is paid before final card charge.

## Runtime Boundary

```text
PayableServices workload
  -> authenticated PaymentExecutionService gRPC
  -> PaymentSwitch.Processor use cases
  -> PaymentDb durable saga state
  -> ExternalConnections company and payment-operator adapters
```

## Durable Execution Flow

```text
Confirmed execution package
  -> validate workload, shape, expiry, totals, route, service code, and card
  -> load Payment by SourceQuoteId
      -> same fingerprint: return existing Payment without repeating work
      -> different fingerprint: reject idempotency conflict
      -> not found: persist Payment + immutable snapshot + retry policy
  -> Accepted
      -> CheckingCard
          -> CardReady
          -> CardCheckRetryScheduled --------- scheduler resumes
          -> Failed -------------------------- final decline
      -> PayingCompany
          -> CompanyPaid
          -> CompanyPaymentRetryScheduled ---- scheduler resumes
          -> CompanyPaymentOutcomeUnknown ---- reconcile with same key
          -> Failed / ManualIntervention
      -> ChargingCard
          -> Completed
          -> ChargeRetryScheduled ------------ scheduler resumes
          -> ChargeOutcomeUnknown ------------ reconcile with same key
          -> RollingBackCompany on known final failure or exhausted retry
          -> ManualIntervention when an unknown charge cannot be resolved safely
      -> RollingBackCompany
          -> CompanyRolledBack ---------------- final failed execution
          -> RollbackRetryScheduled ----------- scheduler resumes
          -> ManualIntervention
```

Each external operation is preceded by a persisted in-progress state. A due
retry is loaded in a fresh dependency-injection scope, and optimistic
concurrency allows only one dispatcher to claim the transition. Provider
outcomes are normalized as succeeded, definitive failure, retryable failure,
or unknown; the use case, not the adapter, decides the next state.

## Main Use Cases

- Register, update, delete, list, and read stored cards.
- `AcceptConfirmedQuoteUseCase` validates and idempotently accepts one immutable
  execution package per source quote.
- `ContinuePaymentExecutionUseCase` advances the durable company-first saga.
- `GetPaymentExecutionStatusUseCase` reports current execution state.
- Read payment history and the retained internal service catalogue.
- Publish remaining pending SQL outbox messages.

## Domain Rules

- A stored card owned by another client is rejected.
- The `ClientId` carried by Payments and PayableServices is the canonical
  string form of the internal `ClientProfiles.Client.Id` GUID; Processor does
  not consume a Keycloak subject.
- `SourceQuoteId` is unique; a repeated identical package is replayed and a
  conflicting fingerprint is rejected.
- Every external operation is preceded by a durable in-progress state and uses
  a stable operation-specific idempotency key.
- Unknown outcomes are reconciled when a provider reference is available and
  otherwise retried only with the same idempotency key.
- An unknown card-charge outcome at its cap goes to manual intervention; it is
  never rolled back as if a charge were known to have failed.
- A definitive or exhausted retryable charge failure immediately starts full
  company rollback.
- The final charge uses the exact full quote total in minor units.

## Data Ownership

`PaymentSwitch.Processor` owns cards, payments, execution snapshots, retry
state, operators, the retained internal catalogue, and outbox records in
`PaymentDb`. The legacy Processor quote and fee tables are removed by
migration.

The saga migration refuses to run while legacy payment executions or
`payment.request` outbox records are still in flight. Deployment must also
drain the old RabbitMQ queue with the old Worker before replacing it.

## Integrations

- Exposes `CreditCardService`, query-only `PaymentService`,
  `PaymentExecutionService`, and `ServiceCatalogService`.
- Uses `ExternalConnections.PaymentOperators` for card operations.
- Uses `ExternalConnections.CompanyPayments` for service payment,
  reconciliation, and rollback.
- Resolves company API keys from server-side configuration by credential
  reference.

Inquiry-backed quotes use the existing immutable execution contract and payment
transitions. The local AppHost resolves the mock company's credential reference
through Processor's existing company-credential configuration. Processor's
retained catalogue is no longer the source of PayableServices synchronization.

## Change Guidance

The draft [Payment Methods requirement](../requirements/payment-methods.md)
proposes durable registration sessions, removal of full card-number storage,
atomic default selection, and coordination of removal with accepted payments.
The selected flow tokenizes directly with dLocal from the mobile app. Processor
will accept temporary tokens and persist verified reusable card_id references;
raw-card inputs are to be removed, not forwarded transiently.
The requirement also proposes central operator selection for new sessions and
immutable provider/account routing for existing cards and accepted operations.
Changing the default must preserve old-provider recovery and refund paths.
Provider-dependent payment-time challenges require a separate execution decision.

Keep gRPC services and the scheduler thin. Put workflow decisions in focused
application use cases and persist a concurrency-protected transition before
each external call. Read [ADR 0003](../decisions/0003-quote-execution-saga.md)
and [ADR 0004](../decisions/0004-money-handling.md).
