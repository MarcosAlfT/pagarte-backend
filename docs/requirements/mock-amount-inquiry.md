# Requirement: Service Catalogue and Amount Inquiry with a Mock Company

## Requirement Status

| Field | Value |
| --- | --- |
| Status | Implemented; HTTP/SQL automated acceptance verified |
| Accepted | 2026-11-02 |
| Priority | Critical non-security feature |
| Transactions | `CAT-01` through `CAT-05`, `INQ-01`, and `INQ-02` |
| Linked integration | `QUOTE-08`: create a quote from a persisted inquiry |
| Owners | `PayableServices` and `ExternalConnections` |
| Initial environment | Local development and automated tests |

This document records the accepted behavior now implemented in
[PayableServices](../contexts/payable-services.md), [Mock Company](../contexts/mock-company.md),
and [Quote Lifecycle](../flows/quote-lifecycle.md). The
[acceptance suite](../../tests/PayableServices.Tests/README.md) verifies the public
HTTP flow and SQL persistence. AppHost build and manifest wiring were verified;
a fresh full Aspire/Keycloak smoke run remains an environment check requiring Docker.

The implementation preserves legacy configured-amount quoting for services that
do not require inquiry. New mock imports require inquiry and explicit route approval.
Imported services have no automatic Pagarte fees; existing configured components
are preserved and tested during quote creation.

## Objective

The implemented [quote submission lifecycle](quotes.md) sets a fixed five-minute
Unpaid window from quote creation, replacing the inherited inquiry-expiry cap.
Creation still requires fresh inquiry evidence. A timely server-side Submitted
claim permanently consumes the quote; Processor acceptance/recovery can continue
after its original deadline. Fresh payment-time bill verification, Strategy-selected
bill identity, and post-charge acknowledgment remain pending additional mock and
application contracts. The execution saga currently remains company-first.

Pagarte shall complete the Service Catalogue and Amount Inquiry section of
the transaction inventory. Because external companies are not available yet,
a deterministic mock company HTTP service shall provide catalogue feeds and
customer balances. Pagarte shall implement the actual application workflows,
persistence, and public APIs around that substitute. Connecting a real company
later shall replace the provider adapter or configuration without replacing
the catalogue or inquiry use cases.

An authenticated client shall be able to submit the reference fields required
by a payable service, receive an amount-due result, and create an immutable
quote from the persisted inquiry. The client shall never supply or override
the provider amount or currency.

## Business Distinction

The service catalogue describes what can be paid. It does not prove what a
specific customer owes.

```text
Catalogue
  -> Electricity service
  -> requires accountNumber
  -> supports USD payments

Amount inquiry
  -> accountNumber = 10000001
  -> amount due = USD 128.45
  -> result expires after 15 minutes
```

The quote adds the configured Pagarte components to the provider amount:

```text
Provider amount:  USD 128.45
Pagarte fee:        USD 2.00
Tax:               USD 0.10
Quote total:     USD 130.55
```

All values above are synthetic examples.

## Target Flow

```text
Administrator
  -> synchronizes the mock company catalogue through ExternalConnections
  -> reviews mappings and activates a valid payment/inquiry route

Verified client
  -> browses or searches the local PayableServices catalogue
  -> selects an active payable service
  -> reads its required reference fields
  -> submits references + idempotency key
  -> PayableServices validates the request locally
  -> ExternalConnections calls the mock company over HTTP
  -> adapter normalizes the provider outcome
  -> PayableServices persists the inquiry snapshot
  -> client creates a quote using InquiryId
  -> PayableServices uses the persisted provider amount
  -> fees, taxes, penalties, and discounts are finalized
  -> immutable Quote is persisted
  -> client confirms and pays the Quote through the existing saga
```

## Ownership

### PayableServices owns

- The local customer-visible catalogue, search, details, mappings, and routes.
- Catalogue synchronization and administrator maintenance use cases.
- Provider-neutral application abstractions used by those use cases.
- Eligibility of a catalogue service and payment route for amount inquiry.
- Validation of configured reference-field names and formats.
- The amount-inquiry application use case and public authenticated endpoint.
- Persisted inquiry state, ownership, idempotency, and expiry.
- Selection of the inquiry used to create a quote.
- Quote amount components and the final total.

### ExternalConnections owns

- Provider-specific catalogue and amount-inquiry HTTP contracts and adapters.
- HTTP authentication and request mapping for company providers.
- Mapping mock provider responses into stable internal outcomes.
- Timeout and transport-failure classification.

### The mock company owns

