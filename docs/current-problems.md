# Current Problems for Discussion

This document records unresolved problems observed after implementation of the
confirmed-quote execution saga.

Last reviewed against the working tree: 2026-09-13.

## Review Status

| Problem | Status | Resolution condition |
| --- | --- | --- |
| Duplicate catalogue ownership | Open | Choose one canonical catalogue or define a deliberate replicated read model. |
| External catalogue adapter | Open | Replace the Processor catalogue dependency with the selected external feed boundary. |
| PayableServices unit of work | Open | Register an implementation and move commits to application-owned transaction boundaries. |
| Remaining integration coverage | Partially resolved | Keep the current unit coverage and add the database-backed and transport-level scenarios listed below. |
| Execution package carries connection data | Open | Resolve company endpoints and credentials server-side from stable route identifiers. |

Seven quote-execution items have been verified as resolved and are listed at
the end of this document.

## 1. Duplicate Catalogue Ownership

**Status: Open.**

### Current implementation

`PayableServicesDb` owns the rich payable-service catalogue. `PaymentDb`
still contains a simpler `Service` and `Company` catalogue, and
`PayableServices.Infrastructure.CompanyPaymentsClient` reads Processor's
catalogue during sync.

The duplicate Processor quote aggregate has been removed; this problem now
applies only to catalogue data.

### Impact

- There are still two sources of service names, prices, currencies, and active
  state.
- Processor seed data and PayableServices records do not share guaranteed
  identifiers.
- The class name `CompanyPaymentsClient` hides its current dependency on
  Processor rather than a company feed.

### Open decisions

- Make PayableServices the canonical catalogue and remove Processor's retained
  catalogue, or define a deliberate replicated read model.
- Choose the real external catalogue/feed boundary.
- Define stable mappings for external company service identifiers.

## 2. External Catalogue Adapter Is Not Implemented

**Status: Open.**

`ExternalConnections.CompanyPayments` supports service payment, status, and
rollback, but not the catalogue/feed bridge described by the earlier design.
PayableServices sync still reads Processor's internal catalogue through
[`CompanyPaymentsClient`](../src/PayableServices/PayableServices.Infrastructure/Clients/CompanyPaymentsClient.cs).

## 3. PayableServices Persistence Does Not Follow Its Unit-of-Work Shape

**Status: Open.**

[`IUnitOfWork`](../src/PayableServices/PayableServices.Application/Abstractions/IUnitOfWork.cs)
exists in PayableServices Application, but Persistence does not register an
implementation. [`QuoteRepository`](../src/PayableServices/PayableServices.Persistence/Repositories/QuoteRepository.cs)
commits directly for create and update. This predates the execution saga and
differs from the explicit application-owned transaction boundary in other
contexts.

## 4. Remaining Integration Coverage

**Status: Partially resolved.**

The current two test suites contain twelve tests. They cover ISO scale and
rounding, checked money totals, the successful company-first sequence,
full-total charging, retry without duplicate company payment,
definitive-charge rollback, and safe handling of unknown charge outcomes.

The repository still needs database-backed and transport-level coverage for:

- Authenticated HTTP quote creation, confirmation, and status refresh.
- Workload authentication rejection and success.
- Concurrent duplicate confirmations against SQL Server.
- Restart recovery and row-version contention between dispatcher instances.
- Real provider contract fixtures for company status/rollback and dLocal
  readiness/charge reconciliation.
- Migration execution against a production-like copy after legacy work has been
  drained.

Quote confirmation and execution-status authentication are implemented. The
first item above remains open because the repository does not yet verify that
behavior through the public HTTP transport.

## 5. Execution Package Carries Company Connection Data

**Status: Open.**

The accepted requirement says the PayableServices-to-Processor package should
carry stable route and destination identifiers, while external adapters should
resolve endpoints and credentials from server-side configuration. The current
[`QuoteExecutionPackage`](../src/PayableServices/PayableServices.Application/Models/ExecutionModels.cs)
and protobuf snapshot still transport company payment, status, and rollback
endpoint URLs plus a credential reference. The API key itself remains
server-side and is not placed in the package.

This does not match the selected contract boundary. Processor should resolve
the current endpoint set and credential reference from the stable route data,
or the requirement must be explicitly revised if immutable endpoint snapshots
are intended.

## Verified Resolved by the Quote Execution Change

The following items were rechecked against the working tree on 2026-09-13:

- PayableServices is the sole quote owner.
- The PayableServices-to-Processor handoff carries an immutable execution
  snapshot.
- Quote confirmation and status are publicly exposed and authenticated.
- PayableServices is included in the AppHost graph.
- The legacy Processor quote flow and post-charge company-delivery consumer are
  removed.
- gRPC and messaging monetary values no longer use floating point.
- The edited payment contract comments no longer contain encoding artifacts.
