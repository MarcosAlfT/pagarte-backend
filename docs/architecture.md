# Pagarte Backend Architecture

This document maps the current implementation. It intentionally shows the
duplicate catalogue and quote responsibilities that exist today; it does not
present the proposed cleanup as completed work.

## Runtime Map

```text
Identity.Client.Api
  -> issues OpenIddict access tokens
  -> owns IdentityClientDb
  -> serves ClientProfiles.Api and Payments.Api

ClientProfiles.Api
  -> validates Identity.Client tokens
  -> owns ClientProfilesDb
  -> delegates workflows to Application and Persistence projects

Payments.Api
  -> validates Identity.Client tokens
  -> exposes the public credit-card HTTP surface
  -> calls PaymentSwitch.Processor over gRPC

PayableServices.Api
  -> exposes catalogue and quote-creation HTTP endpoints
  -> owns PayableServicesDb, including catalogue and Quote tables
  -> has a quote-confirmation use case that is not exposed by a controller
  -> reads a second catalogue from PaymentSwitch.Processor during sync

PaymentSwitch.Processor
  -> owns PaymentDb
  -> owns cards, payments, payment operators, and the SQL outbox
  -> also owns duplicate Service and PaymentQuote tables
  -> exposes credit-card, payment, execution, and catalogue gRPC services
  -> charges cards synchronously
  -> publishes pending outbox messages to RabbitMQ

PaymentSwitch.Worker
  -> consumes payment and refund messages from RabbitMQ
  -> sends payments to companies
  -> retries refunds and publishes alerts
  -> updates PaymentDb through SQL

ExternalConnections
  -> contains payment-operator adapters
  -> contains the company payment-delivery adapter
```

## Current Ownership

| Capability | Current owner |
| --- | --- |
| Client identities and tokens | `Identity.Client` |
| Client profile data | `ClientProfiles` |
| Public credit-card HTTP endpoints | `Payments.Api` |
| Card persistence and operator registration | `PaymentSwitch.Processor` |
| Card charging, payment records, and outbox | `PaymentSwitch.Processor` |
| Asynchronous company delivery and refunds | `PaymentSwitch.Worker` |
| Rich payable-service catalogue | `PayableServices` |
| Simple processor service catalogue | `PaymentSwitch.Processor` |
| Publicly created quotes | `PayableServices` |
| Processor payment quotes | `PaymentSwitch.Processor` |

Catalogue and quote ownership is therefore duplicated. See
[Current Problems](current-problems.md) for the concrete code paths and open
questions.

## Integration Style

- HTTP is used for client-facing APIs.
- OpenIddict issues and validates access tokens.
- gRPC connects `Payments.Api` and `PayableServices` to
  `PaymentSwitch.Processor`.
- SQL Server stores context-owned data.
- RabbitMQ carries post-charge payment, refund, and notification work.
- External HTTP calls are intended to remain behind `ExternalConnections`
  adapters.

## Data Boundaries

| Database | Current writers | Main data |
| --- | --- | --- |
| `IdentityClientDb` | `Identity.Client` | Users and identity tokens |
| `ClientProfilesDb` | `ClientProfiles` | Clients and contact information |
| `PayableServicesDb` | `PayableServices` | Catalogue, routes, mappings, and quotes |
| `PaymentDb` | Processor; Worker uses direct SQL for status updates | Cards, duplicate services and quotes, payments, operators, and outbox |

## Shared Libraries

- `Utilities.Responses` provides the public HTTP response wrapper.
- `PaymentSwitch.Contracts` contains shared gRPC contracts.
- `PaymentSwitch.Messaging` contains RabbitMQ topology, payment messages,
  transaction status, and clock abstractions.
- `Infrastructure.RabbitMQ` provides connection and publishing
  infrastructure.
- `Applications.ServiceDefaults` provides common Aspire service defaults.

## Deployment and Local Orchestration

The Aspire AppHost currently starts Identity, ClientProfiles, Payments,
Processor, and Worker. Although the AppHost project references
`PayableServices.Api`, the runtime graph does not add it. PayableServices must
currently be started separately.

## Detailed Documentation

See the [documentation index](README.md) for context, workflow, decision, and
problem documents.