- Synthetic catalogue snapshots and stable external service identifiers.
- Synthetic customer-reference scenarios.
- Synthetic provider inquiry identifiers and balances.
- Deterministic HTTP responses for local development and tests.

The mock company shall not write Pagarte databases or create quotes.

## Functional Requirements

### `CAT-01` Browse Payable Services

- Preserve browsing of active services that allow quoting and payment.
- Read the local PayableServices catalogue; browsing shall not call the mock.
- Catalogue amounts shall not be presented as a customer's amount due.
- Preserve the existing public catalogue-read access for this delivery; service
  details shall follow the same policy. Customer inquiries require
  `VerifiedClient`, and maintenance requires `VerifiedAdmin`.

### `CAT-02` Search and Filter Payable Services

- Support case-insensitive text search over service name, description, and
  search keywords, plus exact category, provider, country, and external
  service-code filters. Supplied filters shall combine with AND semantics.
- Preserve the existing category-filter behavior for existing callers.
- Return only browse-eligible services, with stable ordering by display order,
  name, and service ID, and bounded pagination (default 20, maximum 100).
- Reject malformed filters and page bounds. Valid filters with no matches
  shall return an empty result rather than an error.

### `CAT-03` Service Details and Required References

- Expose a public service-detail operation by Pagarte service ID containing
  name, provider, country, currency, inquiry availability, and ordered reference
  definitions: field key, label, data type, required flag, length limits, and
  validation constraints. Use the existing reference-definition model.
- The first service shall require an eight-to-twenty-digit `accountNumber`.
  References remain strings so leading zeroes are preserved.
- Unknown or browse-ineligible services shall return not found. A service
  without an active inquiry-capable route shall report inquiry unavailable.
- Do not expose provider endpoints, credentials, or credential references.
- Client-side form validation is advisory; the inquiry use case shall repeat
  validation against the server definitions.

### `CAT-04` Synchronize the Mock Company Catalogue

- Extend synchronization to consume a mock company HTTP catalogue feed through
  ExternalConnections, replacing Processor's retained catalogue as the source
  for this workflow. PayableServices remains the customer catalogue owner.
- Identify imported items by source plus external service code. Repeating a
  snapshot shall update existing items without creating duplicates or changing
  their Pagarte IDs. Preserve administrator mappings and activation decisions.
- New imported services/routes shall remain inactive until mapped and approved.
  Provider data shall not overwrite Pagarte fees, taxes, or credentials.
- The initial feed shall be a complete versioned snapshot. Only a validated,
  complete snapshot may deactivate previously imported items absent from it;
  partial, malformed, or failed responses shall leave the previous state intact.
- Validate duplicate codes, USD currency, reference definitions, and mapping
  consistency before committing the snapshot atomically. Serialize overlapping
  synchronization for a source and prevent older versions replacing newer ones.
- Include deterministic fixtures for repeated, updated, removed, invalid, and
  unavailable feeds, and multiple services/providers/categories/countries to
  exercise filters. All initial fixture services shall use USD.
- This requirement does not remove Processor's legacy catalogue APIs or data.
  Their remaining consumers and broader `ARCH-01` closure remain separate work.

### `CAT-05` Maintain Catalogue and Activate Routes

- Keep synchronization and route activation behind the existing `VerifiedAdmin`
  policy. Verify administrator success and anonymous, client, and support denial.
- For this delivery, maintenance means synchronization, mapping validation, and
  activation through the existing maintenance workflows; a general catalogue
  editing UI or unrelated CRUD APIs are outside scope.
- Activation shall reject missing mappings, incompatible service/country
  currency, invalid references, or incomplete payment/inquiry configuration.
- Resolve inquiry endpoints and credentials from trusted server configuration
  using the selected route/provider. Public clients cannot select destinations.
- Repeating activation shall be safe. Failed validation shall not partially
  activate or alter a route.

### `MAI-001` Eligible Services

- Amount inquiry shall be available only for an active payable service that
  allows quoting and payment and has an active inquiry-capable route.
- The initial mock shall support USD services.
- The service definition shall supply the required reference fields. The
  initial example uses `accountNumber`.
- Reference values shall be validated before an external call.
- A request containing unknown, missing, duplicate, or invalid reference
  fields shall fail without calling the mock provider.

### `MAI-002` Authenticated Public Transaction

- The inquiry endpoint shall require the existing `VerifiedClient` policy.
- The authenticated external identity shall resolve to the internal Pagarte
  `ClientId` before the inquiry begins.
- The client may select a service and provide its required references. The
  client may not provide an amount, currency, provider inquiry ID, route,
  endpoint, or credential reference.
