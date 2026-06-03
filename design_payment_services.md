# PaymentServices Design

`PaymentServices` is the bounded context that owns the service catalogue and payment quote workflow. It was extracted from the old Pagarte payment surface so the public API can stay focused on client-facing card operations while catalogue management and quote orchestration live in their own module.

## Scope

`PaymentServices` owns:

- Catalogue data for payable services.
- Country, category, subcategory, provider, payment network, route, and reference-field modeling.
- External catalogue sync and mapping management.
- Quote creation and quote persistence.
- Quote confirmation orchestration.
- The payment-execution boundary used to confirm a quote through Pagarte.

`Pagarte.API` no longer exposes service browsing or payment quote endpoints. The public API keeps the credit card surface only.

## Runtime Shape

```text
PaymentServices.Api
  -> PaymentServices.Application
  -> PaymentServices.Domain
  -> PaymentServices.Infrastructure
  -> PaymentServices.Persistence
  -> Pagarte.Contracts
  -> ExternalConnections.CompanyPayments
  -> Pagarte.Services (payment execution gRPC)
```

`PaymentServices.Api` is the HTTP boundary. It validates request shape, calls focused application use cases, and returns HTTP responses.

`PaymentServices.Application` owns use cases, DTOs, and abstractions. It must not depend on EF Core or ASP.NET `HttpContext`.

`PaymentServices.Domain` owns the catalogue and quote entities, state transitions, and domain rules.

`PaymentServices.Infrastructure` implements technical adapters, including the `ICompanyPaymentsClient` bridge to the current Pagarte catalogue feed and the payment-execution gRPC client used during quote confirmation.

`PaymentServices.Persistence` owns `PaymentServicesDbContext`, EF configurations, repositories, migrations, and seed data.

## Catalogue Sync

The first bridge into the existing Pagarte catalogue feed stays behind `ICompanyPaymentsClient`.

`SyncExternalCatalogueUseCase` pulls the source catalogue from the current Pagarte feed, stores the external source snapshot, and maps it into the local `PaymentServices` model. This allows the new bounded context to own its own read model and route metadata without forcing the old Pagarte catalogue surface to remain public.

The sync flow is intentionally separate from the public API. `PaymentServices` owns the catalogue state; Pagarte is only the current upstream feed.

## Quote Flow

Payment is a two-step flow:

1. `CreateQuote` builds a persisted quote from a payable service and currency.
2. `ConfirmQuote` validates the quote, validates the selected credit card, and calls the payment-execution boundary to charge synchronously.

The quote is the user-visible pricing record. The payment execution happens only after quote confirmation.

## Payments Boundary

`PaymentServices` does not charge cards directly. It calls `Pagarte.Services` through the `PaymentExecutionService` gRPC contract to confirm a quote.

This keeps the new bounded context aligned with the split:

- `PaymentServices` owns catalogue and quote orchestration.
- `Pagarte.Services` owns card charging and payment execution.
- `Pagarte.Engine` owns the async company-delivery workflow after a card is charged.

## External Connections

The old `Pagarte.Connections` assembly has been replaced by `ExternalConnections`.

`ExternalConnections.CardOperators` owns payment-operator adapters and the registration/charge/refund abstractions.

`ExternalConnections.CompanyPayments` owns the company-payment adapter used by the engine and the catalogue sync bridge.

The connection layer is now shared infrastructure for internal workers rather than a public Pagarte API concern.
