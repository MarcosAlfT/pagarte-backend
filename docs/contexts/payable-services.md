# PayableServices

## Purpose

`PayableServices` owns the customer-visible catalogue, external mappings,
payment routes, persisted amount inquiries, and immutable quotes.

## Owns

- Countries, categories, subcategories, providers, and payment networks.
- Payable services, ordered reference definitions, and configured quote components.
- Composite quote pricing, versioned rule evaluation, and immutable pricing evidence.
- Versioned external snapshots, stable source/code mappings, and route approval.
- Client-owned inquiries, idempotency reservations, execution leases, and expiry.
- One immutable quote per consumed inquiry and quote execution status.

## Does Not Own

Stored cards, external credentials, provider HTTP schemas, or payment-saga
execution. Credentials are resolved by server configuration.

## Runtime Boundary

```text
Public catalogue browsing/details -> local PayableServicesDb
Verified administrator -> external HTTP catalogue -> atomic local import/approval
Verified client -> ClientProfiles identity resolution -> inquiry reservation
  -> ExternalConnections.CompanyInquiries -> mock company HTTP
  -> persist normalized outcome -> inquiry-backed quote -> existing Processor saga
```

## Main Use Cases

- `GetCatalogueUseCase` searches local eligible services with AND filters and
  stable, bounded pagination. `GetServiceDetailsUseCase` supplies the reference
  form and inquiry availability without exposing connection information.
- `SyncExternalCatalogueUseCase` validates a complete USD feed before committing
  imports, mappings, removals, version, and fingerprint together. IDs are stable
  by source/code. New services and routes require administrator activation.
- `ActivatePaymentRouteUseCase` validates mapping, availability, country/currency,
  references, and trusted payment/inquiry configuration before atomically
  activating the service and selected route. Sync preserves local components,
  connection configuration, mappings, and approval decisions for present items.
- `CreateInquiryUseCase` validates references, reserves the client/key pair,
  calls the provider outside a database transaction, and commits the normalized
  result before responding. `GetInquiryUseCase` reads stored owner-only history.
- `CreateQuoteUseCase` consumes an eligible inquiry and persists its quote in
  one transaction with a five-minute deadline. `ConfirmQuoteUseCase` atomically
  consumes the quote as Submitted before dispatch and reconciles its one durable
  intent. `GetQuoteUseCase` provides read-only owned snapshots/status.
- `ListQuotesUseCase` returns owner-only stored summaries with lifecycle,
  service, and inclusive UTC created-date filters. Pages default to 20 and
  allow at most 100 rows, ordered by creation and ID descending. It does not
  consult a company or Processor. The summary excludes connection and pricing
  source metadata and reports locally stored payment association/state.
  It includes only unconsumed Unpaid/Expired rows; submitted, paid, failed, and
  legacy processing/cancelled histories do not reappear as customer summaries.

## Domain Rules

- Catalogue reads and details are public. Maintenance requires `VerifiedAdmin`.
  Inquiries and quotes require `VerifiedClient` and the internal client ID
  resolved from the validated Keycloak issuer/subject through ClientProfiles.
- References are case-sensitive string fields. Values are trimmed and validated
  against server definitions; leading zeroes survive. Inquiry requests use a
  list so duplicate field keys are rejected before any provider call.
- `(ClientId, IdempotencyKey)` is unique and case-sensitive. A fingerprint covers
  the service and ordered, trimmed references. Identical retries return stored
  state; conflicting input returns 409, including after expiry or failure.
- Pending duplicates return 202. A one-minute durable lease and a ten-second
  recovery scan resume interrupted requests using the original provider key.
  Provider HTTP requests have a five-second timeout. No HTTP call holds a
  database transaction open, and failed persistence never reports success.
- Completed failures have a safe error code and nullable balance evidence.
  Nothing-due results preserve a verified zero balance but cannot create quotes.
- Effective balance expiry is the earlier of provider expiry and result time
  plus 15 minutes. Invalid, future, or already-expired evidence is rejected.
- Inquiry-backed quotes accept `InquiryId`; amount, currency, references, and
  route come from stored evidence. Exactly one provider `ServiceAmount` replaces
  configured base components, then configured fees, taxes, penalties, and
  discounts are finalized with existing money rules. Evidence must be fresh at
  creation; quote expiry is CreatedAt + five minutes, independent of inquiry
  expiry. Identical creation retries return the same quote; a new attempt
  requires a new inquiry.