- An inquiry shall be owned by the resolved `ClientId` and shall never be
  retrievable or usable by another client.
- Expose an authenticated retrieval operation by Pagarte inquiry ID. Unknown
  and foreign inquiry IDs shall return the same not-found response. Retrieval
  shall return stored state without another company call, including expired
  results and safe failure information.

### `MAI-003` Mock HTTP Boundary

- Local AppHost orchestration shall start a mock company HTTP resource.
- The PayableServices inquiry workflow shall reach it through an
  ExternalConnections adapter rather than reading seeded balances directly.
- The mock endpoint and any credentials shall come from server-side
  configuration.
- The mock resource shall not be enabled in production configuration.
- Replacing the mock with a real company shall require a new provider adapter
  or configuration, not changes to inquiry or quote domain rules.

### `MAI-004` Deterministic Scenarios

The initial mock shall support these synthetic account numbers:

| Account number | Normalized outcome | Behavior |
| --- | --- | --- |
| `10000001` | `AmountDue` | Return USD 128.45 with a valid inquiry ID and expiry |
| `10000002` | `NothingDue` | Return a zero balance; no payable quote may be created |
| `10000003` | `ReferenceNotFound` | Return a definitive customer-reference rejection |
| `10000004` | `TemporarilyUnavailable` | Return a retryable provider failure |
| `10000005` | `TemporarilyUnavailable` | Simulate a timeout; safe to retry as a new inquiry |
| `10000006` | `InvalidProviderResponse` | Return a malformed amount or currency; no payable result |

Any other syntactically valid account number shall return `ReferenceNotFound`.
Malformed references shall fail locally before the provider is called.

- Responses shall be deterministic for the same scenario and idempotency key.
- Tests shall not depend on random amounts, timing, or provider identifiers.
- Use a controllable clock and timeout simulation in tests. Provider IDs and
  timestamps shall remain stable when replaying the same provider key.
- Additional scenarios may be added without changing the normalized outcome
  contract.

### `MAI-005` Normalized Inquiry Result

A successful normalized result shall provide the business information needed
to persist and quote safely:

- provider inquiry ID;
- external service code;
- normalized reference values;
- amount in signed 64-bit minor units;
- ISO currency code;
- provider due date when supplied;
- provider result timestamp;
- expiration timestamp; and
- normalized outcome.

Provider-specific response shapes shall remain inside ExternalConnections.
PayableServices shall not persist the raw provider response or credentials.

- `AmountDue` requires a strictly positive amount; `NothingDue` requires zero.
- Both balance outcomes require USD matching the selected service, a provider
  inquiry ID, and valid UTC result and expiration timestamps.
- Negative, overflowing, malformed, or mismatched monetary values and invalid
  timestamps shall become `InvalidProviderResponse`, never a payable balance.
- Failure outcomes carry a safe error code and no amount, currency, provider
  inquiry ID, or balance expiry. Missing balance evidence shall not be filled
  with zero or invented values.

### `MAI-006` Inquiry Persistence

PayableServices shall persist an inquiry snapshot containing at least:

- Pagarte inquiry ID;
- owning `ClientId`;
- payable service and payment-route identifiers;
- external service code and provider inquiry ID when a balance is returned;
- normalized reference snapshot;
- amount minor units and currency for balance outcomes only;
- provider due date when supplied;
- normalized outcome;
- creation, provider-result, and expiration timestamps where applicable;
- safe error code for failure outcomes;
- client idempotency key, canonical request fingerprint, and provider key; and
- quote-consumption state.

The persisted inquiry is evidence for quote creation. Catalogue or route
changes after the inquiry shall not mutate the snapshot.

Persist every completed provider outcome, including temporary failures and
invalid responses, before returning it. Balance evidence is nullable for
failures as defined in MAI-005. Failure records are retrievable but never
eligible for quoting. Invalid local requests fail before a provider attempt
and do not reserve an idempotency key.

### `MAI-007` Expiry

- For balance outcomes, effective expiry shall be the earlier of provider
  expiry and provider result timestamp plus 15 minutes. Reject future result
  timestamps, expiry at/before the result timestamp, and results already
  expired when received. The mock shall use UTC and the same test clock.
- Expiry shall be evaluated using the server clock.
- An expired inquiry shall remain available for history but shall not create a
  quote.
- The client must perform a new inquiry after expiry.
- Replaying an expired inquiry shall return its original expiry; it shall not
  refresh the balance or lifetime. Failures have no balance expiry. The bill's
  optional due date is independent of inquiry expiry.
