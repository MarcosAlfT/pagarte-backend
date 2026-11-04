# Pagarte Backend

Pagarte Backend is a .NET 10 payment platform composed of public HTTP APIs,
internal gRPC services, SQL Server databases, and RabbitMQ workers. Keycloak
owns interactive authentication and token issuance.

The backend implements client profiles, catalogue and amount inquiries,
immutable quotes, a durable company-first payment saga, and stored-card
management. Card setup currently uses a Development-only synthetic operator.
Live dLocal/mobile tokenization and challenge acceptance remain pending.
Refund execution/retries and email consumption exist; public refund initiation
and initial payment-result notification publication remain incomplete.

## Architecture at a Glance

| Component | Role | Interface | Data |
| --- | --- | --- | --- |
| Keycloak | Credentials, sessions, roles, and token issuance | OpenID Connect/OAuth 2.0 | Keycloak PostgreSQL |
| `ClientProfiles.Api` | Profiles, contact data, and external-identity to internal-client mapping | Public HTTP and protected internal resolver | `ClientProfilesDb` |
| `Payments.Api` | Owned card/session endpoints and operator callback transport | HTTP to clients/providers, authenticated card gRPC to Processor | None |
| `PayableServices.Api` | Catalogue, routes, persisted inquiries, quotes, and execution status | HTTP and authenticated execution gRPC client | `PayableServicesDb` |
| `PaymentSwitch.Processor` | Card setup/persistence and durable company-first payment execution | gRPC and provider adapters | `PaymentDb` |
| `PaymentSwitch.Worker` | Refund retries and notification consumption | RabbitMQ and provider adapters | Refund-field updates in `PaymentDb` |
| `MockCompany.Api` | Development catalogue feed and synthetic balances | HTTP | Synthetic fixtures |

PayableServices owns quotes. Processor retains a separate simple catalogue,
but its former quote model and post-charge company-delivery flow are removed.
Remaining catalogue duplication and route-resolution work are tracked in
[Current Problems](docs/current-problems.md). Retired Identity.Client source
remains for migration inventory; it is excluded from the solution and AppHost.

## Repository Layout

```text
AppHost/                 Aspire orchestration, realm configuration, and service defaults
src/                     Application source grouped by bounded context
tests/                   Unit and isolated SQL/HTTP/gRPC acceptance tests
docs/                    Architecture, requirements, contexts, flows, and decisions
.agents/skills/          Repository-scoped Codex skills
AGENTS.md                Repository working rules for coding agents
pagarte-backend.sln      Visual Studio solution
```

Start with the [documentation index](docs/README.md).

## Requirements

- .NET 10 SDK.
- Docker for the AppHost's Keycloak/PostgreSQL containers.
- SQL Server databases for client profiles, payable services, and payments.
- RabbitMQ for refund and notification work.
- A trusted ASP.NET Core development HTTPS certificate for local HTTPS.

```powershell
dotnet --version
dotnet dev-certs https --trust
```

## Configuration

Committed application settings leave credentials and connection strings empty.
Supply sensitive values through environment variables, .NET user secrets, or
the deployment secret manager. Do not commit their values.

| Configuration key | Used by |
| --- | --- |
| `ConnectionStrings:ClientProfilesDb` | ClientProfiles |
| `ConnectionStrings:PayableServicesDb` | PayableServices |
| `ConnectionStrings:PaymentDb` | Processor and Worker |
| `AuthSettings` | API Keycloak authority/audience validation |
| `ClientIdentityResolver` | ClientProfiles resolver key; caller URL/key in Payments and PayableServices |
| `PaymentSwitchProcessor:GrpcUrl` | Payments and PayableServices |
| `WorkloadAuth:ApiKey` | Payments, PayableServices, and Processor |
| `RabbitMQ` or `ConnectionStrings:PagQueue` | Processor and Worker |
| `PaymentOperators` | Central integration registry in Processor and Worker |
| `CardOperationsAuth:ApiKey` | Distinct Payments/Processor admin-review workload key |
| `PaymentOperatorCredentials` | Server-side dLocal account credentials resolved by pinned reference |
| `PaymentMethods` | Processor token-claim protection and consent policy version |
| `PaymentOperatorCallbackSecrets` | Processor callback verification |
| `CompanyProviders` / `CompanyCredentials` | Company adapter configuration and server-side secrets |
| `PaymentOperator` | Legacy operator configuration and payment HTTP adapter |
| `Email` | Worker |

Use double underscores for nested environment keys, such as
`ConnectionStrings__PaymentDb`. AppHost parameter names and wiring are defined
in [AppHost.cs](AppHost/Applications.AppHost/AppHost.cs). In addition to database,
RabbitMQ, Keycloak, resolver, company, and workload parameters, card development
requires card-token-digest-key, mock-card-callback-signing-key, and a separate
card-operations-workload-api-key for admin reviews. The
[payment-method test guide](tests/PaymentSwitch.Processor.Tests/README.md)
documents these parameters and the synthetic integration.

## Build and Test

From the repository root:

```powershell
dotnet restore pagarte-backend.sln
dotnet build pagarte-backend.sln
dotnet test pagarte-backend.sln
```

SQL acceptance tests use isolated, uniquely named databases. See the
[payment-method](tests/PaymentSwitch.Processor.Tests/README.md) and
[catalogue/inquiry](tests/PayableServices.Tests/README.md) guides for LocalDB
defaults, alternate test-server configuration, and cleanup behavior.

## Run Locally

The Development-only Aspire AppHost starts Keycloak and PostgreSQL,
ClientProfiles, Payments, PayableServices, Processor, Worker, and MockCompany.
It configures a separate in-process synthetic card operator with its own file
journal; that operator is not MockCompany.Api.

After configuring the AppHost parameters and starting Docker, run:

```powershell
dotnet run --project AppHost/Applications.AppHost/Applications.AppHost.csproj --launch-profile https
```

Database-owning services apply migrations during startup. Review the documented
[card migration outcome](docs/flows/card-registration.md#persistence-and-migration)
before running against populated databases. Older deployments must also follow
the [payment-saga deployment rule](docs/flows/payment-lifecycle.md#deployment-rule).

No browser or native application exists in this repository. The documented
dLocal mobile contract is still an integration target; synthetic token fixtures
are for Development/tests only.

## Documentation

- [Documentation index](docs/README.md)
- [Current architecture](docs/architecture.md)
- [Transaction inventory and completion status](docs/transactions.md)
- [Authentication flow](docs/flows/authentication.md)
- [Card registration and removal](docs/flows/card-registration.md)
- [Payment lifecycle](docs/flows/payment-lifecycle.md)
- [Current problems and remaining acceptance](docs/current-problems.md)

Current implementation, historical decisions, and proposed behavior are labeled
separately. Passing mock tests does not establish live-provider acceptance.
