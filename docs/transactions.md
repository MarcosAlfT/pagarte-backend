# Payment Application Transaction Inventory

This document is the ordered implementation inventory for Pagarte client,
payment, and operational transactions. It separates current implementation
facts from planned work so the team can deliver and close one transaction at a
time.

Historical baseline: main commit e5b50d0, reviewed on 2026-10-08. Current
ownership/status was reconciled on 2026-11-05 with the Keycloak backend,
catalogue/inquiry delivery, payment-method backend/offline-provider work, and
the quote-history/explicit-status-recovery delivery merged in main commit 2cae99b.
Historical Identity.Client use cases are retired source, not active transactions. Provider
account-lifecycle release checks and real dLocal/mobile acceptance remain open;
this inventory does not treat configured capabilities as completed acceptance.

## Status Definitions

| Status | Meaning |
| --- | --- |
| Complete | The intended actor can execute the transaction end to end, with its current authentication, persistence, and failure behavior implemented. |
| Partial | Supporting code exists, but the end-to-end transaction or intended public/operational entry point is incomplete. |
| Missing | No complete application transaction exists. |
| Security gap | The transaction exists, but its current authorization boundary is unsafe or inconsistent with the documented intent. |
| Decision required | Coding should wait for an explicit product or architecture decision. |
| Open | A documented architecture problem remains unresolved. |
| Out of scope | Explicitly excluded from the current product scope; the ID remains for traceability. |

A transaction should move to `Complete` only after its use case, entry point,
authorization, persistence, failure behavior, tests, and relevant documentation
are complete.

Status applies to the current transaction scope named in each row. Accepted
revisions to quote creation/pricing and payment execution remain pending even
where their existing baseline transaction is Complete. The Quotes and Payment
Execution sections identify those differences; these counts do not measure
completion of the revised Quotes requirement or production-provider acceptance.

## Current Snapshot

The inventory retains 60 client and operational transaction IDs: 59 in active
scope and one out of scope. Counts below are derived from the transaction rows,
excluding the separate prerequisites.
Keycloak account-lifecycle work and mock-versus-live payment acceptance are
included in the current statuses.

| Status | Count |
| --- | ---: |
| Complete | 26 |
| Partial | 25 |
| Missing | 7 |
| Security gap | 0 |
| Decision required | 1 |
| Out of scope | 1 |

Eight security, architecture, and testing prerequisites are tracked separately
near the end of this document.

## Baseline Work Order

The order below records the original sequencing; completed rows are identified
in the current inventory and do not require reimplementation.

| Order | Transaction IDs | Outcome |
| --- | --- | --- |
| 1 | `SEC-02`, `SEC-03`, `SEC-04`, `CAT-05` | Close authorization gaps before exposing more functionality. |
| 2 | `ARCH-01`, `ARCH-02`, `ARCH-03` | Resolve catalogue ownership, route-resolution, and PayableServices transaction-boundary problems. |
| 3 | `CAT-02`, `CAT-03` | Give the client a usable service search and service-detail experience. |
| 4 | `INQ-01`, `INQ-02`, `QUOTE-08` | Add an external amount-due inquiry and bind its result to an immutable quote. |
| 5 | `PAY-06`, `PAY-07`, `PAY-08`, `PAY-09` | Complete payment details, history, receipts, and result notifications. |
| 6 | `REF-01`, `REF-02`, `REF-04` | Complete the refund lifecycle from request through client-visible status. |
| 7 | `OPS-01` through `OPS-06` | Add manual-review, reconciliation, support, webhook, financial, and audit operations. |
| 8 | `AUTH-10` through `AUTH-13`, `CARD-06`, `CARD-07` | Complete stronger onboarding, session, authentication, and card-security capabilities. |

## Client Authentication and Onboarding