- Quote creation shall snapshot its own expiry and shall not extend the source
  inquiry's lifetime.
- A newly created quote has CreatedAt + five minutes as its Unpaid deadline,
  independently of the inquiry's earlier expiry. This does not refresh inquiry
  evidence. A timely Submitted claim continues after that original quote deadline.

### `MAI-008` Idempotency

- The public inquiry transaction shall require an idempotency key.
- The key shall be scoped to the authenticated `ClientId`.
- Repeating the same key, service, and reference values shall return the same
  persisted inquiry result.
- Reusing the key with different service or reference values shall return a
  conflict.
- Fingerprint service ID and validated references after trimming values and
  sorting field names; preserve leading zeroes and case-sensitive field names.
- Persist a unique provider key per reserved Pagarte inquiry. Reuse it for
  recovery calls; never forward the raw client key as a globally unique key.
  Different clients using the same client key shall receive different provider
  keys and independent inquiry records.
- Concurrent duplicate requests shall create at most one Pagarte inquiry.
- Reserve the operation durably with uniqueness on `(ClientId, idempotency
  key)` before calling the provider. Duplicates while pending shall return
  pending state without initiating another concurrent provider call.
- A completed failure is replayed just like success. A new attempt after a
  temporary failure or expiry requires a new client key. Timeout does not imply
  a payment side effect because inquiry is read-only; no payment reconciliation
  is required.
- Recover interrupted pending operations using a bounded execution lease and
  the same stored provider key. Do not hold a database transaction open across
  HTTP. Recovery and database failures must not leave requests permanently
  pending or report success before persistence succeeds.
- Retain the key/result association for the lifetime of the inquiry record;
  expiry shall not make a key reusable. Automatic retention cleanup is outside
  this delivery.

### `MAI-009` Quote Binding

- For inquiry-backed services, quote creation shall accept an `InquiryId` and
  derive the base amount, currency, and references from its persisted result.
  Configured catalogue components shall not supply the base amount for
  inquiry-backed services; they remain the base only for legacy services that
  do not require inquiry.
- The inquiry must belong to the authenticated client and have the
  `AmountDue` outcome.
- The inquiry must be unexpired and compatible with the selected service,
  route, and currency.
- The provider amount shall become the quote's value component. PayableServices
  shall then calculate and snapshot configured taxes, fees, penalties, and
  discounts.
- The quote total shall use the existing exact minor-unit and rounding rules.
- A client shall not override the inquiry amount, currency, references,
  provider ID, or route during quote creation or confirmation.
- One inquiry shall create at most one quote. An identical retry shall
  return the existing quote; a replacement after expiry or final closure shall
  require a new inquiry.
- Inquiry consumption and quote persistence shall succeed or fail atomically.
- This is the linked `QUOTE-08` integration. It does not change payment-saga
  execution transitions. Resolve the application-owned transaction boundary
  required by `ARCH-03` for synchronization and inquiry-to-quote persistence.

### `MAI-010` Outcome Behavior

| Outcome | Client-visible behavior | Quote allowed |
| --- | --- | --- |
| `AmountDue` | Return persisted inquiry and expiry | Yes, while valid |
| `NothingDue` | Explain that no balance is payable | No |
| `ReferenceNotFound` | Explain that the reference was not found | No |
| `TemporarilyUnavailable` | Return a safe retryable failure | No |
| `InvalidProviderResponse` | Return a safe provider-data failure; do not guess an amount | No |

Transport responses shall not expose provider credentials, internal endpoints,
stack traces, or raw provider bodies.

### `MAI-011` Observability

- Logs shall correlate the client, Pagarte inquiry, service, provider inquiry,
  idempotency key, and normalized outcome without logging credentials.
- Reference values shall be redacted or masked according to their service
  definition.
- Metrics shall distinguish successful, definitive, retryable, invalid, and
  expired inquiries.
- Provider latency and timeout counts shall be measurable.

## Acceptance Criteria

### Transaction Completion Checks

