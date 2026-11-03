# Pagarte Documentation

This directory is the entry point for repository architecture and design
documentation. Documents describe the current implementation unless a section
is explicitly marked as proposed.

## Start Here

- [Architecture](architecture.md) - system map, runtime boundaries, data
  ownership, and integration styles.
- [Current Problems](current-problems.md) - unresolved duplication and
  integration problems found in the current code.

## Bounded Contexts and Adapter Areas

- [Retired Identity.Client source](contexts/identity-client.md)
- [ClientProfiles](contexts/client-profiles.md)
- [Payments.Api](contexts/payments-api.md)
- [PayableServices](contexts/payable-services.md)
- [PaymentSwitch.Processor](contexts/payment-switch-processor.md)
- [PaymentSwitch.Worker](contexts/payment-switch-worker.md)
- [ExternalConnections](contexts/external-connections.md)

## Cross-Service Flows

- [Authentication](flows/authentication.md)
- [Quote Lifecycle](flows/quote-lifecycle.md)
- [Payment Lifecycle](flows/payment-lifecycle.md)

## Proposed Requirements

- [Keycloak Security Migration](requirements/keycloak-security-migration.md) -
  implementation status and acceptance criteria for the identity cutover
  onboarding while preserving Pagarte domain ownership.

## Decisions

- [Application Use Case Pattern](decisions/0001-application-use-case-pattern.md)
- [SQL Outbox for Post-Charge Delivery](decisions/0002-sql-outbox.md)
- [Quote Execution Saga and Legacy Quote Removal](decisions/0003-quote-execution-saga.md)
- [Global Money Handling](decisions/0004-money-handling.md)

## Maintenance Rules

- Update a context document when ownership, persistence, use cases, or
  integrations change in that context.
- Update a flow document when a message, endpoint, protocol, or responsibility
  changes across contexts.
- Add an architectural decision record when the team accepts a decision whose
  reasoning and consequences should be retained.
- Record observed but unresolved conflicts in
  [Current Problems](current-problems.md).
- Keep API schemas in OpenAPI and gRPC schemas in their source files. Use these
  documents to explain behavior and ownership rather than copy complete
  schemas.
