---
name: pagarte-use-case
description: Add or change a Pagarte backend API, gRPC, consumer, or business workflow while preserving bounded-context ownership and the Application Use Case Pattern.
---

# Pagarte Use Case

Read `AGENTS.md` and `docs/architecture.md`, then read the context document
matching the requested project. Read the relevant file under `docs/flows/`
when the workflow crosses a runtime boundary.

Before changing catalogue, quote, or payment execution behavior, read
`docs/current-problems.md`. Preserve the documented current behavior unless
the user explicitly chooses a resolution for an open ownership question.

Implement workflows as focused Application use cases. Keep HTTP controllers,
gRPC services, RabbitMQ consumers, and hosted-service loops limited to
transport, mapping, and scheduling. Put state invariants on Domain entities and
external or persistence mechanics behind Application abstractions.

Identify every bounded context affected by the change. Update the relevant
context and flow documents when ownership, integration, persistence, or
observable behavior changes. Record a new accepted architectural decision only
when the user or team has actually made that decision.

Run verification proportional to the change and report any unresolved
cross-context dependency or data-ownership conflict.