| Transaction | Required evidence |
| --- | --- |
| `CAT-01` | Browsing still returns only eligible local services; provider outage does not prevent browsing; catalogue amount is not labeled customer balance. |
| `CAT-02` | Text and every exact filter work independently and combined; pagination/order are stable; empty results and invalid inputs behave as specified. |
| `CAT-03` | Detail response builds the account-number form; missing/inactive services and unavailable inquiry routes behave correctly; internal connection data is absent. |
| `CAT-04` | Initial and repeated sync, updates, removals, overlapping/older versions, invalid feeds, and provider failure are tested; failures preserve the prior snapshot and administrator configuration. |
| `CAT-05` | Administrator can sync and activate valid routes; anonymous/client/support callers cannot; invalid mappings/configuration cannot partially activate a route. |
| `INQ-01` | Valid, zero, unknown-account, unavailable, timeout, and malformed-provider scenarios work over HTTP; local invalid input makes no provider call. |
| `INQ-02` | Every completed outcome is persisted and retrievable only by its owner; duplicate/conflicting keys, different clients using the same key, expiry, and crash recovery are tested against the database. |
| `QUOTE-08` (linked) | Only a valid owned inquiry creates a quote; its base replaces the catalogue base exactly once; concurrent retries create one quote and atomic failure leaves neither partial consumption nor a partial quote. |

For `INQ-02`, test failure between provider response and database commit,
interrupted pending requests, and lease recovery with the original provider
key. Test provider expiry shorter than 15 minutes, future timestamps, negative
or overflowing amounts, wrong currencies, and failed-result retrieval. A replay
of a failed or expired result shall never issue a fresh provider call.

### End-to-End Acceptance

This requirement is complete when:

- AppHost starts the mock company together with the Pagarte services.
- An eligible service exposes the reference information needed to build the
  inquiry request.
- `10000001` produces one persisted USD 128.45 inquiry and an identical retry
  returns the same result.
- Reusing an idempotency key with different input is rejected.
- `10000002` through `10000006` produce their documented outcomes and cannot
  create quotes.
- Invalid references fail before the mock provider is called.
- An inquiry cannot be read or used by another client.
- A valid inquiry creates one quote whose value component is USD 128.45 and
  whose total includes the configured quote components.
- The client cannot change amount, currency, references, route, or provider
  identifiers.
- An expired inquiry and a currency mismatch cannot create a quote.
- Concurrent duplicate inquiry and quote requests do not create duplicate
  records.
- Provider timeouts do not create a guessed balance or payable quote.
- Database-backed tests cover persistence, ownership, idempotency, concurrency,
  expiry, and atomic quote consumption.
- Transport-level tests cover the public API, mock HTTP adapter, error mapping,
  and quote creation.
- The existing quote-payment-execution saga accepts the resulting immutable
  quote without changing its execution transitions.
- `CAT-01` through `CAT-05`, `INQ-01`, `INQ-02`, and linked `QUOTE-08` are
  individually verified and updated to `Complete` in
  `docs/transactions.md`, with current-state context and flow documents updated
  in the same implementation change.

## Implementation Order

1. Define provider-neutral application contracts and normalized catalogue and
   inquiry outcomes, keeping provider HTTP shapes inside ExternalConnections.
2. Add the mock company HTTP resource, catalogue snapshots, and balance cases.
3. Implement the adapters and application-owned database transaction boundary.
4. Complete catalogue synchronization, mapping/activation validation, and
   administrator acceptance checks (`CAT-04`, `CAT-05`).
5. Preserve browsing and add search/filter and service details (`CAT-01` through
   `CAT-03`).
6. Add inquiry persistence, migrations, use cases, create/retrieve endpoints,
   idempotency, and pending-operation recovery (`INQ-01`, `INQ-02`).
7. Implement atomic inquiry-to-quote binding as linked `QUOTE-08` work.
8. Run the transaction-specific acceptance tests below and existing quote/saga
   regression tests; do not close a transaction based on documentation alone.
9. Update architecture, context, flow, current-problem, and transaction
   documentation to describe the implemented behavior.

## Out of Scope

- Connecting to a real company balance API.
- Using real customer or production data.
- Integrating a real company's catalogue or balance API; the mock covers both.
- Changing payment execution, stored-card behavior, refunds, or reconciliation.
- Supporting currencies other than USD in the initial mock.
- Scheduled, recurring, partial, or multi-service payments.

## Required Documentation Updates During Implementation

When implemented, update:

- `docs/architecture.md` for the mock runtime, catalogue feed, and inquiry boundary;
- `docs/contexts/payable-services.md` for inquiry ownership and quote binding;
- `docs/contexts/external-connections.md` for the mock provider adapter;
- `docs/flows/quote-lifecycle.md` for inquiry-backed quote creation;
- `docs/current-problems.md` if catalogue ownership or route resolution changes;
  and
- `docs/transactions.md` for `CAT-01` through `CAT-05`, `INQ-01`, `INQ-02`,
  linked `QUOTE-08`, and evidence for affected prerequisites. Do not close
  broader architecture/security prerequisites without verifying their full scope.

Do not mark the transactions complete until the public flow, persistence,
provider adapter, negative outcomes, and integration tests are implemented.
