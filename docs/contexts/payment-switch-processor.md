# PaymentSwitch.Processor

## Purpose

`PaymentSwitch.Processor` owns PaymentDb and hosts internal card and payment
APIs. Card/session and quote-execution gRPC require workload authentication;
legacy payment-query/catalogue gRPC protection remains SEC-02 work. It manages stored cards and durably orchestrates confirmed
quote execution.

## Owns

- Durable card-registration sessions, token claims, setup leases/reconciliation, callback inbox, and safe stored-card persistence.
- Stored-card ownership validation.
- Payment-operator configuration and selection.
- One payment and immutable execution snapshot per source quote.
- Durable quote-submission acceptance/rejection outcomes.
- The stalled charge-first queue and append-only recovery scheduling audits.
- Card readiness, charge, company acknowledgment, compensation, reconciliation,
  and company-first payment and rollback state transitions.
- Persisted retry policy, attempt counters, next-attempt times, external
  references, and idempotency keys.
- Internal gRPC implementations and the durable saga scheduler.
- SQL outbox infrastructure for remaining asynchronous messages.

## Does Not Own

- Public quote HTTP endpoints or client identity.
- Customer-visible quotes or quote status.
- Calculation of quote components or totals.
- External credentials in execution packages.

PayableServices sends finalized monetary leaves and the approved total.
Processor validates that exact sum and stores the same leaves for execution.
Display groups, reference nodes, and pricing rules stay in PayableServices.

## Runtime Boundary

```text
Payments workload
  -> authenticated CreditCardService gRPC
  -> card/session use cases and PaymentDb registration state
  -> ExternalConnections setup/status/signature adapter

PayableServices workload
  -> authenticated PaymentExecutionService gRPC
  -> PaymentSwitch.Processor use cases
  -> PaymentDb durable saga state
  -> ExternalConnections company and payment-operator adapters
```

## Durable Execution Flow

```text
Confirmed execution package
  -> validate workload and load any recorded submission outcome/payment
  -> replay accepted work or the immutable rejection; reject changed package/card
  -> validate shape, timely bound submission, totals, route, service code, and card
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

- Start/complete/read/continue/cancel card-registration sessions and receive verified setup notifications.
- Update, remove, list, and read stored cards.
- List/reconcile stuck card operations and safely retry removal through audited admin reviews.
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
- New executions use `SubmitClaimedQuote`, a distinct authenticated gRPC method
  carrying the server submission ID/time and immutable snapshot/card. The claim
  must be UTC, within CreatedAt + five minutes and not in the future. Current
  time after quote expiry does not reject that valid claim. `ConfirmQuote`
  retains legacy accepted-payment replay but rejects new unclaimed execution.
- QuoteSubmissionOutcomes is committed with payment acceptance or a definitive
  rejection under the existing client/card mutation lock. SourceQuoteId is its
  primary key and SubmissionId is unique. A rejected attempt cannot become
  accepted later if card eligibility changes. The fingerprint binds submission
  metadata, snapshot, and card; legacy payment fingerprints remain unchanged.
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

Processor owns card-registration sessions, notification inbox, cards, payments,
execution snapshots, retry state, operators, the retained internal catalogue,
review audits, immutable route/credential-reference pins, and outbox records in
`PaymentDb`, including QuoteSubmissionOutcomes with accepted payment IDs or safe
rejection codes. Its downgrade refuses to discard recorded outcomes. The legacy Processor quote and fee tables are removed by
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

Processor accepts a single bound submission and durably records rejection or
acceptance. `SubmitVerifiedBill` validates fresh evidence against the approved
bill key and service amount. A transaction-owned lock and filtered unique index
allow one active reservation per bill across clients and quotes. Acceptance and
reservation commit together. A blocked client receives a generic rejection.
Successful and uncertain payments keep the reservation. A failure before the
first company operation, definitive rejection of that first operation, or
confirmed rollback releases it. Evidence for a new
payment must be fresh after release. If verification expires during card checks,
Processor waits for PayableServices to refresh it before the first company payment.

Supported Development bills pin charge-first execution, the billing period,
notification endpoint, and one compensation key on acceptance. Processor
validates freshness before charging and commits charge evidence before notifying
the company. It completes on matching acknowledgment or refunds the full charge
on definitive rejection. An unknown result holds the bill reservation. Worker
admits only matching legacy refunds. See the [Quotes requirement](../requirements/quotes.md).
Execution status lookup returns the owned payment or durable rejection by
source quote ID. A missing outcome keeps an already submitted quote pending.
For stalled charge-first work, a scoped recovery request records actor, reason,
request key, payment version, and operation stage in the transaction that
schedules one attempt. The normal dispatcher recovers the original operation.

Deploy the Processor RPC and migration before PayableServices. Existing accepted
work remains recoverable with its original evidence and execution order.
Explicit quote cancellation is out of scope; navigating away does not stop
accepted or uncertain execution. Submission fencing remains required.
PayableServices permanently consumes each quote at Pay. Processor replays or
recovers that original submission and never creates a second attempt for the
same quote, even after a final failure/refund. A later eligible customer payment
requires a new inquiry/quote and must still pass bill-level duplicate protection.

Processor owns the [card lifecycle](../flows/card-registration.md): durable sessions,
protected token claims, verified cards, defaults, recovery/cleanup, route pins, and
review audits. Shared owner locks and filtered uniqueness order card mutations against
payment acceptance. Removed cards preserve accepted-work/refund references.

CardOperationsService uses a distinct workload key. Reviews persist actor/reason and
intent before provider calls, commit provider-derived results with audit, and preserve
automatic caps, dependency checks, and cancellation/expiry fences.

The migration retains legacy references but requires re-registration for new use;
preconditions and downgrade limitations are in the flow. Live dLocal/mobile gaps remain
in [Current Problems](../current-problems.md#6-payment-method-acceptance-and-recovery-gaps).
Keep gRPC/schedulers thin and transitions durable; follow [ADR 0003](../decisions/0003-quote-execution-saga.md)
and [ADR 0004](../decisions/0004-money-handling.md).
