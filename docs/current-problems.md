# Current Problems for Discussion

This document records problems observed in the current source code. It is a
discussion backlog, not an authorization to change application behavior.

Last reviewed: 2026-09-07.

## 1. Duplicate Catalogue Ownership

### Current implementation

`PayableServicesDb` contains the rich payable-service catalogue: countries,
categories, subcategories, providers, payment networks, routes, reference
fields, mappings, and payable services.

`PaymentDb` separately contains `Service` and `Company` tables. Processor
seeds its own services and exposes them through `ServiceCatalogService`.

`PayableServices.Infrastructure.CompanyPaymentsClient` consumes that Processor
gRPC catalogue even though its name and the earlier design suggested an
external company feed.

### Impact

- There are two sources of service names, prices, currencies, and active state.
- Processor seed data uses generated service IDs while PayableServices uses
  fixed IDs, so catalogue mapping cannot assume identity equality.
- A service may be available in one database and missing or different in the
  other.
- The class name `CompanyPaymentsClient` hides the real dependency on
  Processor.

### Questions for the team

- Is `PayableServices` the canonical catalogue?
- Does Processor need a local execution snapshot instead of a catalogue?
- Which external system will provide the real catalogue feed?
- How should external service IDs map to internal payable-service and route
  IDs?

## 2. Duplicate Quote Aggregates

### Current implementation

`PayableServices` has `Quote`, `QuoteItem`, `CreateQuoteUseCase`, a public
`POST /api/quotes` endpoint, and quote tables in `PayableServicesDb`.

Processor separately has `PaymentQuote`, `PaymentQuoteDetail`,
`CreatePaymentQuoteUseCase`, `ConfirmPaymentQuoteUseCase`, and quote tables
in `PaymentDb`. Its `PaymentService.CreatePaymentQuote` gRPC operation has no
caller inside this repository.

### Impact

- Quote status, totals, expiration, and line items can diverge.
- It is unclear which quote is the customer-visible price commitment.
- Two databases can independently mark different records as paid.

### Questions for the team

- Should PayableServices remain the sole owner of the user-visible quote?
- Should Processor receive an immutable execution snapshot instead of owning a
  second quote?
- If Processor calculates operator fees, should it expose a stateless pricing
  operation during quote creation?

## 3. Quote Confirmation Handoff Does Not Join the Two Models

### Current implementation

`PayableServices.ConfirmQuoteUseCase` sends its local quote ID to
`PaymentExecutionService.ConfirmQuote`. Processor then searches for that ID in
its own `PaymentQuotes` table.

There is no implemented step that copies or creates the PayableServices quote
inside `PaymentDb`.

### Impact

A quote created through the public PayableServices API will normally produce
`Quote not found` if it is passed to Processor.

### Discussion direction

Consider changing the execution contract to carry an external quote ID plus an
immutable amount, currency, route/company, expiration, and line-item snapshot.
Processor could use the external quote ID for idempotency while continuing to
own payment execution.

## 4. Quote Confirmation Is Not Publicly Exposed

`ConfirmQuoteUseCase` is registered in PayableServices dependency injection,
but `QuoteController` only exposes quote creation. No controller calls the
confirmation use case.

The intended two-step quote flow is therefore incomplete at the HTTP boundary.

## 5. PayableServices Is Missing from the AppHost Runtime Graph

The AppHost project references `PayableServices.Api`, but `AppHost.cs` does
not call `AddProject` for it. Developers must currently start it separately
and configure the Processor gRPC URL manually.

## 6. PayableServices Persistence Does Not Follow Its Unit-of-Work Shape

`IUnitOfWork` exists in PayableServices Application, but Persistence does not
register an implementation. `QuoteRepository` calls `SaveChangesAsync`
directly for create and update operations.

This differs from the explicit application-owned transaction boundary used in
ClientProfiles and Processor.

## 7. External Catalogue Adapter Is Not Implemented

`ExternalConnections.CompanyPayments` currently supports company payment
delivery only. It does not expose the catalogue/feed bridge described by the
earlier design. PayableServices sync currently reads Processor's internal
catalogue instead.

## 8. gRPC Contract Comments Contain Encoding Artifacts

Section-divider comments in `payment_switch.proto` contain mojibake characters.
The contract remains readable by tooling, but the source should be normalized
when the contract is next edited.

## 9. Missing End-to-End Coverage for the Payment Path

No test project was found for the public quote-to-charge-to-delivery path.
Before changing ownership, add coverage for quote creation, confirmation,
idempotent execution, card-charge failure, outbox creation, company rejection,
and refund retry.
