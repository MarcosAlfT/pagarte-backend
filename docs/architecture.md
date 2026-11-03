# Pagarte Backend Architecture

This document maps the current implementation and runtime ownership.

## Runtime Map

```text
Keycloak (local Aspire container; PostgreSQL)
  -> issues user access tokens for the pagarte realm
  -> owns credentials, sessions, and signing keys

ClientProfiles.Api
  -> validates Keycloak access tokens and maps (issuer, subject) to Client.Id
  -> owns ClientProfilesDb

Payments.Api
  -> validates client access tokens
  -> resolves the internal Client.Id through ClientProfiles
  -> exposes public stored-card HTTP endpoints
  -> calls PaymentSwitch.Processor over gRPC

PayableServices.Api
  -> validates client access tokens for quote operations
  -> resolves the internal Client.Id through ClientProfiles
  -> owns PayableServicesDb, catalogue, routes, and customer-visible quotes
  -> sends authenticated immutable execution packages to Processor

PaymentSwitch.Processor
  -> authenticates the PayableServices workload
  -> owns PaymentDb, stored cards, payments, retry state, operators, and outbox
  -> checks card, pays company, charges full total, and rolls back when required
  -> reconciles unknown outcomes and schedules durable retries

PaymentSwitch.Worker
  -> consumes refund and notification work
  -> updates refund state in PaymentDb through SQL

ExternalConnections
  -> maps company payment/status/rollback HTTP contracts
  -> maps card registration/readiness/charge/status/refund operator contracts
```

## Quote-to-Payment Execution Flow

The implemented cross-runtime flow keeps the customer quote in PayableServices
and creates one durable Processor payment for its immutable execution package:

```text
Client with access token
  -> PayableServices creates one-service Quote snapshot
  -> client confirms with Quote ID + stored Card ID
  -> PayableServices loads the owned snapshot
  -> authenticated workload gRPC handoff
  -> Processor validates package, expiry, card ownership, and idempotency
  -> Processor persists one Payment by SourceQuoteId
  -> operator checks card readiness
      -> decline: final failure; a new attempt requires a new quote
  -> company pays the service using service code + references
      -> rejection: final failure without a card charge
      -> unknown: durable reconciliation/retry
  -> operator charges the complete quote total once
      -> success: payment completes
      -> retryable failure: durable retry
      -> unknown: durable reconciliation; unsafe ambiguity goes to manual review
      -> known final failure/cap: durable company rollback
  -> PayableServices refreshes execution status
      -> Completed: Quote = Paid
      -> final decline or completed rollback: Quote = PaymentFailed
      -> retry, reconciliation, rollback, or manual review: Quote = Processing
```

Processor commits an in-progress transition before each external operation.
Its scheduler resumes due work from `PaymentDb`, including after a process
restart. Separate stable idempotency keys protect company payment, card charge,
and company rollback. See [Quote Lifecycle](flows/quote-lifecycle.md) and
[Payment Lifecycle](flows/payment-lifecycle.md) for the detailed flows.

## Ownership

| Capability | Owner |
| --- | --- |
| Client identities and tokens | Keycloak |
| External identity to Pagarte client mapping | `ClientProfiles` |
| Client profile data | `ClientProfiles` |
| Public stored-card HTTP surface | `Payments.Api` |
| Rich catalogue, routes, and quotes | `PayableServices` |
| Stored cards and payment execution | `PaymentSwitch.Processor` |
| Company-first durable execution saga | `PaymentSwitch.Processor` |
| Later refund and notification work | `PaymentSwitch.Worker` |
| Third-party contract adaptation | `ExternalConnections` |

Processor retains a simple internal service catalogue used by the current sync
implementation, but it no longer owns payment quotes.

## Integration Style

- HTTP is used for client-facing APIs and third-party adapters.
- ASP.NET Core JWT bearer validation and named policies secure public APIs.
- Payments and PayableServices resolve `(iss, sub)` over a protected internal
  ClientProfiles HTTP endpoint before client-owned operations.
- A workload API key secures the internal quote-execution gRPC boundary.
- gRPC connects public services to Processor.
- SQL Server stores context-owned state.
- Processor uses its payment row as the durable saga schedule and concurrency
  boundary.
- RabbitMQ carries refund and notification work; it no longer carries
  post-charge company delivery.
- Money crosses service and message boundaries as signed 64-bit minor units plus
  an ISO currency code.

## Data Boundaries

| Database | Writers | Main data |
| --- | --- | --- |
| Keycloak PostgreSQL | Keycloak | Users, sessions, realm configuration, and keys |
| `ClientProfilesDb` | `ClientProfiles` | Clients, external identity mapping, and contact information |
| `PayableServicesDb` | `PayableServices` | Catalogue, routes, mappings, quote snapshots and status |
| `PaymentDb` | Processor; Worker for refund fields | Cards, payment execution snapshots, retry state, operators, retained service catalogue, outbox |

## Shared Libraries

- `Utilities.Responses` provides the public HTTP response wrapper.
- `Utilities.Money` centralizes ISO scale, rounding, exact minor-unit
  conversion, and checked summation.
- `PaymentSwitch.Contracts` contains gRPC contracts.
- `PaymentSwitch.Messaging` contains RabbitMQ topology, exact money messages,
  transaction status, and clock abstractions.
- `Infrastructure.RabbitMQ` provides connection and publishing infrastructure.
- `Applications.ServiceDefaults` provides common Aspire service defaults.
- `Pagarte.Security` provides JWT policies and internal client-ID resolution.

## Deployment and Local Orchestration

The Aspire AppHost starts Keycloak with a dedicated PostgreSQL volume, then
ClientProfiles, Payments, PayableServices, Processor, and Worker. It supplies
the database connections and workload credentials through parameters. The
local realm import bootstraps a fresh realm; it does not reconcile an existing
realm. AppHost startup, realm import, and API calls with real Keycloak tokens
were verified locally on 2026-10-19. The retired Identity.Client source remains
in the repository until release acceptance and the data inventory are verified.
The requester confirmed that no browser or native client applications exist
yet.

The Processor saga migration requires legacy active payments and pending
`payment.request` messages to be drained before deployment.

## Detailed Documentation

See the [documentation index](README.md), [Quote Lifecycle](flows/quote-lifecycle.md),
and [Payment Lifecycle](flows/payment-lifecycle.md).
