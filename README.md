# Pagarte Backend

Pagarte Backend is a .NET 10 payment platform composed of public HTTP APIs,
internal gRPC services, background workers, SQL Server databases, and RabbitMQ
messaging.

The repository currently supports client identity and profiles, credit-card
management, a payable-service catalogue, quote creation, synchronous card
charging, and asynchronous company delivery and refund processing.

## Architecture at a Glance

| Component | Role | Interface | Data |
| --- | --- | --- | --- |
| `Identity.Client.Api` | Client registration, authentication, and token issuance | HTTP/OpenIddict | `IdentityClientDb` |
| `ClientProfiles.Api` | Person, organization, address, and phone profiles | HTTP | `ClientProfilesDb` |
| `Payments.Api` | Public credit-card API | HTTP to clients, gRPC to Processor | None |
| `PayableServices.Api` | Catalogue, payment routes, and user-visible quotes | HTTP, gRPC client | `PayableServicesDb` |
| `PaymentSwitch.Processor` | Card registration, charging, payment state, and SQL outbox | gRPC | `PaymentDb` |
| `PaymentSwitch.Worker` | Company delivery, refund retries, and notifications | RabbitMQ | Reads and updates `PaymentDb` |

The current implementation contains duplicate catalogue and quote models in
`PayableServices` and `PaymentSwitch.Processor`. The resulting integration
gap is documented in [Current Problems](docs/current-problems.md).

## Repository Layout

```text
AppHost/                 .NET Aspire orchestration and shared service defaults
src/                     Application source grouped by bounded context
docs/                    Architecture, context, workflow, and decision records
.agents/skills/          Repository-scoped Codex skills
AGENTS.md                 Repository working rules for coding agents
pagarte-backend.sln      Visual Studio solution
```

Start with the [documentation index](docs/README.md) for architecture and
bounded-context details.

## Requirements

- .NET 10 SDK.
- SQL Server instances or databases for identity, client profiles, payable
  services, and payment processing.
- RabbitMQ for asynchronous payment workflows.
- A trusted ASP.NET Core development HTTPS certificate for local HTTPS.

Check the installed SDK and prepare the development certificate:

```powershell
dotnet --version
dotnet dev-certs https --trust
```

## Configuration

Committed `appsettings.json` files intentionally leave connection strings and
credentials empty. Supply sensitive values with environment variables, .NET
user secrets, or an approved secret manager. Do not commit real credentials.

The main configuration keys are:

| Configuration key | Used by |
| --- | --- |
| `ConnectionStrings:IdentityClientDb` | `Identity.Client.Api` |
| `ConnectionStrings:ClientProfilesDb` | `ClientProfiles.Api` |
| `ConnectionStrings:PayableServicesDb` | `PayableServices.Api` |
| `ConnectionStrings:PaymentDb` | Processor and Worker |
| `PaymentSwitchProcessor:GrpcUrl` | Payments and PayableServices APIs |
| `RabbitMQ` or `ConnectionStrings:PagQueue` | Processor, Worker, and AppHost |
| `PaymentOperator` | Processor and Worker |
| `Email` | Worker |

Use double underscores for nested keys when setting environment variables, for
example `ConnectionStrings__PaymentDb`.

## Build

From the repository root:

```powershell
dotnet restore pagarte-backend.sln
dotnet build pagarte-backend.sln
```

## Run Locally

The Aspire AppHost currently starts:

- `Identity.Client.Api`
- `ClientProfiles.Api`
- `Payments.Api`
- `PaymentSwitch.Processor`
- `PaymentSwitch.Worker`

After providing the required database and RabbitMQ configuration, start it with:

```powershell
dotnet run --project AppHost/Applications.AppHost/Applications.AppHost.csproj --launch-profile https
```

`PayableServices.Api` is referenced by the AppHost project but is not yet
added to the AppHost runtime graph. Run it separately when working with the
catalogue or quote API:

```powershell
dotnet run --project src/PayableServices/PayableServices.Api/PayableServices.Api.csproj
```

When running it separately, configure
`PaymentSwitchProcessor__GrpcUrl=https://localhost:51990` to match the current
Processor development launch profile.

## Documentation

- [Documentation index](docs/README.md)
- [Current architecture](docs/architecture.md)
- [Authentication flow](docs/flows/authentication.md)
- [Payment lifecycle](docs/flows/payment-lifecycle.md)
- [Open problems for discussion](docs/current-problems.md)

The architecture documents describe the code as it exists today. Proposed
ownership changes remain in the issue log until the team makes and records a
decision.