- Quote composition, pricing, and bill-key selection follow the
  [Composite and Strategy design](#quote-design-composite-and-strategy) below.
- Legacy services without `RequiresInquiry` retain configured-amount quoting.
  Configured amounts have no authoritative bill verification.
- Detail, creation replay, and history derive Expired for an Unpaid quote with
  no local PaymentId at its stored deadline, without changing the row. Processing
  and final states retain their lifecycle. Display expiry is not proof that an
  uncertain submission cannot exist or that replacement is safe.
- Explicit execution refresh looks up Processor by ClientId + SourceQuoteId,
  including when local PaymentId is absent or the quote deadline has passed.
  A found execution repairs the association/state and an owned recorded
  rejection closes the original submission. Not-found or unavailable
  results do not clear state or authorize another submission.
- A SQL application lock on quote ID serializes Pay, recovery, and result
  updates across API instances. A valid server claim before expiry atomically
  persists Submitted, submission ID, selected card, UTC SubmittedAt, immutable
  package JSON/fingerprint, and a one-minute worker lease. No SQL lock spans gRPC.
  Only the matching lease can apply its response; terminal results never reopen.
- Original quote expiry never rejects the claimed intent. Same-card technical
  redelivery returns/resumes that one result; another card conflicts. A recorded
  Processor rejection finalizes the quote as PaymentFailed, Invalidated, or
  NoLongerDue. A missing payment or RPC outage stays pending, including on
  the explicit status endpoint.
- Inquiry-backed verification uses a new provider operation key, checks the original route
  and exact bill/service amount/currency, and persists the evidence separately
  from the creation inquiry and the immutable submission. Recovery looks up
  existing execution first and obtains fresh evidence if Processor requires it
  before the first payment operation. Configured-price submission also checks
  current service eligibility and the original route before dispatch.
  An unavailable route is recorded on the consumed quote before Processor is
  asked to record non-acceptance. Recovery preserves the recorded decision.
- `QuoteSubmissionRecoveryService` scans up to 100 due intents every ten seconds.
  Fresh scopes and leased claims resume pending dispatch/status with the exact
  persisted package. SQL stores retry count, next attempt, lease, and final result.

## Quote Design: Composite and Strategy

This section is the implementation reference for quote composition, pricing,
and bill identity. [ADR 0005](../decisions/0005-resumable-quotes-and-payment-revalidation.md)
records the accepted design choice. The [Quotes requirement](../requirements/quotes.md)
defines product behavior and acceptance; shared conventions live in
[Application Design](../standards/application-design.md) and
[Money Handling](../standards/money-handling.md).

### Current Implementation

Composite pricing is implemented for services with explicit trusted pricing
definitions. The flat pricing path remains supported. Bill-key selection is
implemented for the supported Development company services; additional live
provider identity contracts and acceptance remain pending. Do not describe a
generic configurable bill-key engine or live-provider enablement as implemented.

| Responsibility | Implementation |
| --- | --- |
| Composition and snapshot models | [QuotePricingModels](../../src/PayableServices/PayableServices.Application/Models/QuotePricingModels.cs) describes groups, leaves, display nodes, and retained rule evidence. |
| Composition evaluation | [QuotePricingService](../../src/PayableServices/PayableServices.Application/Services/QuotePricingService.cs) validates structure/dependencies, calculates leaves, and builds the immutable breakdown. |
| Monetary strategies | [QuotePricingStrategies](../../src/PayableServices/PayableServices.Application/Services/QuotePricingStrategies.cs) resolves trusted strategy ID/version pairs for provider base, fixed, percentage, and discount calculations. |
| Bill-key strategy | [BillIdentityStrategyRegistry](../../src/PayableServices/PayableServices.Application/Models/BillIdentity.cs) selects a trusted provider/service strategy independently of monetary calculation. |
| Orchestration and persistence | [CreateQuoteUseCase](../../src/PayableServices/PayableServices.Application/UseCases/CreateQuoteUseCase.cs) coordinates inquiry consumption and persists finalized items, pricing evidence, and bill identity with the quote. |

Composite is represented through group/leaf records and recursive evaluation;
it does not require a class hierarchy for every node. Groups aggregate children
for display and contribute no second charge. Reference nodes have no monetary
value. Reject missing children, cycles, duplicate membership, unreachable items,
and mixed currencies before persisting the quote.

Pricing strategies calculate individual monetary leaves. Dependencies name
other leaves explicitly; missing or cyclic bases fail validation. Finalize each
leaf with the shared money rules before using it as a percentage basis or
summing the total. Keep strategy selection in the trusted registry, not
service/operator conditionals scattered through use cases.

The Development bill-key strategy selects company, external service, account,
and authoritative billing period. It preserves significant account zeroes and
hashes an unambiguous serialized canonical tuple. The snapshot retains strategy
ID/version and the selected values. The key is distinct from QuoteId and request
idempotency keys. A compatible strategy version must still identify the same
obligation; a version change must not bypass Processor's bill reservation.

### Extension and Snapshot Rules

- Add pricing calculations through the existing versioned registry and keep
  pricing separate from bill identity and provider transport mapping.
- For a new bill-key strategy, define authoritative fields, canonicalization,
  and date/period format. Reject missing identity evidence. Establish compatible
  key mapping or block affected outstanding obligations before activating an
  incompatible identity version. Inquiry or charge time is not a bill date.
- Persist the finalized leaves, grouped breakdown, rule versions, rates, bases,
  currency, total, and separate bill identity once. Reads and dispatch use stored
  evidence rather than the current strategy configuration.
- Keep current rounding support explicit: quote boundaries accept ToEven only.
  Check the display-ordered leaf sum used by Processor, including intermediate
  overflow even when the final grouped result would fit.
- Preserve historical flat quotes without inventing missing strategy evidence.
  A new definition/version affects newly created quotes, not approved snapshots.
- Keep external schemas in ExternalConnections. Processor validates finalized
  monetary lines and owns bill reservation/execution; it does not price quotes.

### Pricing Configuration

Provision a service's trusted pricing definition on
`AmountCompositions.PricingDefinitionJson`. The
[pricing models](../../src/PayableServices/PayableServices.Application/Models/QuotePricingModels.cs)
and [synthetic fixture](../../tests/PayableServices.Tests/CompositeQuotePricingTests.cs)
show the shape and a complete example. Use one composition per service.
Configured-price services use a fixed service amount. Inquiry-backed services
use the verified provider amount as their single service-amount leaf.

Percentage rates are decimal fractions. For example, `0.05` means five percent.
List the monetary leaf IDs that form each basis. A tax includes fees only when
those leaves are in its basis. Percentage rules use the already-rounded
amounts of their dependencies. Fixed discounts take a positive magnitude
and produce a negative line. Invalid definitions fail quote creation before
the inquiry is consumed.
Quote creation checks the finalized monetary leaves in the same display order
used by Processor. Overflow at any step rejects the quote during creation.

The grouped `breakdown` on quote detail contains subtotals and reference nodes.
The existing `items` list and Processor handoff carry finalized monetary leaves.
Rule IDs, versions, rates, and calculated bases remain in the immutable
server-side snapshot. Historical flat quotes and services using flat amount
configuration continue to return their flat items.

### Verification

Use [CompositeQuotePricingTests](../../tests/PayableServices.Tests/CompositeQuotePricingTests.cs)
for composition validation, dependency calculation, currencies, discounts,
overflow, and snapshots. Use
[QuoteSubmissionAcceptanceTests](../../tests/PayableServices.Tests/QuoteSubmissionAcceptanceTests.cs)
for bill identity and submission behavior, and
[PaymentRecoveryEndToEndTests](../../tests/PayableServices.Tests/PaymentRecoveryEndToEndTests.cs)
for preservation of approved pricing across HTTP/gRPC execution and recovery.
Follow the [Testing Standard](../standards/testing.md) and
[acceptance guide](../../tests/PayableServices.Tests/README.md) for setup and scope.

## Data Ownership

`PayableServicesDb` contains catalogue state, `AmountInquiries`, quotes, lines,
trusted pricing definitions and immutable pricing snapshots,
reference snapshots, bill identity and verification, and submission evidence/result/schedule on the
quote row. Unique filtered indexes protect `InquiryId` and `SubmissionId`.
`IUnitOfWork` owns commits; repositories stage writes. SQL transaction-owned
application locks serialize catalogue import/approval and inquiry/quote work
across API instances. The inquiry table retains keys for its lifetime.

## Integrations

- `ExternalConnections.CompanyInquiries` implements provider-neutral catalogue,
  inquiry, and connection-configuration abstractions owned by Application.
- `PaymentSwitchExecutionClient` submits immutable snapshots to Processor using
  the workload credential. Existing connection fields remain in the execution
  package; broader route resolution is tracked in Current Problems.
- Processor owns selected-card verification, expiry/market eligibility, and
  removal ordering at acceptance. PayableServices still sends a Pagarte card ID
  only; it does not receive temporary tokens or provider card references.
- `Utilities.Money` supplies exact minor units, checked totals, and rounding.
- The old Processor catalogue client remains unregistered legacy source;
  synchronization no longer calls Processor.

## Change Guidance

PayableServices includes the verified billing period in the workload handoff
and projects Processor's RefundPending and Refunded outcomes to consumed quotes.
Live company verification remains pending. See the [Quotes requirement](../requirements/quotes.md)
and [target flow](../flows/quote-review-and-payment.md).
Deploy Processor's bill-aware and route-rejection RPCs before cutting over
PayableServices. Unsubmitted inquiry-backed legacy quotes without bill evidence
become Invalidated. Previously accepted work remains recoverable. The migrations
block downgrade when it would discard consumed submissions, bill evidence, or
recorded route rejections.
Explicit quote cancellation is out of scope. Leaving the summary causes no
backend mutation or submission revocation; existing Cancelled history remains.

See [Quote Lifecycle](../flows/quote-lifecycle.md),
[amount-inquiry requirement](../requirements/mock-amount-inquiry.md), and
[acceptance tests](../../tests/PayableServices.Tests/README.md).
Keep provider schemas outside this context and keep adapters thin.
