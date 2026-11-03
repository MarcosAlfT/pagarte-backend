# Catalogue and Inquiry Acceptance Tests

Run `dotnet test tests/PayableServices.Tests/PayableServices.Tests.csproj`.
The suite uses .NET 10, ASP.NET HTTP TestServer, signed access tokens with the
real authorization policies, and SQL Server. It applies the actual EF migrations
and creates a unique `PagarteInquiryTests_` database for each test, then removes
only that test database.

Windows SQL Server LocalDB is the default. Other SQL Server installations can
be selected with the `PAGARTE_TEST_SQL` environment variable; the test rig replaces
its database name with a unique test name. The account needs permission to create
and remove test databases. Keep credentials in environment configuration.

The mock and application share a controllable clock. No random amount or
wall-clock delay determines an assertion. Transport timeout and persistence
failure injection cover failures before outcome commit and before atomic quote
commit. Concurrent HTTP calls exercise SQL locks and unique indexes.

## Coverage

| Transactions | Evidence |
| --- | --- |
| CAT-01, CAT-03 | Public local browsing during provider outage, reference-form details, unavailable inquiry routes, hidden connection data, missing/inactive service handling. |
| CAT-02 | Each exact filter, case-insensitive text search, AND combinations, stable pages, empty matches, and invalid bounds/filters. |
| CAT-04 | Real mock HTTP feed, repeat/update/removal, invalid/incomplete/unavailable snapshots, immutable versions, concurrent sync, and preserved IDs/approval. |
| CAT-05 | Signed administrator success and anonymous/client/support denial, inactive imports, incompatible activation rollback. |
| INQ-01 | All six account scenarios, unknown and malformed references, unknown/duplicate fields, transport timeout, invalid monetary/timestamp/reference evidence. |
| INQ-02 | SQL persistence, safe failure retrieval, ownership, independent client keys, conflict/replay, pending duplicates, completion failure, lease recovery with original provider key, capped and short expiry. |
| QUOTE-08 | Owned positive evidence, exact base replacement plus configured components, atomic rollback, concurrent retries, expiry/currency/route rejection, unchanged Processor execution validation and quote confirmation. |

Run the existing `PaymentSwitch.Processor.Tests`, `Utilities.Money.Tests`, and
`Pagarte.Security.Tests` suites for saga, money, and security regressions.

## Local Manual Flow

1. Start the local AppHost with its existing database, identity, and credential
   parameters configured. It starts `mock-company` and supplies the trusted
   `CompanyProviders:MockCompany` settings. Docker is required for Keycloak.
2. With an administrator token, POST `api/catalogue/maintenance/sync` using
   `SourceName = MockCompany` and an active USD `CountryId`. The response pairs
   imported service IDs, route IDs, external codes, and names in `routes`.
3. Review those mappings and POST the selected `PaymentRouteId` to
   `api/payment-routes/activate`. Imports are inactive until approval.
4. Read `api/catalogue/{serviceId}` to build the account-reference form.
5. With a verified client token, POST `api/inquiries` with an `Idempotency-Key`
   header and `{ "serviceId": "<id>", "references": [{ "key": "accountNumber",
   "value": "10000001" }] }`.
6. POST `api/quotes` with `{ "inquiryId": "<returned-id>" }`. The value is USD
   128.45 plus the service's configured additional components. Imported services
   have no default Pagarte fees; administration-owned components are preserved.
7. Confirm the quote using the existing stored-card flow. Repeat the inquiry key
   and quote request to verify their stable IDs. Try accounts 2 through 6 for
   nonpayable outcomes.

Full Aspire startup with Keycloak is a separate local smoke check. The automated
HTTP/SQL tests do not require Docker, an external company, or a live Keycloak
server; signed tokens exercise validation and policies with a test signing key.
