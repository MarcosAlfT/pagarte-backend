# Payment Lifecycle

## Purpose

This document describes the current catalogue, quote, card-charge, company
delivery, and refund paths. The quote-to-charge path is not yet connected
end-to-end.

## Card Management

```text
Client
  -> Payments.Api HTTP
  -> PaymentSwitch.Processor CreditCardService gRPC
  -> payment-operator registration adapter
  -> PaymentDb
```

This path is the public credit-card boundary.

## Catalogue and Quote Boundary

Payable Services currently creates the public quote in `PayableServicesDb`,
but the confirmation handoff to Processor is disconnected. See
[Quote Lifecycle](quote-lifecycle.md) for the current failure and the proposed,
not-yet-implemented execution-package flow.

During catalogue sync, PayableServices currently calls Processor's
`ServiceCatalogService` and stores an external snapshot and mapping state.
That source is another internal catalogue, not the external company feed
described by the previous design.

## Processor Charge and Outbox Flow

When Processor receives the identifier of a quote that exists in its own
database:

1. Validate the quote, client, expiration, and selected credit card.
2. Resolve or register the operator-side card.
3. Create a payment and persist `ChargingCard`.
4. Charge the card synchronously.
5. On failure, persist the failed payment.
6. On success, mark the Processor quote paid and the payment card-charged.
7. Store a `PaymentRequestMessage` in the SQL outbox with the final state.
8. Publish pending outbox messages to RabbitMQ.

## Company Delivery and Refund

```text
payment.request
  -> PaymentSwitch.Worker
  -> company payment adapter
      -> accepted: mark payment completed and publish success work
      -> rejected: mark payment failed and publish refund.request

refund.request
  -> payment-operator refund adapter
      -> success: persist refunded status
      -> failure: persist retry schedule
      -> retries exhausted: publish alert
```

## Open Decisions

See [Current Problems](../current-problems.md) before changing this flow. The
team must select one catalogue owner, one customer-visible quote owner, and a
contract that carries a stable execution snapshot into Processor.
