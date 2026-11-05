---
name: pagarte-use-case
description: Add or change a Pagarte backend API, gRPC, consumer, or business workflow while preserving bounded-context ownership and the Application Use Case Pattern.
---

# Pagarte Use Case

Read `AGENTS.md` and `docs/architecture.md`, then the matching context document
and [Application Design](../../../docs/standards/application-design.md).
Read the relevant flow when the workflow crosses a runtime boundary.

Use the context for specialized design and requirements for product behavior.
For quote composition, pricing, or bill identity, read
[PayableServices: Quote Design](../../../docs/contexts/payable-services.md#quote-design-composite-and-strategy).
For monetary changes, read [Money Handling](../../../docs/standards/money-handling.md).
Follow AGENTS.md routing for quote/payment, card, and authentication rules.

Before changing catalogue, quote, or payment execution, inspect
`docs/current-problems.md`. Distinguish an unresolved ownership question from an
accepted decision awaiting implementation. Carry out accepted changes within the
request; do not silently settle unrelated open ownership questions.

Locate the existing use case and its transport adapter. Follow the standard's
layer and commit responsibilities. For durable work, trace intent persistence,
external calls, result commits, idempotency, concurrency, and recovery before
changing a transition. Reuse the context's existing mechanisms.

Identify affected contexts and update their context/flow documents when
ownership, integration, persistence, or observable behavior changes. Record a
new ADR only when the user or team has actually accepted the decision.

Select verification with [Testing](../../../docs/standards/testing.md) and the
relevant suite guide. Report results, verification limits, and unresolved
cross-context dependencies or ownership conflicts.
