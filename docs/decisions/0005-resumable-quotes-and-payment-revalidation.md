# ADR 0005: Resumable Quotes and Payment Revalidation

## Status

Accepted product/architecture direction on 2026-11-04; implementation pending.

## Context

A client may leave quote review to register a card. A short hard expiry forces
unnecessary repetition, while an unexpired quote does not prove the bill remains
unpaid. Current inquiry expiry caps and execution checks must evolve together.

## Decision

- Keep immutable quotes separate from inquiries and payment execution records.
- Replace quote-age rejection with resumable summaries and freshness metadata.
  Retain inquiry evidence expiry; revalidate before every new payment submission.
- Same unpaid bill and material price breakdown proceed from the existing Pay
  action. Changed evidence creates a linked replacement requiring approval.
- Use Composite to structure price components and Strategy to calculate them.
  Snapshot finalized monetary leaves and pricing versions; do not reprice reads
  or accepted executions.
- Require conditional company acceptance or equivalent bill duplicate protection
  in addition to request idempotency. Keep quote pricing in PayableServices and
  execution/compensation in Processor.
- Persist submission intent and fence it against cancellation/requote. Recover
  by source quote ID without depending on a locally saved payment ID.

## Consequences

The new [requirement](../requirements/quotes.md) and
[target flow](../flows/quote-review-and-payment.md) supersede hard quote expiry
semantics in MAI-007/MAI-009 for future implementation. Existing behavior remains
documented in the current flow until code, migrations, and acceptance tests change.
The execution contract, freshness validation, bill evidence, and company payment
adapter require coordinated changes. Provider support cannot be inferred from
the existing fixed mock balances. No accepted payment is stopped merely because
its quote evidence ages.
