# Quote Lifecycle

## Purpose

This document describes current implementation. The accepted
[review/expiry/payment flow](quote-review-and-payment.md) and
[Quotes requirement](../requirements/quotes.md) retain pending Composite pricing
and charge-first company acknowledgment/full refund. Current execution is
company-first with bill verification for the Development company.
The five-minute Unpaid window, permanent Submitted claim, customer-list exclusion,
and durable submission recovery are implemented. The accepted server claim must
precede expiry; Processor acceptance and recovery may continue afterward.

PayableServices is the sole customer-visible quote owner. One quote represents
one payable service and one payment attempt.

## Creation

```text
Authenticated client
  -> read public catalogue details and required references
  -> POST /api/inquiries with references + client-scoped Idempotency-Key
  -> resolve internal Client.Id and validate references locally
  -> durably reserve inquiry/provider key; return pending for concurrent duplicates
  -> ExternalConnections calls the mock company over HTTP outside a transaction
  -> persist normalized outcome and verified evidence; lease recovery reuses provider key
  -> POST /api/quotes with InquiryId
  -> resolve `(iss, sub)` through ClientProfiles to internal Client.Id
  -> load active payable service, country, route, amount composition,
     and reference-field definitions
  -> require owned, unexpired AmountDue evidence compatible with the service/route/currency
  -> derive references and ServiceAmount from persisted inquiry
  -> snapshot strategy version and company/service/account/billing-period key
  -> replace configured base components exactly once
  -> finalize each amount component with ISO scale and ToEven rounding
  -> checked-sum the full positive total
  -> atomically consume inquiry and persist one immutable quote in PayableServicesDb
  -> set LifecycleVersion 1 and ExpiresAt = CreatedAt + five minutes
```

Legacy services without `RequiresInquiry` retain configured-amount quoting.
Inquiry-backed services reject direct quoting or supplied currency/reference
overrides. Zero balances and failure outcomes cannot create quotes.
Fresh inquiry evidence is required at creation; its shorter deadline does not
shorten the new quote's five-minute Unpaid window.
Provider minor units are converted through the shared currency scale; creation
does not assume two decimal places. External mock feeds remain USD-only.

The snapshot includes the source inquiry, service and route IDs, external service code, company
operation endpoints, server-side credential reference, reference values,
amount components, total minor units, currency scale, rounding rule, and
expiry. It contains no API key or payment-operator card token.

## Owned History and Detail

`GET /api/quotes` reads owner-only summaries from PayableServicesDb. Status,
service ID, and inclusive created-from/created-to filters combine with AND.
Date offsets normalize to UTC. Pagination defaults to page 1 with 20 rows;
page size is limited to 100 and page number to 1,000,000. Invalid values,
repeated/empty query values, and reversed date ranges return 400. Ordering is
CreatedAt descending, then ID descending, for deterministic ties.
The query includes only Unpaid/Expired rows with no SubmissionId or PaymentId.
Used, failed, paid, processing, and legacy Cancelled rows never reappear in this
customer list. Owned detail/status remains read-only access to stored history.

Summaries include the immutable total, expiry, reference values, inquiry ID,
and locally stored payment ID/lifecycle. Connection fields, credentials, and
internal pricing source metadata are omitted. Empty pages succeed. A stored
summary is not a live payment-status query; use the explicit execution endpoint
to reconcile the payment association/state.

Detail, history, and creation replay derive Expired for stored Unpaid quotes
without a PaymentId at `now >= ExpiresAt`. Reads never persist this projection,
reprice, contact the company, or call Processor. Processing and final states
are preserved. New creation uses five minutes; claimed submissions are never
expired by the original deadline. Display expiry does not establish safe reuse
of a possibly dispatched legacy execution.

## Confirmation

```text
Authenticated client
  -> POST /api/quotes/{quoteId}/confirm with CreditCardId only
  -> load quote by authenticated client
  -> acquire the quote's SQL application lock
  -> require unconsumed Unpaid version-1 quote and server time before ExpiresAt
  -> commit Submitted + submission ID/time + card + immutable package/fingerprint + lease
  -> release the SQL transaction; quote disappears from customer listing
  -> discover existing payment by ClientId + SourceQuoteId
  -> when absent, verify the bill against a fresh provider inquiry and original route
  -> persist verification separately from the creation inquiry and original submission
  -> submit the immutable intent and fresh evidence through authenticated SubmitVerifiedBill
  -> Processor atomically records rejection or accepts and reserves the bill key
  -> reacquire quote lock; only the current lease may save the result
```

The client cannot override amount, currency, route, service code, references,
or endpoints at confirmation.

A changed bill closes the quote as Invalidated. A matching bill with zero
balance closes as NoLongerDue. Other definitive rejections close as PaymentFailed.
None can return to Unpaid.
Technical same-card redelivery resumes/returns the original intent/result;
another card conflicts. Processor validates the timely server claim, including
the exact five-minute window, and can accept it after the original deadline.
The legacy ConfirmQuote RPC only replays existing accepted work; it cannot
authorize a new unclaimed payment. Card lifecycle rules remain in Processor;
see [Card Registration](card-registration.md).

The existing configured-price path uses authenticated SubmitClaimedQuote.
Bill-backed execution uses SubmitVerifiedBill so an older Processor cannot
silently accept an inquiry quote without verification and reservation.

## Status Refresh