| ID | Transaction | Status | Current implementation or missing work | Owner |
| --- | --- | --- | --- | --- |
| `AUTH-01` | Register client user | Partial | Keycloak self-registration is configured; SMTP-backed registration/email verification release acceptance remains pending. | Keycloak |
| `AUTH-02` | Confirm email | Partial | Keycloak owns email verification; configured SMTP and the end-to-end email flow remain pending release acceptance. | Keycloak |
| `AUTH-03` | Log in with password | Complete | Keycloak Authorization Code + PKCE login with password and real API access tokens was verified locally; no production client application exists yet. | Keycloak |
| `AUTH-04` | Refresh client session | Partial | Keycloak owns refresh tokens and persistent sessions; refresh/session lifecycle release acceptance remains pending. | Keycloak |
| `AUTH-05` | Log out | Partial | Logout is delegated to Keycloak; the documented release checks remain pending. | Keycloak |
| `AUTH-06` | Recover forgotten password | Partial | Recovery is delegated to Keycloak; configured SMTP and recovery-flow acceptance remain pending. | Keycloak |
| `AUTH-07` | Change password | Partial | Password changes belong to Keycloak account management; acceptance of the account-lifecycle journey remains pending. | Keycloak |
| `AUTH-08` | Create person or organization profile | Complete | Separate person and organization creation use cases are implemented. | `ClientProfiles` |
| `AUTH-09` | Manage addresses and phone numbers | Complete | Create, read, update, delete, and primary-contact operations are implemented. | `ClientProfiles` |
| `AUTH-10` | Complete registration and profile onboarding as one client journey | Partial | Keycloak login and ClientProfiles mapping exist, but no client application coordinates the complete onboarding/recovery journey. | Keycloak, ClientProfiles, and future client application |
| `AUTH-11` | View and revoke active sessions or devices | Partial | Keycloak owns persistent sessions; client-facing session/device management and its release acceptance remain pending. | Keycloak and future client application |
| `AUTH-12` | Enroll and use MFA or passkeys | Missing | No configured and accepted MFA/passkey enrollment/authentication journey is recorded. Retired Identity.Client entities are not an active implementation. | Keycloak and future client application |
| `AUTH-13` | Close a client account | Partial | Client-profile deletion exists; coordinated identity closure, stored-card handling, and legal data retention do not. | Cross-context decision required |

## Service Catalogue and Amount Inquiry

The accepted [catalogue and amount-inquiry requirement](requirements/mock-amount-inquiry.md)
covers every transaction in this section and linked `QUOTE-08`. The implemented
mock company supplies catalogue feeds and balances. On 2026-11-02 the
[SQL/HTTP acceptance suite](../tests/PayableServices.Tests/README.md) verified
these transactions. AppHost build/manifest wiring is verified; a fresh full
Aspire/Keycloak startup requires Docker and remains a local smoke check.

| ID | Transaction | Status | Current implementation or missing work | Owner |
| --- | --- | --- | --- | --- |
| `CAT-01` | Browse payable services | Complete | Local eligible catalogue reads remain public and work during provider outage; catalogue base amount is not customer balance. | `PayableServices` |
| `CAT-02` | Search and filter payable services | Complete | Case-insensitive text plus exact category/provider/country/service-code AND filters, stable bounded pages, empty matches, and invalid input are verified over HTTP/SQL. | `PayableServices` |
| `CAT-03` | View service details and required reference fields | Complete | Public eligible-service details expose ordered reference definitions and inquiry availability while hiding connection data; missing/inactive cases are verified. | `PayableServices` |
| `CAT-04` | Synchronize the external service catalogue | Complete | Versioned mock HTTP snapshots import atomically by source/code, preserve IDs/approval/components, deactivate removals, and reject invalid/partial/older/unavailable feeds; overlapping sync is SQL-tested. | `PayableServices` and `ExternalConnections` |
| `CAT-05` | Maintain catalogue and activate payment routes | Complete | Signed administrator success and anonymous/client/support denial are verified; validated mapping/configuration precedes atomic route activation, and failed validation preserves inactive state. | `PayableServices` |
| `INQ-01` | Validate the customer reference and check the amount due | Complete | All mock account HTTP scenarios and local reference validation are verified; provider timeout and invalid amount/currency/timestamps cannot produce payable evidence. | `PayableServices` and `ExternalConnections` |
| `INQ-02` | Persist a stable amount-inquiry result | Complete | Owned results and nullable failure evidence persist in SQL; client-scoped uniqueness, conflict/replay, pending duplicates, lease recovery with original provider key, expiry, and persistence failure are verified. | `PayableServices` |

