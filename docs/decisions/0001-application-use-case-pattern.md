# ADR 0001: Application Use Case Pattern

## Status

Accepted.

## Context

Pagarte contains HTTP controllers, gRPC services, RabbitMQ consumers, and hosted
polling services. Business workflows become difficult to test and reuse when
transport adapters own orchestration and persistence decisions.

## Decision

Backend and API workflows use focused Application use cases, also known as
Interactors or Application Services.

- Controllers, gRPC services, and consumers validate and translate transport
  data, invoke a use case, and map the result.
- Application use cases coordinate repositories, gateways, domain behavior,
  clocks, and transaction boundaries.
- Domain entities own state transitions and invariants.
- Infrastructure implements persistence and external-service abstractions.
- Hosted services own scheduling and polling loops, not business decisions.

The physical project layout may differ by bounded context. Identity,
ClientProfiles, and PayableServices use separate projects, while Processor and
Worker use internal layer folders inside one deployable project.

## Consequences

- Business workflows have explicit entry points.
- Transport technology can change without moving business rules.
- Infrastructure depends on Application abstractions; Application does not
  depend on concrete Infrastructure implementations.
- Small workflows require an additional use-case type, but the responsibility
  remains clear.

## Implementation Guidance

[Application Design](../standards/application-design.md) maintains the coding
conventions for this decision. Context documents describe their current layout
and specialized patterns; this ADR retains the architectural rationale.
