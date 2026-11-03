# PayableServices

## Purpose

`PayableServices` owns the customer-visible payable-service catalogue, payment
routes, external catalogue mappings, and quotes.

## Owns

- Countries, categories, subcategories, providers, and payment networks.
- Payable services, reference-field definitions, and amount compositions.
- External catalogue sources, items, and mappings.
- Payment routes, including company service codes and server-side credential
  references.
- Quotes, reference values, finalized amount components, and quote status.
- Authenticated public catalogue and quote HTTP endpoints.

## Does Not Own

- Stored credit cards.
- Payment-operator selection or card charging.
- Durable payment execution, company rollback, or payment records.
- External credentials; a quote stores only a credential reference.

## Runtime Boundary

```text
authenticated client
  -> PayableServices.Api
  -> PayableServices.Application
  -> PayableServicesDb
  -> authenticated PaymentSwitch.Processor gRPC execution boundary
```

## Quote-to-Execution Flow

```text
Create quote
  -> resolve validated token `(iss, sub)` through ClientProfiles
  -> validate service, route, references, currency, and amount components
  -> persist immutable one-service Quote snapshot
  -> Quote = Unpaid

Confirm quote with Quote ID + Card ID
  -> load Quote for authenticated ClientId
  -> reject missing, foreign, closed, or expired Quote locally
  -> build execution package only from persisted snapshot + selected Card ID
  -> call Processor with the PayableServices workload credential
  -> persist returned Payment ID, status, and safe error information

Refresh execution
  -> query Processor by ClientId + SourceQuoteId
  -> Completed ------------------------------> Quote = Paid
  -> final decline / completed rollback -----> Quote = PaymentFailed
  -> retry / reconciliation / rollback ------> Quote = Processing
  -> manual intervention --------------------> Quote = Processing + details
```

A pre-execution rejection that creates no Processor payment leaves the quote
available for a corrected confirmation. Once Processor has created an
execution, a final decline consumes the quote; another payment attempt requires
a newly persisted quote even when the application hides that recreation from
the user.

## Main Use Cases

- `GetCatalogueUseCase` reads the local catalogue.
- `CreateQuoteUseCase` validates reference fields, route completeness, currency,
  and amount composition before persisting an immutable execution snapshot.
- `GetQuoteUseCase` returns a quote only to its owning client.
- `ConfirmQuoteUseCase` submits that snapshot with the selected stored-card ID
  and records the returned execution state.
- `SyncExternalCatalogueUseCase` and `ActivatePaymentRouteUseCase` manage
  catalogue integration state.

## Domain Rules

- Public quote operations require `VerifiedClient` and receive the internal
  `Client.Id` from the protected ClientProfiles resolver. A missing mapping
  returns `403 PROFILE_REQUIRED`; resolver failure returns 503 before any
  Processor call.
- Catalogue sync and payment-route activation require `VerifiedAdmin`.
- A service must be active and allow quoting and payment.
- One quote represents one payable service and one payment attempt.
- A quote can contain multiple finalized amount components, but its full total
  is the unit charged and refunded or rolled back.
- The quote snapshots the service, route, company endpoints, external service
  code, reference values, amount components, currency rules, total, and expiry.
- The current handoff copies the snapshotted company endpoints and credential
  reference into Processor. It does not copy the API key. Replacing those
  connection fields with server-side resolution by stable route identifiers is
  tracked in [Current Problems](../current-problems.md).
- The client cannot override snapshot values during confirmation.
- A declined execution closes the quote as `PaymentFailed`; another attempt
  requires a newly created quote.
- A rejected confirmation that did not create a payment, such as selecting a
  card owned by another client, does not consume the quote.

## Data Ownership

`PayableServices` owns `PayableServicesDb` and is the sole quote owner.
Processor receives and stores a payment execution snapshot, not another quote.

## Integrations

- `PaymentSwitchExecutionClient` authenticates to Processor and calls
  `ConfirmQuote` or `GetExecutionStatus`.
- `CompanyPaymentsClient` currently reads Processor's legacy internal service
  catalogue for catalogue sync; replacing that source remains backlog work.
- `Utilities.Money` supplies the shared ISO currency and minor-unit rules.

## Change Guidance

The proposed [catalogue and amount-inquiry requirement](../requirements/mock-amount-inquiry.md)
covers search, service details, mock catalogue synchronization, route checks,
and persisted customer balances. It preserves PayableServices ownership and
links inquiry results to quotes. These additions are not implemented yet.

Read [Quote Lifecycle](../flows/quote-lifecycle.md),
[Payment Lifecycle](../flows/payment-lifecycle.md),
[ADR 0003](../decisions/0003-quote-execution-saga.md), and
[ADR 0004](../decisions/0004-money-handling.md) before changing quote or
execution behavior.
