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
  one transaction. `ConfirmQuoteUseCase` and `GetQuoteUseCase` preserve the
  existing quote execution and owner-only retrieval behavior.

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
  discounts are finalized with existing money rules. Quote expiry never exceeds
  inquiry expiry. Identical retries return the same quote; replacement requires
  a new inquiry.
- Legacy services without `RequiresInquiry` retain configured-amount quoting.
  Processor payment transitions and confirmation payloads are unchanged.

## Data Ownership

`PayableServicesDb` contains catalogue state, `AmountInquiries`, quotes, lines,
and reference snapshots. A unique filtered quote index protects `InquiryId`.
`IUnitOfWork` owns commits; repositories stage writes. SQL transaction-owned
application locks serialize catalogue import/approval and inquiry/quote work
across API instances. The inquiry table retains keys for its lifetime.

## Integrations

- `ExternalConnections.CompanyInquiries` implements provider-neutral catalogue,
  inquiry, and connection-configuration abstractions owned by Application.
- `PaymentSwitchExecutionClient` submits immutable snapshots to Processor using
  the workload credential. Existing connection fields remain in the execution
  package; broader route resolution is tracked in Current Problems.
- `Utilities.Money` supplies exact minor units, checked totals, and rounding.
- The old Processor catalogue client remains unregistered legacy source;
  synchronization no longer calls Processor.

## Change Guidance

See [Quote Lifecycle](../flows/quote-lifecycle.md),
[amount-inquiry requirement](../requirements/mock-amount-inquiry.md), and
[acceptance tests](../../tests/PayableServices.Tests/README.md).
Keep provider schemas outside this context and keep adapters thin.