Maintenance requires `VerifiedAdmin`; inquiries and quotes require `VerifiedClient`
and internal client-ID resolution. Catalogue base amounts remain configured metadata.
An inquiry-backed service obtains customer balance only through its persisted
provider inquiry. Imported services remain inactive until approved.

## Payment Methods

The [requirement](requirements/payment-methods.md) defines CARD-01 through CARD-07;
CARD-08 through CARD-12 track supporting backend and integration delivery.
The [flow](flows/card-registration.md) documents implementation;
the [test guide](../tests/PaymentSwitch.Processor.Tests/README.md) records verification.

| ID | Transaction | Status | Current implementation or missing work | Owner |
| --- | --- | --- | --- | --- |
| `CARD-01` | Add a stored card | Partial | Consent, owned token-only setup, verified metadata, idempotency, recovery, and audited rechecks are tested with the mock. Bound live dLocal setup remains pending. | `Payments.Api` and `PaymentSwitch.Processor` |
| `CARD-02` | List stored cards | Complete | Safe owned listing, stable pagination, removed exclusion, and availability/expiry are tested. | `Payments.Api` and `PaymentSwitch.Processor` |
| `CARD-03` | View one stored card | Complete | Safe owned detail, uniform missing/foreign responses, and malformed-ID handling are tested. | `Payments.Api` and `PaymentSwitch.Processor` |
| `CARD-04` | Update card metadata or select the default card | Complete | Allowed metadata and atomic defaults cover uniqueness, concurrency, eligibility, and rollback. | `Payments.Api` and `PaymentSwitch.Processor` |
| `CARD-05` | Delete a stored card | Partial | Local removal/default clearing, deferred cleanup, removal status, and audited retries preserve shared resources and accepted work/refunds. Live-provider acceptance remains pending. | `Payments.Api` and `PaymentSwitch.Processor` |
| `CARD-06` | Complete a provider challenge such as 3-D Secure | Partial | Owned status/continue/cancel, mock challenges, bounded callback headers, and signed event deduplication are tested. Real issuer UI/timing and required payment continuation remain pending. | Mobile, `Payments.Api`, Processor, ExternalConnections |
| `CARD-07` | Tokenize in the mobile app without sending PAN/CVV through Pagarte | Partial | Backend accepts token/session data only and removes raw-card storage; mobile tokenization/live acceptance remains pending. | Mobile, `Payments.Api`, Processor, ExternalConnections |
| `CARD-08` | Manage registration sessions and consent | Partial | Policy/version audit, owned creation/status/continue/cancel, protected tokens, leases, and bounded recovery are implemented. Live dLocal setup continuation remains pending. | `Payments.Api` and `PaymentSwitch.Processor` |
| `CARD-09` | Select and retain the payment-operator integration | Partial | Provider-neutral contracts, central selection, immutable identity/credential pins, account keys, market checks, and retained refund routing are implemented. Full bound-setup replacement remains pending. | Processor, Worker, ExternalConnections |
| `CARD-10` | Review and reconcile stuck card operations | Complete | VerifiedAdmin queue, authoritative rechecks, safe cleanup retries, and audit lookup are HTTP/gRPC/SQL-tested; separate workload credentials, expected-state/idempotency, and dependency/expiry checks are enforced. | `Payments.Api` and `PaymentSwitch.Processor` |
| `CARD-11` | Save, inspect, and remove a card through dLocal | Partial | Token-only vault HTTP, known-card lookup/deletion verification, signing, and strict result parsing are offline-tested. Customer binding, public setup integration, and live acceptance remain pending. | `ExternalConnections.PaymentOperators` |
| `CARD-12` | Migrate legacy stored-card data | Complete | SQL migration tests cover raw-column removal, retained card/payment references, unverified legacy eligibility, and rejection of incompatible populated data. | `PaymentSwitch.Processor` |

Public dLocal registration stays disabled pending Direct-token/customer binding,
trusted payer projection, lost-reference recovery, setup-event/challenge contracts,
and mobile/live acceptance. Offline HTTP tests do not close those gaps.

## Quotes

