# Quote Lifecycle

## Purpose

This document describes current implementation. The accepted
[review/expiry/payment flow](quote-review-and-payment.md) and
[Quotes requirement](../requirements/quotes.md) define pending changes:
five-minute quote expiry, Composite/Strategy pricing and business-key selection,
and bill verification before Pay without repricing. Expired/changed quotes
require a new consultation. Execution will charge first, then notify the company
and complete on OK or fully refund on definitive NOT OK.

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
  -> replace configured base components exactly once
  -> finalize each amount component with ISO scale and ToEven rounding
  -> checked-sum the full positive total
  -> atomically consume inquiry and persist one immutable quote in PayableServicesDb
  -> cap quote expiry at the effective inquiry expiry
```

Legacy services without `RequiresInquiry` retain configured-amount quoting.
Inquiry-backed services reject direct quoting or supplied currency/reference
overrides. Zero balances and failure outcomes cannot create quotes.
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

Summaries include the immutable total, expiry, reference values, inquiry ID,
and locally stored payment ID/lifecycle. Connection fields, credentials, and
internal pricing source metadata are omitted. Empty pages succeed. A stored
summary is not a live payment-status query; use the explicit execution endpoint
to reconcile the payment association/state.

Detail, history, and creation replay derive Expired for stored Unpaid quotes
without a PaymentId at `now >= ExpiresAt`. Reads never persist this projection,
reprice, contact the company, or call Processor. Processing and final states
are preserved. Existing stored deadlines still apply; fixed five-minute creation
is pending. Display expiry does not prove an uncertain submission cannot exist.

## Confirmation

```text
Authenticated client
  -> POST /api/quotes/{quoteId}/confirm with CreditCardId only
  -> load quote by authenticated client
  -> reject closed or expired quote
  -> build execution package only from the persisted snapshot
  -> call authenticated Processor PaymentExecutionService
  -> record returned PaymentId and current execution state
```

The client cannot override amount, currency, route, service code, references,
or endpoints at confirmation.

A confirmation rejected before Processor creates a payment, such as a foreign
client's stored card, leaves the quote available for a corrected confirmation.
Processor also rejects deleted, unverified, expired, or unsupported-market cards
before creating a new execution. Card references and lifecycle state stay in
Processor; see [Card Registration](card-registration.md) for removal/acceptance
ordering and real-provider acceptance limits.
Once an execution exists, a definitive decline closes the quote and another
attempt requires a new quote even when all values are unchanged.

## Status Refresh

`GET /api/quotes/{quoteId}/execution` retrieves the current Processor state and
updates the local quote. Lookup uses ClientId + SourceQuoteId even when local
PaymentId is missing or quote evidence has expired. This recovers an accepted
execution after its response or the local association commit was lost. It never
dispatches a payment or revalidates the company bill. Not-found/unavailable
results do not clear a saved association or establish safe cancellation/retry.
Durable submission intent, automatic restart recovery, and confirmation-time
recovery before expiry rejection remain pending.

The local transitions are:

```text
Unpaid
  -> Processing       durable payment execution accepted
  -> Expired          confirmation after expiry

Processing
  -> Paid             company accepted and full card charge succeeded
  -> PaymentFailed    definitive decline or completed company rollback
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
