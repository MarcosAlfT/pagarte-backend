# ADR 0005: Five-Minute Quotes and Charge-First Confirmation

## Status

Accepted revised product/architecture direction. Single-use submission, Unpaid
expiry, filtering, durable acceptance/rejection recovery, Development bill
verification, and cross-quote reservation are implemented. Composite pricing
and charge-first execution remain pending.
This revision supersedes this ADR's original resumable-freshness proposal and
the company-first execution/compensation direction of [ADR 0003](0003-quote-execution-saga.md)
for future implementation. The file path is retained to preserve existing links.
On 2026-11-05, explicit quote cancellation (QUOTE-06) was removed from scope;
submission ownership, fencing, and recovery remain required.
The same clarification makes a quote single-use at Pay: persist Submitted and
permanent consumption, then exclude that summary from the customer quote list.
The five-minute deadline applies only to the server's Unpaid -> Submitted claim;
verification, Processor acceptance, and recovery may continue afterward.

## Context

The accepted product behavior is a short-lived, fixed payment summary. The client
can consult again after expiry instead of managing automatic refresh or linked
replacements. Payment notification to the company records a bill payment after
the card charge; it does not transfer money. Later settlement/reconciliation is
outside this application. The current code still executes the company-first saga.

## Decision

- PayableServices remains the sole quote owner. Unconsumed Unpaid quotes expire
  five minutes after server creation. The server must atomically persist the
  Unpaid -> Submitted claim and bound SubmittedAt/intent before that deadline.
  Client tap time cannot backdate the claim. Submitted work continues through
  verification, Processor acceptance, execution, refund, and recovery afterward;
  original quote expiry is not a downstream processing cutoff.
- Expired/changed quotes require a new consultation and quote. No automatic
  refresh, replacement chain, or extra approval screen is required.
- Customers can leave an unsubmitted summary and let its original deadline
  expire. Leaving causes no backend mutation and does not revoke submitted
  payment work. No quote-cancellation endpoint is required; retain QUOTE-06
  and existing Cancelled history for traceability/compatibility.
- Atomically change Unpaid to Submitted with one durable intent before external
  work. Never restore that quote to Unpaid, including after verification failure
  or refund, and never expire it solely because its original deadline passes.
  Paid means confirmed charge plus matching company OK.
  Preserve backend status/history, hide consumed customer summaries, and recover
  only the original submission on technical redelivery. Any later eligible
  customer attempt requires a new inquiry/quote.
- Composite organizes quote items. Pricing Strategies calculate monetary leaves;
  service-specific key Strategies select authoritative identity items, including
  bill date/period where applicable. Snapshot selections/versions and prevent a
  new quote or Strategy version bypassing protection for the same obligation.
- Freeze the exact breakdown/total. Within the submitted Pay action and before
  charging, verify the same unpaid bill, amount/currency, and route. Unchanged
  valid evidence proceeds from that Pay
  action without repricing. Changed evidence requires a new consultation.
- Processor validates the timely server claim and its owner/snapshot/card
  binding instead of requiring its own acceptance before original quote expiry.
  Fresh bill evidence, card/route eligibility, technical timeouts, and retry
  policies still apply. Delayed/recovered work repeats stale verification for
  the original approved intent before charging; it cannot reopen the quote.
- Processor charges the full total first and then notifies the company. Matching
  OK completes payment; definitive NOT OK requires full card refund. Unknown
  charge, company, or refund outcomes require durable reconciliation.
- Company confirmation carries bill identity/service amount separately from the
  charged total. It records payment but performs no money transfer.
- Processor owns compensation and bill-level duplicate protection. Coordinate
  Worker refunds through one durable refund identity to prevent double refund.
- Persist and atomically claim submission before dispatch, fence attempts, and
  recover by source quote ID even without local PaymentId. A not-found lookup
  alone cannot close an attempt that may still arrive. Even terminal rejection
  does not make the consumed quote reusable.

## Consequences

The [requirement](../requirements/quotes.md) and
[target flow](../flows/quote-review-and-payment.md) replace the previous proposal.
The five-minute quote lifetime supersedes the inherited inquiry-expiry cap in
MAI-007/MAI-009 for future implementation; fresh evidence is still required at
creation and bill verification within the submitted Pay action. This deadline
limits initial quote submission only; independent execution policies govern
processing after the server has consumed the quote.

PayableServices, Processor, ExternalConnections, mock fixtures, and Worker refund
coordination need compatible changes. Drain or version current executions; do
not reinterpret company-first work using the new order. Preserve historical flat
snapshots without fabricating Strategy or bill evidence. Unexecuted legacy quotes
without the new contract require a new inquiry rather than being made refreshable.

A company rejection can leave a temporary card debit until the full refund
completes. Pre-charge verification alone does not prevent another channel paying
in the gap; company conflict detection and compensation handle that case.
Current-state documentation remains accurate until implementation and acceptance
tests demonstrate the revised behavior.
