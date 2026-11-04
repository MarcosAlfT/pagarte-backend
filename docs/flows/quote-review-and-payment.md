# Quote Review, Expiry, and Payment Confirmation

## Status

Accepted revised target; implementation pending. This replaces the previous
resumable-freshness/replacement proposal. The [Quotes requirement](../requirements/quotes.md)
is authoritative. The [current quote flow](quote-lifecycle.md) and
[current payment flow](payment-lifecycle.md) describe the implemented baseline.

## Customer Flow

```text
Consult bill -> create quote -> review fixed service amount + fees/taxes = total
  -> optional card registration
  -> Pay with quote ID and selected Pagarte card ID
      -> recover an existing/uncertain submission first
      -> otherwise require unexpired quote and verify the unpaid bill
          -> unchanged: charge the displayed total without extra approval
          -> changed / expired: consult again and create a new quote
          -> paid / nothing due: stop without charging
          -> verification unavailable: safe error; retry only before expiry
```

Quotes expire exactly five minutes after server-side creation. New Processor
acceptance must occur before that deadline. At the deadline, new payment is
rejected; accepted payments keep running. There is no automatic refresh or
linked replacement. Listing, retrieval, and card registration never charge.

Example: a quote created at 10:00 for USD 102 expires at 10:05. Returning from
card registration at 10:04 permits Pay after verification; returning at 10:12
requires a new inquiry and quote. A payment accepted at 10:04 continues to
completion or refund even after 10:05.

## Quote Items and Business Identity

Composite organizes monetary and reference/identity items. Pricing Strategies
calculate monetary leaves; a separate service-specific key Strategy selects
the items that identify the obligation. Snapshot selected names/values and
Strategy versions. Nonmonetary identity items do not contribute to totals.

For electricity, the key might be company + service + account + billing period.
Changing any selected key value identifies a different obligation. The billing
date/period is authoritative bill evidence, not the time of Pay or consultation.
Technical QuoteId and request idempotency keys remain separate from this key.
A new quote or Strategy version must not bypass duplicate protection.

The reviewed pricing snapshot stays fixed during its five-minute life. New
pricing configuration does not reallocate fees/taxes or change the total at Pay.
Changed bill amount/currency requires a new consultation even if the key matches.
An invalid route blocks payment; the backend cannot silently change destination.

## Component and Payment Flow

```text
Mobile -> PayableServices: quote ID + Pagarte card ID
PayableServices: claim submission against cancellation; recover prior submission
  -> CompanyInquiries adapter: fresh unpaid-bill evidence
  -> compare key, bill amount, currency, and original route; no repricing
  -> Processor: immutable execution snapshot + verification/key evidence
Processor: atomic idempotent acceptance before expiry and bill-key protection
  -> operator: check card; charge complete quote total
      -> definitive failure: Failed; no company notification
      -> unknown: reconcile same charge; do not notify yet
      -> confirmed charge:
          -> company adapter: send payment confirmation (no funds transfer)
              -> matching OK: Succeeded
              -> definitive NOT OK: refund full card charge
                  -> confirmed refund: Refunded
                  -> pending/unknown/failure: RefundPending; recover/escalate
              -> unknown/transient: Processing; reconcile/retry notification
Processor -> PayableServices -> Mobile: payment status
```

Processor owns durable orchestration and rejection compensation. The existing
Worker refund path must coordinate with the same durable refund operation so
one charge cannot be refunded twice. Adapters normalize evidence; they do not
choose compensation or update application state.

## Amounts and Company Response

For a USD 100 bill plus USD 2 fee, charge USD 102 and store the full breakdown.
Notify the company about the USD 100 bill using its business key, Pagarte payment
reference, charge evidence, and a stable notification key. Service amount and
charged total are distinct fields; company settlement happens elsewhere.

Company OK acknowledges this exact payment and records the bill as paid.
Definitive NOT OK requires a refund of the entire USD 102, including the fee.
Do not report success at card charge alone, or report refunded before the
operator confirms the refund. A retry of an accepted notification must return
its original acknowledgment rather than reject it as another payment.

A read-before-charge check cannot eliminate another channel paying in the gap.
The company must detect that conflict when notified, reject the new payment,
and trigger compensation. This does not promise that no temporary charge occurs.

## Recovery, Cancellation, and Status

Use separate stable operation keys for charge, notification, and refund. A lost
response is unknown, not rejection. Recover status or replay the same operation
safely; never create a second charge to recover a notification failure. Exhausted
unknown outcomes remain unresolved with operational attention, not guessed success
or an automatic refund.

Pay and Cancel have one durable winner. Cancel before submission keeps history;
once submission is claimed, cancellation is blocked until safe non-acceptance is
established. A missing local PaymentId, expired worker lease, or Processor
not-found lookup does not prove a delayed submission cannot arrive. Recover by
source quote ID and require durable rejection of delayed attempts before release.

Client payment statuses are Processing, Succeeded, Failed, RefundPending, and
Refunded. Expiry/changed bill/verification errors before payment are ordinary
operation results, not extra approval or freshness workflows. Accepted or
unresolved payment prevents another payment for the same bill, even from a new
quote; successful payments remain protected while external balances lag.

## Implementation Boundary

The current company-first saga must be drained or explicitly versioned during
cutover. Existing active payments retain their original order/evidence. Legacy
unexecuted quotes lacking the new identity/expiry contract require new inquiry;
expired quotes are never reopened. See the requirement for acceptance scenarios
and migration rules.
