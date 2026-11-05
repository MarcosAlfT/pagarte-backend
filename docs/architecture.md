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
  -> exposes public owned card-registration sessions and stored-card HTTP endpoints
  -> forwards signed setup notifications for provider verification
  -> calls PaymentSwitch.Processor over gRPC

PayableServices.Api
  -> validates client access tokens for quote operations
  -> resolves the internal Client.Id through ClientProfiles
  -> owns PayableServicesDb, catalogue, routes, persisted inquiries, and customer-visible quotes
  -> atomically consumes one five-minute quote and persists immutable submission intent
  -> sends authenticated claimed execution packages to Processor; recovers leased work

PaymentSwitch.Processor
  -> authenticates card and quote-execution workloads
  -> owns PaymentDb, card setup sessions/inbox, stored cards, payments, retry state, operators, and outbox
  -> checks card, pays company, charges full total, and rolls back when required
  -> reconciles unknown outcomes and schedules durable retries

PaymentSwitch.Worker
  -> consumes refund and notification work
  -> updates refund state in PaymentDb through SQL

MockCompany.Api (Development only)
  -> serves synthetic versioned catalogue snapshots and deterministic USD balances

ExternalConnections
  -> maps company catalogue and amount-inquiry HTTP contracts
  -> maps company payment/status/rollback HTTP contracts
  -> maps card registration/readiness/charge/status/refund operator contracts
```

## Quote-to-Payment Execution Flow

The implemented cross-runtime flow keeps the customer quote in PayableServices
and creates one durable Processor payment for its immutable execution package:

```text
Client with access token
  -> PayableServices reserves and persists an owned company amount inquiry
  -> PayableServices atomically consumes the inquiry into one-service Quote snapshot
  -> client confirms with Quote ID + stored Card ID
  -> PayableServices loads the owned snapshot
  -> authenticated workload gRPC handoff
  -> PayableServices commits Submitted + card + server claim time + immutable intent
  -> authenticated SubmitClaimedQuote gRPC handoff
  -> Processor validates timely bound claim, card ownership, totals, and idempotency
  -> Processor records one accepted Payment or immutable rejection by SourceQuoteId
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
| Rich catalogue, routes, inquiries, and quotes | `PayableServices` |
| Development catalogue and balance fixtures | `MockCompany.Api` |
| Stored cards and payment execution | `PaymentSwitch.Processor` |
| Company-first durable execution saga | `PaymentSwitch.Processor` |
| Later refund and notification work | `PaymentSwitch.Worker` |
| Third-party contract adaptation | `ExternalConnections` |

Processor retains a simple internal service catalogue and its legacy APIs.
PayableServices synchronization now consumes the external mock HTTP feed; it no
longer reads Processor for this workflow. Processor does not own inquiries or quotes.

## Integration Style

- HTTP is used for client-facing APIs and third-party adapters. Catalogue
  browsing/details are public; inquiries use the existing verified-client policy.
- ExternalConnections normalizes catalogue feeds and read-only amount inquiries.
  Effective balance expiry is capped at 15 minutes, and only positive owned
  inquiries can supply the base for an inquiry-backed quote.
- PayableServices application use cases own database commits. SQL application
  locks and unique indexes protect versioned imports, inquiry reservations,
  lease recovery, and atomic single-quote consumption. HTTP runs outside those
  transactions.
- Owned quote history and detail read stored snapshots without company or
  Processor calls. Customer listing excludes consumed quotes. Explicit refresh
  and leased submission recovery use source quote ID without depending on local
  PaymentId. An unknown outcome stays consumed; no SQL lock spans a gRPC call.
- ASP.NET Core JWT bearer validation and named policies secure public APIs.
- Payments and PayableServices resolve `(iss, sub)` over a protected internal
  ClientProfiles HTTP endpoint before client-owned operations.
- A workload API key secures internal card and quote-execution gRPC boundaries.
- Card setup uses durable owned sessions, token claims, leases, and provider
  lookup. Callback signatures are verified in the adapter; notifications only
  schedule status lookup. The synthetic operator is Development-only.
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
| `PayableServicesDb` | `PayableServices` | Catalogue, routes, mappings, inquiries, quote snapshots/status, immutable submission IDs/packages, leases/retry schedules/results |
| `PaymentDb` | Processor; Worker for refund fields | Card setup/session audits, immutable integration/credential pins, stored cards, quote submission acceptance/rejection outcomes, payment execution snapshots/retry state, operators, retained catalogue, outbox |

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
ClientProfiles, Payments, the mock company, PayableServices, Processor, and Worker. It supplies
the database connections and workload credentials through parameters. The
local realm import bootstraps a fresh realm; it does not reconcile an existing
realm. AppHost startup, realm import, and API calls with real Keycloak tokens
were verified locally on 2026-10-19. The retired Identity.Client source remains
in the repository until release acceptance and the data inventory are verified.
The requester confirmed that no browser or native client applications exist
yet.

The mock resource and its adapter configuration are enabled only by the local
Development AppHost. The mock and PayableServices require server-supplied company
credentials; no provider destination is accepted from public clients. The
AppHost build and manifest wiring are verified; a fresh full Aspire smoke run
requires Docker. HTTP/SQL acceptance tests independently exercise the new flow.

The [card lifecycle](flows/card-registration.md) implements token-only registration,
consent, recovery/removal, immutable routing, and audited admin reviews with a
Development-only provider. Processor owns its state; Worker reads route pins for refunds.
dLocal HTTP contracts are offline-tested; public setup/mobile acceptance remains
[pending](current-problems.md#6-payment-method-acceptance-and-recovery-gaps).
The [test guide](../tests/PaymentSwitch.Processor.Tests/README.md) covers credentials,
fixtures, migration evidence, and verification limits.

The Processor saga migration requires legacy active payments and pending
`payment.request` messages to be drained before deployment.

## Detailed Documentation

PayableServices owns five-minute Unpaid expiry, permanent consumption at Pay,
customer-list filtering, leased recovery, and fresh bill verification for the
Development company. Processor accepts the bound post-deadline handoff and
reserves the bill key atomically across clients and quotes. Migrate and deploy
Processor before the PayableServices cutover. Legacy accepted payments retain
their original evidence and execution order.

Composite pricing and charge-first execution are pending. The accepted target
charges the card before company notification. A definitive rejection requires
a full refund coordinated between Processor and Worker. Later company settlement
is outside this app. See the [Quotes requirement](requirements/quotes.md) and
[target flow](flows/quote-review-and-payment.md).

See the [documentation index](README.md), [Quote Lifecycle](flows/quote-lifecycle.md),
and [Payment Lifecycle](flows/payment-lifecycle.md).
