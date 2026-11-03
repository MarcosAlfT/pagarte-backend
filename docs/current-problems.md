# Current Problems for Discussion

This document records unresolved problems observed after implementation of the
confirmed-quote execution saga.

Last reviewed for catalogue/inquiry implementation: 2026-11-02.

## Review Status

| Problem | Status | Resolution condition |
| --- | --- | --- |
| Duplicate catalogue ownership | Open | Choose one canonical catalogue or define a deliberate replicated read model. |
| External catalogue adapter | Resolved for mock delivery | HTTP catalogue/inquiry adapter replaces Processor as the synchronization source; real providers remain later integrations. |
| PayableServices unit of work | Resolved | Registered application-owned commits and SQL transactions cover catalogue synchronization, activation, and inquiry-to-quote consumption. |
| Remaining integration coverage | Partially resolved | Keep the current unit coverage and add the database-backed and transport-level scenarios listed below. |
| Execution package carries connection data | Open | Resolve company endpoints and credentials server-side from stable route identifiers. |

Seven quote-execution items have been verified as resolved and are listed at
the end of this document.

## 1. Duplicate Catalogue Ownership

**Status: Open.**

### Current implementation

`PayableServicesDb` owns the rich payable-service catalogue. `PaymentDb`
still contains a simpler `Service` and `Company` catalogue. PayableServices sync
now reads the mock company HTTP feed through ExternalConnections instead of
Processor. The old `CompanyPaymentsClient` source remains unregistered; its
Processor gRPC dependency is no longer part of the synchronization runtime.

The duplicate Processor quote aggregate has been removed; this problem now
applies only to catalogue data.

### Impact

- There are still two sources of service names, prices, currencies, and active
  state.
- Processor seed data and PayableServices records do not share guaranteed
  identifiers.
- Processor retains legacy catalogue data/APIs; their retirement and any
  external consumers have not been verified in this requirement.

### Open decisions

- Make PayableServices the canonical catalogue and remove Processor's retained
  catalogue, or define a deliberate replicated read model.
- Verify remaining consumers before retiring the retained Processor catalogue.
- The selected mock HTTP feed boundary and stable source/code mappings are
  implemented; a real company integration remains later work.

## 2. External Catalogue Adapter

**Status: Resolved for mock delivery.**

`ExternalConnections.CompanyInquiries` implements provider-neutral catalogue,
amount-inquiry, and connection-configuration interfaces owned by PayableServices
Application. The local mock supplies versioned complete snapshots and synthetic
balances over HTTP. Invalid feeds leave local state intact. Processor's legacy
catalogue APIs/data remain outside this delivery.

## 3. PayableServices Unit of Work

**Status: Resolved.**

`IUnitOfWork` is registered in Persistence and owns commits requested by
Application use cases. Repositories stage writes. Transaction-owned SQL
application locks serialize catalogue updates/approval and inquiry consumption.
Inquiry reservations commit before provider HTTP, completion commits afterward,
and a bounded durable lease supports recovery. A single transaction persists
both quote and consumption state. SQL/HTTP tests cover concurrency and injected
persistence failures; no database transaction spans provider HTTP.

## 4. Remaining Integration Coverage

**Status: Partially resolved.**

The original three test suites contain 21 tests. They cover security policy and
resolver failure behavior as well as ISO scale and
rounding, checked money totals, the successful company-first sequence,
full-total charging, retry without duplicate company payment,
definitive-charge rollback, and safe handling of unknown charge outcomes.

The catalogue/inquiry acceptance suite adds SQL Server migrations and HTTP
coverage for search/details, admin policies, snapshots, inquiries, idempotency,
leases, ownership, invalid provider evidence, expiry, and atomic quote creation.
An inquiry-backed quote is also tested against the existing Processor validator
and public quote confirmation adapter. See the
[acceptance-test guide](../tests/PayableServices.Tests/README.md).

The repository still needs database-backed and transport-level coverage for:

- Full authenticated quote confirmation/status refresh through live Processor
  gRPC and its SQL persistence, beyond the existing HTTP adapter/validator coverage.
- Workload authentication rejection and success.
- Concurrent duplicate confirmations against SQL Server.
- Restart recovery and row-version contention between dispatcher instances.
- Real provider contract fixtures for company status/rollback and dLocal
  readiness/charge reconciliation.
- Migration execution against a production-like copy after legacy work has been
  drained.

Quote confirmation and execution-status authentication are implemented. A
local AppHost run verified authenticated HTTP quote creation with a real
Keycloak token and SQL Server persistence on 2026-10-19. Repeatable automated
transport coverage for confirmation and status refresh remains open.

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
