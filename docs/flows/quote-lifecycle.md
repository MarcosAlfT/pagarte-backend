# Quote Lifecycle

## Purpose

This document describes how quotes are created and confirmed across
`PayableServices` and `PaymentSwitch.Processor`.

The current flow is disconnected. The proposed flow is explicitly marked as
not implemented and follows the approved quote-to-payment execution handoff
requirement.

## Current Ownership

- `PayableServices` owns the public catalogue and the customer-visible quote
  stored in `PayableServicesDb`.
- `PaymentSwitch.Processor` owns card charging, payments, and the SQL outbox in
  `PaymentDb`.
- Processor currently also has a separate `PaymentQuote` aggregate. This
  duplicates quote ownership and causes the confirmation failure described
  below.

## Current Quote Creation

```text
Client
  -> POST /api/quotes
  -> PayableServices.Api
  -> CreateQuoteUseCase
      -> load PayableService from PayableServicesDb
      -> validate service availability and currency
      -> create Quote and QuoteItem
      -> persist in PayableServicesDb
  -> return customer-visible quote
```

The persisted quote currently snapshots the service identifier and name,
currency, amount, expiration, and one quote item. It does not yet snapshot all
route, external reference, and downstream execution data required by the
target payment handoff.

## Current Confirmation Failure

```text
PayableServicesDb Quote ID
  -> PayableServices.ConfirmQuoteUseCase
  -> PaymentExecutionService.ConfirmQuote gRPC
  -> Processor ConfirmPaymentQuoteUseCase
  -> search PaymentDb.PaymentQuotes using the Payable Services Quote ID
  -> quote normally not found
```

No code copies the Payable Services quote into Processor's quote table. The
identifiers belong to independent databases and cannot be assumed to match.

The confirmation use case is registered in Payable Services, but no HTTP
controller currently exposes it. Adding only that endpoint would expose the
failure without fixing the handoff.

## Proposed Flow - Not Implemented

The selected solution keeps one customer-visible quote owner and sends an
immutable execution package to Processor.

```text
Authenticated client
  -> confirm Quote ID and selected CreditCard ID
  -> PayableServices.Api
      -> obtain Client ID from the access token
      -> load the persisted Quote and its execution snapshot
      -> validate owner, status, expiration, route, and totals
      -> build immutable ConfirmedQuoteExecution package
  -> PaymentSwitch.Processor gRPC
      -> find an existing Payment by SourceQuoteId
          -> found: return the existing result without charging again
          -> not found: create Payment and persist execution snapshot
      -> validate the selected card belongs to the client
      -> charge the card synchronously
          -> declined: persist failed Payment and return definitive failure
          -> accepted: persist charged Payment and SQL outbox atomically
  -> PayableServices
      -> accepted: mark Quote Paid
      -> declined: mark Quote PaymentFailed
      -> unknown/transient result: do not record a definitive decline
  -> return the immediate payment result to the client
```

## Execution Package Boundary

The package sent to Processor must be created exclusively from the persisted
Payable Services quote snapshot. The confirmation request from the client must
not be allowed to override monetary or routing information.

The package contains the source quote and client identifiers, selected card,
expiration, currency, exact total, service and item snapshots, payment-route
snapshot, and external reference values required for downstream execution.

API endpoints and credentials are not part of the package. External adapters
resolve secrets from server-side configuration.

## Idempotency

`SourceQuoteId` is the idempotency key at the Processor boundary.

- Processor creates at most one payment for a source quote.
- Repeating confirmation after a timeout returns the existing payment result.
- A repeated request must not charge the card or create the outbox message
  twice.
- Processor enforces the rule with persistence-level uniqueness in addition to
  application validation.

## Quote State Outcomes

```text
Unpaid
  -> Paid            when Processor confirms a successful card charge
  -> PaymentFailed   after a definitive card decline
  -> Expired         when confirmation occurs after expiration
```

A transport timeout or unknown Processor outcome is not equivalent to a card
decline. The caller retries with the same source quote identifier so Processor
can return the idempotent result.

## Flow Invariants

- Payable Services remains the only owner of quote status and quote history.
- Processor does not require a local `PaymentQuote` to execute the package.
- Processor does not recalculate the amount from its mutable catalogue.
- Processor stores the values used for the charge in the payment snapshot.
- The payment snapshot supplies the data used to create post-charge outbox
  work.
- A successful charge and its outbox message are committed together.
- Documentation must continue to describe this as proposed until the code and
  automated tests implement the flow.

## Relevant Source

- [`ConfirmQuoteUseCase`](../../src/PayableServices/PayableServices.Application/UseCases/ConfirmQuoteUseCase.cs)
- [`PaymentSwitchExecutionClient`](../../src/PayableServices/PayableServices.Infrastructure/Clients/PaymentSwitchExecutionClient.cs)
- [`PaymentExecutionGrpcService`](../../src/PaymentSwitch/PaymentSwitch.Processor/GrpcServices/PaymentExecutionGrpcService.cs)
- [`ConfirmPaymentQuoteUseCase`](../../src/PaymentSwitch/PaymentSwitch.Processor/Application/UseCases/ConfirmPaymentQuoteUseCase.cs)
- [`PaymentRequestOutbox`](../../src/PaymentSwitch/PaymentSwitch.Processor/Infrastructure/Outbox/PaymentRequestOutbox.cs)

## Related Documentation

- [Payment Lifecycle](payment-lifecycle.md)
- [PayableServices Context](../contexts/payable-services.md)
- [PaymentSwitch.Processor Context](../contexts/payment-switch-processor.md)
- [Current Problems](../current-problems.md)
