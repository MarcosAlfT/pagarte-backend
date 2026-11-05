# Application Design Standard

## Scope and Authority

These are implementation conventions for Pagarte backend and API changes.
[ADR 0001](../decisions/0001-application-use-case-pattern.md) records the accepted
Application Use Case Pattern. The [architecture](../architecture.md) and
[context documents](../README.md#bounded-contexts-and-adapter-areas) describe
current ownership. Requirements define product behavior and acceptance criteria.

## Layer Responsibilities

- HTTP controllers, gRPC services, and message consumers translate transport
  input, invoke an Application use case, and map its result. Hosted services
  schedule work in fresh scopes; they do not decide business transitions.
- Application use cases coordinate domain behavior, repositories, gateways,
  clocks, and commits. Keep a workflow's transaction decisions here.
- Domain entities enforce state transitions and invariants. Keep transport,
  provider schemas, and database mechanics outside the Domain.
- Infrastructure implements persistence and external-service abstractions.
  Application code depends on abstractions, not concrete adapters. Register
  implementations at the composition root.
- Follow the context's existing physical layout. Separate projects and internal
  layer folders both exist; adopting this pattern does not require splitting a
  deployable project.

## Persistence and External Work

For durable workflows, inspect the existing use case and context contract before
changing transaction boundaries. Persist intent and claim work before an
external side effect; commit the resulting evidence afterward. Keep provider
HTTP and execution gRPC calls outside SQL transactions and application locks.

Preserve operation-specific idempotency identities, concurrency checks, and
lease fencing during recovery. Distinguish a definitive failure from an unknown
external outcome. Provider adapters normalize evidence; Application and Domain
own the resulting workflow decisions. Detailed lifecycle rules belong in the
relevant [flow document](../README.md#cross-service-flows).

Repositories follow their context's commit contract. In PayableServices,
repositories stage writes and Application use cases commit through IUnitOfWork.
Do not introduce independent repository commits into that workflow.

## Context Boundaries and Specialized Design

Reuse shared contracts and utilities rather than copying their behavior.
[Money Handling](money-handling.md) governs finalized monetary values.
[Authentication](../flows/authentication.md) describes canonical client identity,
named policies, and the independent client and workload boundaries.

For quote composition, pricing strategies, and bill-key selection, follow
[PayableServices: Quote Design](../contexts/payable-services.md#quote-design-composite-and-strategy).
Those patterns are scoped to quote design; they are not a requirement to add
Composite or Strategy to every use case. Provider mappings remain in
ExternalConnections and payment execution remains in Processor.

An accepted design may have only partial implementation. Preserve that
status in the context document. An unresolved issue in
[Current Problems](../current-problems.md) does not prevent implementing an
already accepted decision within the requested scope; do not silently settle
an open ownership question through an unrelated change.

## Verification and Documentation

Use the [Testing Standard](testing.md) to select checks for the changed behavior.
Update affected context and flow documents when ownership, persistence,
integration, or observable behavior changes. Record a new ADR only for an
actually accepted architectural decision, with its reasoning and consequences.
