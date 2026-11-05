# Quote Review, Expiry, and Payment Confirmation

## Status

Accepted revised target. The current quote and payment flows document the
implemented single-use submission, Development bill verification and reservation,
and company-first execution. Composite pricing and the charge-first flow below
remain pending. See the [Quotes requirement](../requirements/quotes.md),
[current quote flow](quote-lifecycle.md), and [current payment flow](payment-lifecycle.md).

## Customer Flow

```text
Consult bill -> create quote -> review fixed service amount + fees/taxes = total
  -> optional card registration
  -> Pay with quote ID and selected Pagarte card ID
      -> check server deadline and atomically Unpaid -> Submitted
      -> permanently consume and hide summary; persist bound SubmittedAt/intent
      -> recover the same submission on technical request redelivery
      -> verify the unpaid bill; continue even after the original quote deadline
          -> unchanged: charge the displayed total without extra approval
          -> changed bill: consult again and create a new quote
          -> paid / nothing due: stop without charging
          -> verification unavailable: fail the consumed attempt safely; consult again for any new attempt
```

Unconsumed Unpaid quotes expire exactly five minutes after server-side creation.
PayableServices must atomically claim Pay before that deadline. At the deadline,
an unclaimed quote rejects Pay. Once Submitted, verification, Processor acceptance,
execution, and recovery continue independently of the original quote deadline.
Fresh bill checks and operation timeout/retry policies still apply; delayed or
recovered work obtains fresh evidence when required before charging. There is
no automatic quote refresh or linked replacement. Listing, retrieval, and card
registration never charge.
Leaving the summary without Pay causes no backend mutation. Its original
deadline remains in effect; explicit quote cancellation is out of scope.
Leaving after Pay does not revoke accepted or uncertain execution.
The quote never returns to Unpaid after submission, including after verification
failure or refund. A later customer attempt requires a new inquiry and quote.
Technical redelivery/recovery belongs to the original submission only.

Example: an Unpaid USD 102 quote created at 10:00 expires at 10:05. Pay claimed
by the server at 10:04:59 changes it to Submitted; verification and Processor
acceptance at 10:05:02 can continue. A first Pay claim at exactly 10:05 is rejected.
Returning from card registration at 10:12 without submission requires a new
inquiry and quote. A Submitted quote never reopens or expires from that deadline.

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

The reviewed pricing snapshot stays fixed before submission and throughout
execution. New pricing configuration does not reallocate fees/taxes or change
the total at Pay.
Changed bill amount/currency requires a new consultation even if the key matches.
An invalid route blocks payment; the backend cannot silently change destination.

## Component and Payment Flow

```text
Mobile -> PayableServices: quote ID + Pagarte card ID
PayableServices: before ExpiresAt, atomically Submitted + bound SubmittedAt/intent
  -> exclude consumed summary from customer quote list permanently
  -> recover original intent on technical redelivery; never reopen quote
  -> CompanyInquiries adapter: fresh unpaid-bill evidence
  -> compare key, bill amount, currency, and original route; no repricing
  -> Processor: immutable snapshot + server submission + verification/key evidence
Processor: validate timely server claim; atomic acceptance and bill-key protection
  -> original quote deadline is not a new acceptance or execution cutoff
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

## Recovery and Status

Use separate stable operation keys for charge, notification, and refund. A lost
response is unknown, not rejection. Recover status or replay the same operation
safely; never create a second charge to recover a notification failure. Exhausted
unknown outcomes remain unresolved with operational attention, not guessed success
or an automatic refund.

Technical duplicate requests claim or replay one durable intent. Only its current
claimed attempt may dispatch. A missing local PaymentId, expired worker lease,
or Processor not-found lookup does not prove a delayed submission cannot arrive.
Recover by source quote ID and require durable rejection of delayed attempts
before closing a dispatched attempt as non-accepted. Only a new inquiry/quote
can authorize a later customer attempt; the original quote stays consumed.
Navigating away does not revoke a submission; no quote-cancellation endpoint
is required.

Client payment statuses are Processing, Succeeded, Failed, RefundPending, and
Refunded. Expiry/changed bill/verification errors before payment are ordinary
operation results, not extra approval or freshness workflows. Accepted or
unresolved payment prevents another payment for the same bill, even from a new
quote; successful payments remain protected while external balances lag.

Quote status changes separately: Unpaid -> Submitted -> Processing -> Paid on
confirmed success. Verification can close Submitted as PaymentFailed,
Invalidated, or NoLongerDue. Original quote expiry applies only before Submitted;
it never terminates this submission. Company rejection after charge moves it
through RefundPending to Refunded. No consumed quote transitions
back to Unpaid. Persist consumption independently of payment association so a
missing PaymentId or failure before execution cannot make the quote reusable.

## Implementation Boundary

The current company-first saga must be drained or explicitly versioned during
cutover. Existing active payments retain their original order/evidence. Legacy
unexecuted quotes lacking the new identity/expiry contract require new inquiry;
expired quotes are never reopened. See the requirement for acceptance scenarios
and migration rules.