The accepted [Quotes requirement](requirements/quotes.md) covers seven active
quote transactions with [new lifecycle definitions](flows/quote-review-and-payment.md).
QUOTE-06 is retained as out of scope following the 2026-11-05 decision to use
expiry rather than an explicit quote-cancellation transaction.
Inquiry-backed quotes use the Development company's bill identity and fresh
unpaid-bill verification. Explicit pricing definitions support nested
breakdowns and versioned calculation. Expired or changed quotes require
a new consultation; there is no automatic refresh or replacement approval.
Payment charges the card first, then notifies the company: OK completes payment;
definitive NOT OK requires a full refund. Company settlement is outside this app.
The statuses below describe the current baseline, not completion of these new
acceptance criteria.

| ID | Transaction | Status | Current implementation or missing work | Owner |
| --- | --- | --- | --- | --- |
| `QUOTE-01` | Create an itemized quote | Complete | Quote creation validates the service, route, references, and currency. It calculates explicit versioned pricing definitions and snapshots the Development bill key for inquiry-backed quotes. | `PayableServices` |
| `QUOTE-02` | Display base value, taxes, fees, discounts, and total | Complete | Explicit definitions produce an immutable grouped breakdown and ordered finalized monetary leaves. Rule versions, rates, and bases remain available for audit. Historical flat quotes keep their original amounts. | `PayableServices` |
| `QUOTE-03` | Retrieve a quote by ID | Complete | `GetQuoteUseCase` returns the owner's snapshot and effective lifecycle at the stored deadline without writes or provider/execution calls; foreign and unknown IDs return not found. | `PayableServices` |
| `QUOTE-04` | Expire unsubmitted quotes after five minutes | Complete | New quotes have an exact five-minute Unpaid window. The atomic Submitted claim binds server time, card, and immutable intent. Processor validates that timely claim even after expiry. SQL/HTTP/gRPC tests cover the deadline, late acceptance, fencing, restart, and legacy migration. | `PayableServices` and `PaymentSwitch.Processor` |
| `QUOTE-05` | List a client's quotes | Complete | Owner-only summaries retain filters and bounded stable pages. Customer listing excludes consumed quotes. Reads make no provider/execution calls, and owned detail/status retains backend history. | `PayableServices` |
| `QUOTE-06` | Cancel an unpaid quote | Out of scope | Explicit quote cancellation was removed from scope on 2026-11-05. Leaving the summary does not revoke the quote or change its deadline. No cancellation endpoint is required. Existing Cancelled values remain for historical compatibility. | `PayableServices` |
| `QUOTE-07` | Consult again after expiry or unsuccessful payment | Partial | Development supports new inquiry/quote creation, changed-bill invalidation, and fresh verification after confirmed compensation. Unresolved and successful payments hold the bill reservation. Live provider acceptance remains pending. | `PayableServices` and `PaymentSwitch.Processor` |
| `QUOTE-08` | Create a quote from a verified amount inquiry | Complete | Owned positive inquiry evidence supplies the base once through shared currency-scale conversion. Quote creation snapshots the authoritative Development bill key and atomically consumes the inquiry. | `PayableServices` |

One quote currently represents one payable service and one payment attempt.

The [quote lifecycle](flows/quote-lifecycle.md) describes the delivered history
and explicit execution-recovery behavior. The [acceptance guide](../tests/PayableServices.Tests/README.md)
records verification and limits. HTTP/SQL tests cover consumption, failed commits,
replay/card conflicts, exact expiry, stale leases, and API restart recovery. The
Processor suite uses the actual handoff client against authenticated gRPC and SQL
for late acceptance, concurrency, durable rejection, and workload enforcement.
Public quote HTTP tests exercise the actual Processor gRPC handoff, restart
recovery, and the original finalized lines after a pricing configuration change.

The revised requirement remains partially implemented. Live company
verification, acknowledgment, and refund contracts still require
acceptance. Effective
expiry and a not-found execution lookup do not prove a delayed submission is
safe to replace. Leaving the summary after Pay does not cancel accepted or
uncertain work. PAY-05 concerns payment-execution cancellation and retains its
separate Decision required status.
The clarified target permanently consumes a quote on the first valid Pay:
atomically persist Unpaid -> Submitted with its single intent before external
work, then hide its customer summary. No failure, rejection, expiry, or refund
restores that quote to Unpaid. This is implemented: recorded rejection leaves
PaymentFailed, unknown dispatch remains Submitted, and customer listing excludes
used summaries. Recovery uses the original immutable package and a fenced lease.
Technical redelivery recovers the same intent/result; any later eligible
customer payment requires a new inquiry/quote. QUOTE-04 limits the initial server
claim while Unpaid; it never expires Submitted work. Processor acceptance and
recovery may occur after that original deadline, with fresh bill checks and
independent operation policies still enforced.

