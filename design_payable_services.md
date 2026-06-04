# PayableServices Design

`PayableServices` is the bounded context that owns the service catalogue and payment quote workflow. It keeps catalogue management and quote orchestration separate from the client-facing card API.

## Scope

`PayableServices` owns:

- Catalogue data for payable services.
- Country, category, subcategory, provider, payment network, route, and reference-field modeling.
- External catalogue sync and mapping management.
- Quote creation and quote persistence.
- Quote confirmation orchestration.
- The payment-execution boundary used to confirm a quote through `PaymentSwitch.Processor`.

`Payments.Api` does not expose service browsing or payment quote endpoints. The public API keeps the credit-card surface only.

## Runtime Shape

```text
PayableServices.Api
  -> PayableServices.Application
  -> PayableServices.Domain
  -> PayableServices.Infrastructure
  -> PayableServices.Persistence
  -> PaymentSwitch.Contracts
  -> ExternalConnections.CompanyPayments
  -> PaymentSwitch.Processor (payment execution gRPC)
```

`PayableServices.Api` is the HTTP boundary. It validates request shape, calls focused application use cases, and returns HTTP responses.

`PayableServices.Application` owns use cases, DTOs, and abstractions. It must not depend on EF Core or ASP.NET `HttpContext`.

`PayableServices.Domain` owns the catalogue and quote entities, state transitions, and domain rules.

`PayableServices.Infrastructure` implements technical adapters, including the `ICompanyPaymentsClient` bridge to the company-payment catalogue feed and the payment-execution gRPC client used during quote confirmation.

`PayableServices.Persistence` owns `PayableServicesDbContext`, EF configurations, repositories, migrations, and seed data.

## Catalogue Sync

The first bridge into the external catalogue feed stays behind `ICompanyPaymentsClient`.

`SyncExternalCatalogueUseCase` pulls the source catalogue from the company-payment feed, stores the external source snapshot, and maps it into the local `PayableServices` model. This allows the bounded context to own its own read model and route metadata without exposing catalogue internals through the payments API.

The sync flow is intentionally separate from the public API. `PayableServices` owns the catalogue state; the company-payment adapter is only the current upstream feed.

## Quote Flow

Payment is a two-step flow:

1. `CreateQuote` builds a persisted quote from a payable service and currency.
2. `ConfirmQuote` validates the quote, validates the selected credit card, and calls the payment-execution boundary to charge synchronously.

The quote is the user-visible pricing record. The payment execution happens only after quote confirmation.

## Payments Boundary

`PayableServices` does not charge cards directly. It calls `PaymentSwitch.Processor` through the `PaymentExecutionService` gRPC contract to confirm a quote.

This keeps the bounded context aligned with the split:

- `PayableServices` owns catalogue and quote orchestration.
- `PaymentSwitch.Processor` owns card charging and payment execution.
- `PaymentSwitch.Worker` owns the async company-delivery workflow after a card is charged.

## External Connections

`ExternalConnections` is the shared adapter layer for payment operators and company payment integrations.

`ExternalConnections.PaymentOperators` owns payment-operator adapters and the registration/charge/refund abstractions.

`ExternalConnections.CompanyPayments` owns the company-payment adapter used by the worker and the catalogue sync bridge.

The connection layer is now shared infrastructure for internal workers rather than a public payments API concern.