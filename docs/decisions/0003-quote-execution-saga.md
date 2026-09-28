# ADR 0003: Quote Execution Saga and Legacy Quote Removal

## Status

Accepted and implemented.

## Context

Before this decision was implemented, PayableServices and
PaymentSwitch.Processor owned separate quote aggregates. A PayableServices
quote identifier was sent to Processor, which looked for the same identifier
in its own database. The independently generated records did not match, so the
public confirmation flow was disconnected.

The former payment sequence charged the card before asking the company to pay
the selected service. The accepted business sequence requires the company
payment to be accepted before the final card charge. A failed final charge must
be retried durably and, after the configured attempt cap is exhausted, the
company payment must be rolled back.

## Decision

- PayableServices is the sole owner of the customer-visible quote.
- One quote represents one payable service and one payment attempt.
- A quote may contain multiple amount components, including the service value,
  taxes, fees, penalties, and discounts. The card charge and any rollback or
  refund apply to the full quote total rather than to individual components.
- PayableServices sends Processor an immutable execution snapshot. Processor
  does not load or own a second quote and does not recalculate the total from
  mutable catalogue data.
- The execution order is:

  1. Validate the authenticated client, quote, execution package, and card
     ownership.
  2. Ask the payment operator whether the selected stored card is usable for
     the payment without recording a successful final charge.
  3. Ask the company to pay the external service using the persisted service
     code and reference values.
  4. After company acceptance, charge the card for the full quote total.
  5. Retry a non-definitive failed charge according to a durable retry policy.
  6. If the charge does not succeed before the persisted attempt cap, call the
     company rollback operation for the full service payment.

- The default retry interval may come from configuration, but the effective
  interval, maximum attempts, attempts made, and next-attempt time are stored
  with the payment so an in-flight workflow is stable and survives restarts.
- Company payment, card charge, and company rollback calls use stable
  idempotency keys and durable outcomes. Unknown external outcomes are
  reconciled before another side-effecting call is attempted.
- A definitive decline closes the quote as `PaymentFailed`. Trying again,
  including with the same values or another card, requires a newly created
  quote and therefore a new source quote identifier.
- Processor authenticates the PayableServices workload before accepting an
  execution package. Public quote operations derive the client identifier from
  the access token. A stored card owned by another client is rejected.
- The legacy Processor-owned `CreatePaymentQuote`, `ConfirmPayment`,
  `PaymentQuote`, and `PaymentQuoteDetail` flow is removed rather than retained
  as a compatibility path.

## Consequences

- Confirmation can return an in-progress result because company payment, card
  retries, and rollback may outlive the original HTTP request.
- Payment persistence needs execution, company-payment, card-charge, retry,
  and rollback states in addition to the immutable quote snapshot.
- The external company contract must support payment and rollback operations,
  each with stable idempotency and reconciliation semantics.
- Payment-operator contracts must distinguish card readiness, definitive
  decline, retryable failure, unknown outcome, and successful charge.
- Processor schedules the saga from persisted payment state. Each due payment
  is executed in an isolated scope and claimed through a row-version-protected
  transition; card tokens and credentials do not enter RabbitMQ.
- SQL outbox or an equivalent durable dispatcher remains required for each
  asynchronous transition, but it is no longer specifically a post-charge
  company-delivery mechanism.
- Removing the legacy gRPC operations and tables is a breaking contract and
  schema change. The migration requires legacy active payments and pending
  payment-request outbox records to be drained before deployment.

## Related Documentation

- [Quote Lifecycle](../flows/quote-lifecycle.md)
- [Payment Lifecycle](../flows/payment-lifecycle.md)
- [Global Money Handling](0004-money-handling.md)
- [Current Problems](../current-problems.md)
