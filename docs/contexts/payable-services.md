# PayableServices

## Purpose

`PayableServices` owns the customer-visible catalogue, external mappings,
payment routes, persisted amount inquiries, and immutable quotes.

## Owns

- Countries, categories, subcategories, providers, and payment networks.
- Payable services, ordered reference definitions, and configured quote components.
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
- Legacy services without `RequiresInquiry` retain configured-amount quoting.
  The existing company-first Processor saga remains in place; charge-first
  notification/refund and fresh bill verification are pending work.
- Detail, creation replay, and history derive Expired for an Unpaid quote with
  no local PaymentId at its stored deadline, without changing the row. Processing
  and final states retain their lifecycle. Display expiry is not proof that an
  uncertain submission cannot exist or that replacement is safe.
- Explicit execution refresh looks up Processor by ClientId + SourceQuoteId,
  including when local PaymentId is absent or the quote deadline has passed.
  A found execution repairs the association/state; not-found or unavailable
  results do not clear state or authorize another submission.
- A SQL application lock on quote ID serializes Pay, recovery, and result
  updates across API instances. A valid server claim before expiry atomically
  persists Submitted, submission ID, selected card, UTC SubmittedAt, immutable
  package JSON/fingerprint, and a one-minute worker lease. No SQL lock spans gRPC.
  Only the matching lease can apply its response; terminal results never reopen.
- Original quote expiry never rejects the claimed intent. Same-card technical
  redelivery returns/resumes that one result; another card conflicts. A recorded
  Processor rejection sets PaymentFailed, never Unpaid. A missing payment or RPC
  outage stays pending, including on the explicit status endpoint.
- `QuoteSubmissionRecoveryService` scans up to 100 due intents every ten seconds.
  Fresh scopes and leased claims resume pending dispatch/status with the exact
  persisted package. SQL stores retry count, next attempt, lease, and final result.

## Data Ownership

`PayableServicesDb` contains catalogue state, `AmountInquiries`, quotes, lines,
reference snapshots, and single-use submission evidence/result/schedule on the
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

Remaining work in the accepted [Quotes requirement](../requirements/quotes.md)
includes Composite/Strategy pricing, service-specific bill keys, fresh bill
verification and cross-quote duplicate protection. The accepted
[target flow](../flows/quote-review-and-payment.md) uses charge-first company
confirmation/full refund; that execution change is pending. Single-use submission,
fixed five-minute Unpaid expiry, customer filtering, and leased recovery are
implemented above; they do not establish the missing provider capabilities.
Deploy Processor's new RPC/migration before exposing the new PayableServices
release and stop/drain older PayableServices instances at cutover. Unexecuted
legacy quotes become Expired without submission evidence. Legacy accepted
payments remain recoverable. Downgrade is blocked once consumed quotes exist.
Explicit quote cancellation is out of scope. Leaving the summary causes no
backend mutation or submission revocation; existing Cancelled history remains.

See [Quote Lifecycle](../flows/quote-lifecycle.md),
[amount-inquiry requirement](../requirements/mock-amount-inquiry.md), and
[acceptance tests](../../tests/PayableServices.Tests/README.md).
Keep provider schemas outside this context and keep adapters thin.
