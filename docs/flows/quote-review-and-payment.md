# Quote Review, Resume, and Payment Revalidation

## Status

Accepted target flow on 2026-11-04; not implemented. The
[current quote lifecycle](quote-lifecycle.md) still uses expiry rejection.
Implementation is specified in [Quotes](../requirements/quotes.md).

## Customer Flow

```text
Consult bill -> review saved payment summary
  -> optionally leave to register a card or perform another task
  -> retrieve the same quote and review it again
  -> select an available stored card and tap Pay
      -> recover any previous submission first
      -> for a new submission, verify the same bill and current pricing
          -> unchanged and unpaid: proceed without another review step
          -> changed: show linked replacement and request approval
          -> already paid / nothing due: explain and stop
          -> verification unavailable: preserve quote and offer retry
```

There is no five-minute customer cutoff. Evidence expiry means NeedsRefresh,
not lost work. Reads do not initiate payment. Every new Pay submission verifies
the bill, even if the quote still appears fresh.

## Component Flow

```text
Mobile -> PayableServices: owned quote ID + selected Pagarte card ID
PayableServices: reserve/fence submission against cancellation and replacement
  -> Processor: discover existing execution by source quote ID if uncertain
  -> CompanyInquiries adapter: fresh bill status, identity, amount, version
  -> pricing Strategies calculate Composite candidate breakdown
  -> same material summary: append validation evidence; keep original quote
  -> changed summary: persist replacement; wait for explicit customer approval
PayableServices -> Processor: approved immutable snapshot + bound validation
Processor: idempotent acceptance, selected-card eligibility/readiness
  -> company adapter: conditional payment of the verified bill/version
      -> already paid/changed: no operator charge
      -> unknown: reconcile; do not assume failure
      -> accepted: existing full-total card charge and compensation flow
Processor -> PayableServices: processing/final execution state
```

Fresh balance verification is read-only and does not reserve the bill by itself.
The company payment boundary must prevent a payment made elsewhere between
verification and acceptance. Same-key idempotency alone does not cover another
channel's payment. Missing provider support is an explicit integration blocker.

## Preserved Evidence and Pricing

- The original inquiry and reviewed quote are immutable evidence. Revalidation
  creates a separate record and never changes the old total or expiry in place.
- The same unpaid obligation and unchanged material breakdown allow the original
  Pay action to continue. A different bill period, price, or currency requires
  review of a linked replacement, even when an account number is unchanged.
- Composite groups organize components; Strategies calculate leaves. Finalized
  leaves contribute once. Processor copies the approved total without repricing.
- Technical validation deadlines bound new execution acceptance. They are
  refreshed transparently and never terminate already accepted payment work.
- No-longer-due evidence does not imply Pagarte charged the client. Only its
  own successful payment can produce a Pagarte Paid result/payment association.

## Resume, Cancel, and Recovery

The mobile app retains the quote ID while adding a card and restores it after
navigation/login. Registering a card does not charge it or change the quote.
An owned quote list provides recovery if the app loses its local navigation state.

Cancel is allowed only when no outstanding submission can take effect. Pending
or unknown execution blocks cancellation and requote. Cross-instance fencing
prevents delayed workers submitting after cancellation. Lost acceptance responses
are recovered by source quote ID even if local PaymentId was never committed.

Example: a USD 102 quote remains accessible after a 12-minute card registration.
If the bill and breakdown remain unchanged, Pay produces one USD 102 payment.
If the total is now USD 105, the client approves a new quote first. If the bill
was paid through a bank, Pagarte explains that nothing remains payable.