## Payment Execution

The rows below cover the persisted company-first and Development charge-first
execution modes. See the [Quotes requirement](requirements/quotes.md).

| ID | Transaction | Status | Current implementation or missing work | Owner |
| --- | --- | --- | --- | --- |
| `PAY-01` | Confirm and pay a quote with a stored card | Complete | `ConfirmQuoteUseCase` consumes the quote, binds the card, and checks fresh evidence for inquiry-backed bills. Processor pins execution mode and technical replay resumes the original intent. | `PayableServices` and `PaymentSwitch.Processor` |
| `PAY-02` | Accept a payment idempotently | Complete | SourceQuoteId, SubmissionId, fingerprint, and recorded outcomes bind one execution. Processor reserves a verified bill atomically with acceptance, across different quotes and clients. | `PaymentSwitch.Processor` |
| `PAY-03` | Execute the payment saga | Complete | Development charge-first payments charge the full total, then recover company acknowledgment or refund a definitive rejection. Company-first payments retain their original execution and rollback behavior. | `PaymentSwitch.Processor` and `ExternalConnections` |
| `PAY-04` | Refresh payment execution status | Complete | Explicit status and leased recovery use source quote ID without a local PaymentId. Lookup returns the owned payment or its durable rejection. A genuinely missing outcome remains pending. Public Pay and gRPC recovery are tested across restarts. | `PayableServices` and `PaymentSwitch.Processor` |
| `PAY-05` | Cancel a payment execution | Decision required | Cancellation is unsafe after an external side effect may have occurred. Valid cancellation states and compensation rules must be defined first. | `PaymentSwitch.Processor` |
| `PAY-06` | View one completed or failed payment | Partial | Processor has `GetPaymentUseCase`, but no authenticated public HTTP transaction exposes it. | Public boundary decision required |
| `PAY-07` | View payment history | Partial | Processor has `GetPaymentHistoryUseCase`, but `Payments.Api` does not expose payment history. | Public boundary decision required |
| `PAY-08` | View or download a receipt | Missing | No receipt model or public transaction exists. | Public boundary and notification decision required |
| `PAY-09` | Receive payment-result notifications | Partial | Worker can consume email messages, but payment completion does not currently produce the initial durable notification message. | `PaymentSwitch.Processor` and `PaymentSwitch.Worker` |

## Refunds and Disputes

These rows describe later refunds. Processor owns automatic full-charge
compensation for supported Development payments. Worker guards its legacy
refund path by persisted execution mode.

| ID | Transaction | Status | Current implementation or missing work | Owner |
| --- | --- | --- | --- | --- |
| `REF-01` | Request and authorize a refund | Missing | No public or operational use case validates a completed payment, starts refunding, and publishes the initial `refund.request`. | `PaymentSwitch.Processor` plus a public or operational boundary |
| `REF-02` | Execute a refund | Partial | `ProcessRefundRequestUseCase` consumes a message and calls the payment-operator refund adapter, but the initiating transaction is missing. | `PaymentSwitch.Worker` |
| `REF-03` | Retry a failed refund durably | Complete | Refund retry state and redispatch are implemented in Worker. | `PaymentSwitch.Worker` |
| `REF-04` | View refund status | Missing | No client or operator-facing refund-status transaction exists. | Public boundary decision required |
| `REF-05` | Open and manage a dispute or chargeback | Missing | No dispute aggregate, provider integration, or operational workflow exists. | Product and compliance decision required |

## Operations and Support