`GET /api/quotes/{quoteId}/execution` retrieves the current Processor state and
updates the local quote. Lookup uses ClientId + SourceQuoteId even when local
PaymentId is missing or quote evidence has expired. This recovers an accepted
execution after its response or the local association commit was lost. It never
dispatches a payment or revalidates the company bill. For a consumed quote whose
payment is not yet found, or whose status lookup is unavailable, it reports the
original pending/final submission result. Unsubmitted legacy not-found remains
an operation error. Lookup cannot release a consumed quote or reset it to Unpaid.

`QuoteSubmissionRecoveryService` scans up to 100 due intents every ten seconds
using fresh scopes. One-minute SQL leases and response fencing allow restart
recovery without repeating a logical payment. Dispatch/status exceptions retain
Submitted and the immutable package; transient work schedules another attempt.
Recovery looks up accepted work before verifying. If Processor reports stale
evidence before the first company operation, recovery verifies again and
resubmits evidence for the same intent. Terminal results are cached durably,
and stale leases/responses cannot overwrite them. No SQL lock spans HTTP or gRPC.

The local transitions are:

```text
Unpaid
  -> Submitted        permanent atomic Pay claim before expiry
  -> Expired          no submission claim before expiry

Submitted
  -> Processing       Processor accepted the payment
  -> Paid             fast completion recovered or returned
  -> PaymentFailed    Processor durably rejected the attempt without a payment
  -> Invalidated      verification found changed bill evidence
  -> NoLongerDue       verification found zero remaining balance
  -> Submitted        dispatch/status unknown; recover the same intent

Processing
  -> Paid             company accepted and full card charge succeeded
  -> PaymentFailed    definitive decline or completed company rollback
  -> Invalidated      refreshed evidence found a changed bill before company payment
  -> NoLongerDue       refreshed evidence found zero remaining balance before company payment
  -> Processing       retry, reconciliation, rollback pending, or manual review
```

Manual-intervention details remain visible in the execution response; the quote
does not incorrectly advertise a safe retry while an external outcome is
unresolved.

## Idempotency

- Inquiry keys are unique per internal client ID for the record's lifetime.
- An identical key/input replays stored success, failure, expiry, or pending state
  without another provider call. Changed input conflicts; a new attempt requires
  a new key. Recovery reuses the separately stored provider key after lease expiry.
- One inquiry creates at most one quote. SQL locks, a unique `InquiryId` index,
  and one application-owned transaction protect concurrent consumption.
- Identical quote retries return the existing quote. Closed/expired attempts
  require a new inquiry to create a replacement.
- Processor creates at most one payment per `SourceQuoteId`.
- A deterministic fingerprint covers the immutable package and selected card.
- Identical repeats return the current payment with `replayed = true`.
- Any different package or card for the same source quote is a conflict.
- Database uniqueness handles concurrent duplicate confirmations.
- Processor's QuoteSubmissionOutcomes records acceptance or final rejection by
  unique SourceQuoteId/SubmissionId. A retry of a rejected request cannot later
  become accepted. The RPC reports whether that outcome is durably recorded;
  otherwise PayableServices retains pending state.
- Legacy unpaid quotes without a valid claim lifecycle became Expired. The
  bill-evidence migration invalidates still-unsubmitted inquiry-backed quotes
  without a bill identity. Existing snapshots/inquiry links are preserved and
  accepted work remains recoverable. Migration guards protect retained evidence.
- Deploy Processor's new RPC and migration first, then cut over all older
  PayableServices instances before exposing the new public Pay flow. An older
  Processor cannot silently ignore metadata because the submission RPC is new.

## Flow Invariants

- Processor never loads a second quote or recalculates the total from mutable
  catalogue data.
- Amount components may include value, taxes, fees, penalties, and discounts.
- The full quote total is paid, charged, refunded, or rolled back; components
  are never independent charges.
- The external service code identifies the company's selected service.
- External API keys remain in server configuration and are resolved from the
  persisted credential reference.
- Retry policy is snapshotted into the payment.
- The legacy Processor quote operations, aggregate, repositories, and tables
  are removed.

## Relevant Source

- [CreateQuoteUseCase](../../src/PayableServices/PayableServices.Application/UseCases/CreateQuoteUseCase.cs)
- [ConfirmQuoteUseCase](../../src/PayableServices/PayableServices.Application/UseCases/ConfirmQuoteUseCase.cs)
- [PaymentSwitchExecutionClient](../../src/PayableServices/PayableServices.Infrastructure/Clients/PaymentSwitchExecutionClient.cs)
- [AcceptConfirmedQuoteUseCase](../../src/PaymentSwitch/PaymentSwitch.Processor/Application/UseCases/AcceptConfirmedQuoteUseCase.cs)
- [ContinuePaymentExecutionUseCase](../../src/PaymentSwitch/PaymentSwitch.Processor/Application/UseCases/ContinuePaymentExecutionUseCase.cs)

## Related Documentation

- [Catalogue and amount inquiry](../requirements/mock-amount-inquiry.md)
  defines the implemented persisted-inquiry source and linked `QUOTE-08`.
- [Acceptance tests](../../tests/PayableServices.Tests/README.md)
- [Payment Lifecycle](payment-lifecycle.md)
- [Quote Execution Saga](../decisions/0003-quote-execution-saga.md)
- [Global Money Handling](../decisions/0004-money-handling.md)
