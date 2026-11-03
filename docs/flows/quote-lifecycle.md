# Quote Lifecycle

## Purpose

PayableServices is the sole customer-visible quote owner. One quote represents
one payable service and one payment attempt.

## Creation

```text
Authenticated client
  -> POST /api/quotes
  -> resolve `(iss, sub)` through ClientProfiles to internal Client.Id
  -> load active payable service, country, route, amount composition,
     and reference-field definitions
  -> validate supplied reference values
  -> finalize each amount component with ISO scale and ToEven rounding
  -> checked-sum the full positive total
  -> persist immutable quote execution snapshot in PayableServicesDb
```

The snapshot includes the service and route IDs, external service code, company
operation endpoints, server-side credential reference, reference values,
amount components, total minor units, currency scale, rounding rule, and
expiry. It contains no API key or payment-operator card token.

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
Once an execution exists, a definitive decline closes the quote and another
attempt requires a new quote even when all values are unchanged.

## Status Refresh

`GET /api/quotes/{quoteId}/execution` retrieves the current Processor state and
updates the local quote:

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

- [Payment Lifecycle](payment-lifecycle.md)
- [Quote Execution Saga](../decisions/0003-quote-execution-saga.md)
- [Global Money Handling](../decisions/0004-money-handling.md)
