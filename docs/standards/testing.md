# Testing Standard

## Select Checks by Changed Behavior

Run verification proportional to the change. Use the existing .NET 10 test
projects and fixtures; choose focused cases during development and the affected
suite before reporting completion. A documentation-only change needs link,
consistency, encoding, and whitespace checks, not application execution.

Commands below run from the repository root.

| Changed area | Verification entry point |
| --- | --- |
| Shared money arithmetic or currency scales | `dotnet test tests/Utilities.Money.Tests/Utilities.Money.Tests.csproj` |
| JWT policies, ownership resolution, or shared security | `dotnet test tests/Pagarte.Security.Tests/Pagarte.Security.Tests.csproj` plus the affected API/transport tests |
| Catalogue, inquiry, quote pricing, submission, or history | `dotnet test tests/PayableServices.Tests/PayableServices.Tests.csproj` |
| Processor execution, cards, provider contracts, or Worker refund compatibility | `dotnet test tests/PaymentSwitch.Processor.Tests/PaymentSwitch.Processor.Tests.csproj` |
| Shared quote/payment contracts or lifecycle behavior across both services | Both PayableServices and Processor suites, plus money/security checks when those contracts change |
| Broad project wiring or references | `dotnet build pagarte-backend.sln`; use `dotnet test pagarte-backend.sln` when solution-wide regression is warranted |

The [PayableServices guide](../../tests/PayableServices.Tests/README.md) and
[Processor guide](../../tests/PaymentSwitch.Processor.Tests/README.md) own exact
fixture configuration, coverage descriptions, and dated execution evidence.
Do not copy historical pass counts into current verification claims.

## Infrastructure and Fixtures

SQL acceptance tests use Windows SQL Server LocalDB by default. PAGARTE_TEST_SQL
selects another test SQL Server; existing rigs replace its database name with a
unique isolated test database and need create/delete permissions. Preserve that
isolation. Do not substitute an application database or weaken migration guards
to make a test run pass. Keep credentials in environment configuration.

Reuse the existing controllable clocks, provider fixtures, and persistence or
transport fault injection. Real SQL is necessary for claims about SQL locks,
filtered uniqueness, transactions, and migration behavior. In-memory substitutes
do not establish those guarantees.

## Durable Workflow Changes

Select scenarios applicable to the changed transition or boundary:

- Identical replay, conflicting input, ownership rejection, and concurrent claims.
- Failed intent commit before dispatch and external success before a lost result
  commit or response.
- Restart recovery with original operation identities and stale-worker fencing.
- Exact expiry boundaries, uncertain provider outcomes, and safe compensation.
- Historical records, migration preconditions, and downgrade evidence retention.

For quote pricing changes, use CompositeQuotePricingTests and the HTTP-to-gRPC
PaymentRecoveryEndToEndTests where relevant. Check that configuration changes do
not alter approved snapshots, and that creation and Processor agree on finalized
line order and checked totals. Source links and setup are in the PayableServices
guide; add tests for changed behavior rather than mirroring implementation.

For EF model changes, check that migrations and model snapshots agree and exercise
the affected migration against isolated test data. Deployment ordering remains
in the relevant context/flow documents, not in a generic test recipe.

## Evidence and Limits

Report commands run, results, and any missing prerequisites or unverified
boundaries. Distinguish unit tests, SQL/HTTP/gRPC acceptance, offline provider
contract fixtures, and a full Aspire smoke run. Aspire startup requires its
configured services and Docker for Keycloak/PostgreSQL. Synthetic JWT/provider
tests do not establish real Keycloak login, live-provider, or mobile acceptance.

For Markdown changes, verify local links and referenced source paths, consistent
implementation status, UTF-8, CRLF, and `git diff --check`. Keep dated test records
intact unless a new run supplies new evidence.
