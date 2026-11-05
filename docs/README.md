# Pagarte Documentation

This directory is the entry point for repository architecture and design
documentation. Documents describe the current implementation unless a section
is explicitly marked as proposed.

## Start Here

- [Architecture](architecture.md) - system map, runtime boundaries, data
  ownership, and integration styles.
- [Payment Application Transactions](transactions.md) - ordered inventory of
  complete, partial, missing, and blocked client and operational transactions.
- [Current Problems](current-problems.md) - unresolved duplication and
  integration problems found in the current code.

## Where Guidance Belongs

| Location | Purpose |
| --- | --- |
| `requirements/` | Product behavior, scope, and acceptance criteria. |
| `standards/` | Maintained implementation and verification conventions. |
| `decisions/` | Accepted architectural choices, rationale, and consequences. |
| `contexts/` | Context ownership, current implementation, and domain-specific design. |
| `flows/` | Behavior and integration across runtime boundaries. |
| `../AGENTS.md` and repository skills | Concise routing and task workflows that reference these documents. |

Keep one authoritative home for detailed guidance. Requirements link to design
and standards rather than acting as the coding handbook. ADRs retain decision
history; contexts explicitly distinguish implemented design from planned work.

## Implementation Standards

- [Application Design](standards/application-design.md) - layers, use cases,
  transaction boundaries, and specialized-design routing.
- [Money Handling](standards/money-handling.md) - exact amounts, rounding,
  snapshots, and current implementation limits.
- [Testing](standards/testing.md) - suite selection, isolated fixtures, recovery
  scenarios, and verification evidence.

Quote-specific Composite and Strategy guidance is in
[PayableServices: Quote Design](contexts/payable-services.md#quote-design-composite-and-strategy).

## Bounded Contexts and Adapter Areas

- [Retired Identity.Client source](contexts/identity-client.md)
- [ClientProfiles](contexts/client-profiles.md)
- [Payments.Api](contexts/payments-api.md)
- [PayableServices](contexts/payable-services.md)
- [PaymentSwitch.Processor](contexts/payment-switch-processor.md)
- [PaymentSwitch.Worker](contexts/payment-switch-worker.md)
- [ExternalConnections](contexts/external-connections.md)
- [Mock Company](contexts/mock-company.md)

## Cross-Service Flows

- [Authentication](flows/authentication.md)
- [Quote Lifecycle](flows/quote-lifecycle.md)
- [Quote Review, Expiry, and Payment Confirmation](flows/quote-review-and-payment.md) - accepted target with implemented submission, expiry, and Development charge-first execution.
- [Payment Lifecycle](flows/payment-lifecycle.md)
- [Card Registration and Removal](flows/card-registration.md)

## Requirements and Acceptance

- [Quotes](requirements/quotes.md) - five development points, accepted behavior,
  implementation scope, and open acceptance gaps. QUOTE-06 is out of scope.
- [Payment Methods](requirements/payment-methods.md) - scope, status, and acceptance rules.
- [Payment-Method Acceptance Tests](../tests/PaymentSwitch.Processor.Tests/README.md) -
  configuration, manual fixtures, and verification evidence.
- [Keycloak Security Migration](requirements/keycloak-security-migration.md) -
  implementation status and acceptance criteria for the identity cutover
  onboarding while preserving Pagarte domain ownership.
- [Service Catalogue and Amount Inquiry with a Mock Company](requirements/mock-amount-inquiry.md) -
  implemented catalogue search, details, mock feed synchronization, route checks,
  persisted USD inquiries, and linked inquiry-backed quote creation.
- [Catalogue, Inquiry, and Quote Acceptance Tests](../tests/PayableServices.Tests/README.md) -
  SQL/HTTP coverage, test prerequisites, and the local manual flow.

## Decisions

- [Application Use Case Pattern](decisions/0001-application-use-case-pattern.md)
- [SQL Outbox for Post-Charge Delivery](decisions/0002-sql-outbox.md) (historical; superseded by ADR 0003)
- [Quote Execution Saga and Legacy Quote Removal](decisions/0003-quote-execution-saga.md)
- [Global Money Handling](decisions/0004-money-handling.md)
- [Five-Minute Quotes and Charge-First Confirmation](decisions/0005-resumable-quotes-and-payment-revalidation.md) - accepted direction with Development execution implemented.

## Maintenance Rules

- Update a standard when an accepted implementation convention changes; retain
  the decision's rationale in its ADR and link between them.
- Keep pattern responsibilities and extension guidance in the owning context,
  with product behavior and acceptance criteria in requirements.

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
