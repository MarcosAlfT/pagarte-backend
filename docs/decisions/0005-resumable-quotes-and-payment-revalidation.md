# ADR 0005: Five-Minute Quotes and Charge-First Confirmation

## Status

Accepted revised product/architecture direction; implementation pending.
This revision supersedes this ADR's original resumable-freshness proposal and
the company-first execution/compensation direction of [ADR 0003](0003-quote-execution-saga.md)
for future implementation. The file path is retained to preserve existing links.

## Context

The accepted product behavior is a short-lived, fixed payment summary. The client
can consult again after expiry instead of managing automatic refresh or linked
replacements. Payment notification to the company records a bill payment after
the card charge; it does not transfer money. Later settlement/reconciliation is
outside this application. The current code still executes the company-first saga.

## Decision

- PayableServices remains the sole quote owner. Quotes expire exactly five
  minutes after server creation; new Processor acceptance must precede expiry.
  Already accepted payment recovery/confirmation/refund continues after expiry.
- Expired/changed quotes require a new consultation and quote. No automatic
  refresh, replacement chain, or extra approval screen is required.
- Composite organizes quote items. Pricing Strategies calculate monetary leaves;
  service-specific key Strategies select authoritative identity items, including
  bill date/period where applicable. Snapshot selections/versions and prevent a
  new quote or Strategy version bypassing protection for the same obligation.
- Freeze the exact breakdown/total. Before a new Pay, verify the same unpaid bill,
  amount/currency, and route. Unchanged valid evidence proceeds from that Pay
  action without repricing. Changed evidence requires a new consultation.
- Processor charges the full total first and then notifies the company. Matching
  OK completes payment; definitive NOT OK requires full card refund. Unknown
  charge, company, or refund outcomes require durable reconciliation.
- Company confirmation carries bill identity/service amount separately from the
  charged total. It records payment but performs no money transfer.
- Processor owns compensation and bill-level duplicate protection. Coordinate
  Worker refunds through one durable refund identity to prevent double refund.
- Persist submission before dispatch, serialize it against cancellation, and
  recover by source quote ID even without local PaymentId. A not-found lookup
  alone cannot release an attempt that may still arrive.

## Consequences

The [requirement](../requirements/quotes.md) and
[target flow](../flows/quote-review-and-payment.md) replace the previous proposal.
The five-minute quote lifetime supersedes the inherited inquiry-expiry cap in
MAI-007/MAI-009 for future implementation; fresh evidence is still required at
creation and bill verification at Pay.

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