| ID | Transaction | Status | Current implementation or missing work | Owner |
| --- | --- | --- | --- | --- |
| `OPS-01` | List payments requiring manual intervention | Partial | Verified admins can list stalled charge-first payments by charge, company acknowledgment, or refund stage. Company-first intervention remains outside this queue. | `Payments.Api` and `PaymentSwitch.Processor` |
| `OPS-02` | Reconcile or resolve a manual-intervention payment | Partial | A verified admin can schedule one audited recovery attempt with the existing provider operation key and expected payment version. Processor uses provider evidence to advance the payment. Broader manual resolution remains pending. | `Payments.Api` and `PaymentSwitch.Processor` |
| `OPS-03` | Search payments for client support | Missing | Internal queries are client-oriented and there is no support authorization model. | Operational API decision required |
| `OPS-04` | Receive and reconcile provider webhooks | Partial | Mock setup callbacks authenticate/deduplicate wakeups; dLocal signature primitives are offline-tested. Live setup/payment/refund webhook integration remains pending. | ExternalConnections and PaymentSwitch.Processor |
| `OPS-05` | Reconcile provider settlement and produce financial reports | Missing | There is no settlement-file, ledger, or provider financial-reconciliation workflow. | New financial operations boundary likely required |
| `OPS-06` | Audit privileged operational actions | Partial | Card review and payment recovery record the authenticated actor, reason, and request identity. The payment recovery audit also records the operation stage and expected version. Broader operational auditing remains open. | `Payments.Api` and `PaymentSwitch.Processor` |

## Security and Architecture Prerequisites

These items are not customer transactions, but they block safe completion of
several transactions above.

| ID | Prerequisite | Status | Required outcome |
| --- | --- | --- | --- |
| `SEC-01` | Authenticate client profile, card, and quote operations | Complete | Public profile, stored-card, and quote endpoints validate access tokens and derive client ownership from the token. |
| `SEC-02` | Authenticate every internal Processor gRPC service | Partial | Card/execution gRPC require workload authentication; CardOperationsService requires a distinct operations credential. Payment-query/catalogue gRPC protection remains pending. |
| `SEC-03` | Add administrator/support identities and permissions | Partial | Keycloak roles and VerifiedAdmin catalogue/card-review boundaries exist. Payment/refund manual-review and broader support permissions remain pending. |
| `SEC-04` | Decide whether catalogue browsing is public or authenticated | Complete | The accepted amount-inquiry requirement preserves public catalogue browsing/details; HTTP tests verify that boundary while customer inquiries remain protected. |
| `ARCH-01` | Resolve duplicate catalogue ownership and the external feed | Open | The mock HTTP feed is implemented. Resolve ownership/retirement of Processor's retained catalogue; real-provider catalogue integration remains later work. |
| `ARCH-02` | Resolve company endpoints and credentials server-side | Open | The execution package should carry stable route or destination identifiers rather than connection details. |
| `ARCH-03` | Implement PayableServices application-owned transaction boundaries | Complete | Registered IUnitOfWork owns commits; repositories stage writes. SQL transaction-owned application locks protect catalogue sync/activation, durable inquiry reservations/completion, and atomic quote consumption; HTTP runs outside transactions. |
| `TEST-01` | Add database-backed and transport-level integration coverage | Partial | Quote HTTP/SQL tests cover claim/expiry/fencing/restart/migration; actual handoff-client gRPC/SQL tests cover workload auth, concurrency, late acceptance, and durable rejection. Card/provider suites retain existing coverage. A single public quote-HTTP through Processor runtime smoke and live-provider acceptance remain pending. |

See [Current Problems](current-problems.md) for the detailed architectural
conflicts behind `ARCH-01` through `ARCH-03` and `TEST-01`.

## Product Scope Decisions for Later

The following capabilities are not part of the current one-service,
one-quote, one-payment-attempt model. They should receive transaction IDs only
after the product chooses to support them:

- saved biller accounts and favorites;
- scheduled payments and automatic recurring payments;
- bank accounts, wallets, cash, or other non-card payment methods;
- multi-service carts or split payments;
- partial refunds;
- promotions, loyalty, or rewards;
- payment limits, fraud scoring, KYC, AML, and sanctions workflows beyond the
  current client identity and card-ownership checks.

## Maintenance Rules

- Keep transaction IDs stable after they are referenced by issues, branches,
  commits, or pull requests.
- Update the status and evidence in this document in the same change that
  implements a transaction.
- Update the affected context and flow documents whenever ownership,
  integration, persistence, or observable behavior changes.
- Do not mark a proposed boundary as implemented until the code follows it.
- Add an architectural decision record when the team accepts a decision that
  changes ownership or cross-service contracts.
