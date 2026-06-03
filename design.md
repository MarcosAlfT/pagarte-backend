# Pagarte Backend Design

This file is the system-level map for the repository. It describes the main projects, their runtime boundaries, and the way they integrate with each other. Project-specific detail lives in the matching `design_<project>.md` files.

## Runtime Map

```text
ClientIdentityService
  -> issues OpenIddict tokens
  -> serves Clients.API and Pagarte.API

Clients.API
  -> validates ClientIdentityService tokens
  -> owns client profile data

Pagarte.API
  -> validates ClientIdentityService tokens
  -> exposes the public credit-card HTTP surface
  -> calls Pagarte.Services over gRPC

Pagarte.Services
  -> owns PagarteDb
  -> handles card registration and synchronous card charge
  -> publishes payment messages to RabbitMQ
  -> exposes internal gRPC contracts

Pagarte.Engine
  -> consumes payment and refund messages from RabbitMQ
  -> sends payment to companies
  -> retries refunds and publishes alerts

PaymentServices
  -> owns catalogue and quote orchestration
  -> syncs catalogue data from the current Pagarte feed
  -> confirms quotes through Pagarte.Services gRPC

ExternalConnections
  -> shared adapters for card operators and company integrations

RabbitMQ
  -> shared messaging transport used by Pagarte.Services and Pagarte.Engine

Shared libraries
  -> Responses
  -> Pagarte.Contracts
  -> Pagarte.Messaging
```

## Integration Style

- HTTP is used for public APIs.
- gRPC is used for internal service-to-service calls between `Pagarte.API`, `PaymentServices`, and `Pagarte.Services`.
- SQL Server is owned by the service that owns the data.
- RabbitMQ is used for asynchronous payment and notification workflows.
- External HTTP integrations are isolated behind `ExternalConnections`.

## Project Docs

- [design_client_identity_service.md](design_client_identity_service.md)
- [design_clients_api.md](design_clients_api.md)
- [design_pagarte_api.md](design_pagarte_api.md)
- [design_pagarte_services.md](design_pagarte_services.md)
- [design_pagarte_engine.md](design_pagarte_engine.md)
- [design_payment_services.md](design_payment_services.md)
- [design_external_connections.md](design_external_connections.md)

## Shared Libraries

- `Responses` provides the standard response wrapper used by the public HTTP APIs.
- `Pagarte.Contracts` holds the gRPC contracts shared by `Pagarte.API`, `Pagarte.Services`, and `PaymentServices`.
- `Pagarte.Messaging` holds the shared payment message contracts and RabbitMQ topology.
- `RabbitMQ` provides the shared connection and publishing infrastructure.
