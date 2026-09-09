# PayableServices

## Purpose

`PayableServices` models the rich payable-service catalogue, payment routes,
external catalogue mappings, and user-visible quotes.

This document describes the current implementation, including its overlap with
`PaymentSwitch.Processor`.

## Owns

Within `PayableServicesDb`, the context currently owns:

- Countries, categories, and subcategories.
- Providers and payment networks.
- Payable services and reference-field definitions.
- Payment routes.
- External catalogue sources, items, and mappings.
- Amount compositions and components.
- Quotes and quote items.
- Public catalogue and quote-creation HTTP endpoints.

## Does Not Own

- Credit-card persistence.
- Payment-operator selection.
- Card charging.
- Payment records and SQL outbox publication.
- Asynchronous company delivery and refunds.

Processor currently duplicates catalogue and quote concepts. That overlap is an
unresolved problem rather than an intended ownership rule.

## Runtime Boundary

```text
PayableServices.Api
  -> HTTP controllers
  -> PayableServices.Application
  -> PayableServices.Domain
  -> PayableServices.Persistence / PayableServicesDb
  -> PayableServices.Infrastructure
      -> PaymentSwitch.Processor gRPC
```

## Projects and Layers

- `PayableServices.Api` contains controllers, request DTOs, startup, and
  OpenAPI.
- `PayableServices.Application` contains use cases, models, and abstractions.
- `PayableServices.Domain` contains catalogue, route, mapping, amount, and
  quote entities.
- `PayableServices.Infrastructure` implements the Processor gRPC clients.
- `PayableServices.Persistence` contains EF Core, repositories, migrations,
  and seed data.

## Main Use Cases

- `GetCatalogueUseCase` reads the local PayableServices catalogue.
- `CreateQuoteUseCase` creates and persists a local quote.
- `SyncExternalCatalogueUseCase` stores a source snapshot and mapping state.
- `ActivatePaymentRouteUseCase` activates a payment route.
- `ConfirmQuoteUseCase` validates a local quote and calls Processor, but no
  HTTP controller currently exposes it.

## Domain Rules

- A service must be active and allow quoting before a quote is created.
- The requested currency must match the service currency.
- A quote starts unpaid and expires after the configured creation window,
  currently 60 minutes.
- Confirmation rejects missing, foreign, non-unpaid, or expired quotes.

## Data Ownership

`PayableServices` owns `PayableServicesDb`. The current
`QuoteRepository` commits directly, while an unused `IUnitOfWork` abstraction
also exists. This inconsistency is tracked in
[Current Problems](../current-problems.md).

## Integrations

- `PaymentSwitchExecutionClient` calls Processor's
  `PaymentExecutionService.ConfirmQuote`.
- The class named `CompanyPaymentsClient` currently calls Processor's
  `ServiceCatalogService`; it does not call an external company catalogue
  adapter.
- `Utilities.Responses` supplies the public response wrapper.

## Change Guidance

Read [Quote Lifecycle](../flows/quote-lifecycle.md),
[Payment Lifecycle](../flows/payment-lifecycle.md), and
[Current Problems](../current-problems.md) before changing catalogue, quote, or
confirmation behavior. Do not assume the duplicate Processor records share
identifiers with PayableServices records.
