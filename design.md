# Backend Design

This file is the system-level map for the repository. It describes the main projects, their runtime boundaries, and the way they integrate with each other. Project-specific detail lives in the matching `design_<project>.md` files.

## Runtime Map

```text
Identity.Client
  -> issues OpenIddict access tokens
  -> serves ClientProfiles.Api and Payments.Api

ClientProfiles.Api
  -> validates Identity.Client tokens
  -> owns client profile data
  -> delegates workflows to ClientProfiles.Application
  -> persists through ClientProfiles.Persistence

Payments.Api
  -> validates Identity.Client tokens
  -> exposes the public credit-card HTTP surface
  -> calls PaymentSwitch.Processor over gRPC

PaymentSwitch.Processor
  -> owns PaymentDb
  -> handles card registration and synchronous card charge
  -> records post-charge messages in the SQL outbox
  -> publishes pending outbox messages to RabbitMQ
  -> exposes internal gRPC contracts

PaymentSwitch.Worker
  -> consumes payment and refund messages from RabbitMQ
  -> sends payment to companies
  -> retries refunds and publishes alerts

PayableServices
  -> owns catalogue and quote orchestration
  -> syncs catalogue data through the company-payment adapter
  -> confirms quotes through PaymentSwitch.Processor gRPC

ExternalConnections
  -> shared adapters for payment operators and company integrations

Infrastructure.RabbitMQ
  -> shared messaging infrastructure used by PaymentSwitch.Processor and PaymentSwitch.Worker

Shared libraries
  -> Utilities.Responses
  -> PaymentSwitch.Contracts
  -> PaymentSwitch.Messaging
```

## Integration Style

- HTTP is used for public APIs.
- gRPC is used for internal service-to-service calls between `Payments.Api`, `PayableServices`, and `PaymentSwitch.Processor`.
- SQL Server is owned by the service that owns the data.
- RabbitMQ is used for asynchronous payment and notification workflows.
- External HTTP integrations are isolated behind `ExternalConnections`.

## Project Docs

- [design_identity_client.md](design_identity_client.md)
- [design_client_profiles.md](design_client_profiles.md)
- [design_payments_api.md](design_payments_api.md)
- [design_payment_switch_processor.md](design_payment_switch_processor.md)
- [design_payment_switch_worker.md](design_payment_switch_worker.md)
- [design_payable_services.md](design_payable_services.md)
- [design_external_connections.md](design_external_connections.md)

## Shared Libraries

- `Utilities.Responses` provides the standard response wrapper used by the public HTTP APIs.
- `PaymentSwitch.Contracts` holds the gRPC contracts shared by `Payments.Api`, `PaymentSwitch.Processor`, and `PayableServices`.
- `PaymentSwitch.Messaging` holds the shared payment message contracts, RabbitMQ topology,
  `PaymentTransactionStatus`, and `IClock`/`SystemClock`.
- `Infrastructure.RabbitMQ` provides the shared connection and publishing infrastructure.
