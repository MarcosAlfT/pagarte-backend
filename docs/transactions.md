# Payment Application Transaction Inventory

This document is the ordered implementation inventory for Pagarte client,
payment, and operational transactions. It separates current implementation
facts from planned work so the team can deliver and close one transaction at a
time.

Baseline reviewed against `main` commit `e5b50d0` on 2026-10-08. The
`codex/keycloak-security-migration` branch now contains Keycloak JWT validation,
profile identity mapping, and administrator policies. The status table below
remains the baseline transaction inventory until the client applications and
real Keycloak runtime tests complete; it must not be treated as a verified
post-migration transaction count.

## Status Definitions

| Status | Meaning |
| --- | --- |
| Complete | The intended actor can execute the transaction end to end, with its current authentication, persistence, and failure behavior implemented. |
| Partial | Supporting code exists, but the end-to-end transaction or intended public/operational entry point is incomplete. |
| Missing | No complete application transaction exists. |
| Security gap | The transaction exists, but its current authorization boundary is unsafe or inconsistent with the documented intent. |
| Decision required | Coding should wait for an explicit product or architecture decision. |
| Open | A documented architecture problem remains unresolved. |

A transaction should move to `Complete` only after its use case, entry point,
authorization, persistence, failure behavior, tests, and relevant documentation
are complete.

## Current Snapshot

The inventory currently tracks 55 client and operational transactions:

| Status | Count |
| --- | ---: |
| Complete | 24 |
| Partial | 9 |
| Missing | 19 |
| Security gap | 1 |
| Decision required | 2 |

Eight security, architecture, and testing prerequisites are tracked separately
near the end of this document.

## Recommended Work Order

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
| `AUTH-01` | Register client user | Complete | `RegisterUserUseCase` creates a user pending email confirmation. | `Identity.Client` |
| `AUTH-02` | Confirm email | Complete | `ConfirmEmailUseCase` validates the confirmation token and activates the account. | `Identity.Client` |
| `AUTH-03` | Log in with password | Complete | `LoginWithPasswordUseCase` authenticates the user and issues access and refresh tokens. | `Identity.Client` |
| `AUTH-04` | Refresh client session | Complete | `RefreshTokenUseCase` validates and rotates refresh tokens. | `Identity.Client` |
| `AUTH-05` | Log out | Complete | `LogoutUseCase` revokes refresh-token access. | `Identity.Client` |
| `AUTH-06` | Recover forgotten password | Complete | Forgot-password and reset-password use cases are implemented. | `Identity.Client` |
| `AUTH-07` | Change password | Complete | `ChangePasswordUseCase` supports an authenticated password change. | `Identity.Client` |
| `AUTH-08` | Create person or organization profile | Complete | Separate person and organization creation use cases are implemented. | `ClientProfiles` |
| `AUTH-09` | Manage addresses and phone numbers | Complete | Create, read, update, delete, and primary-contact operations are implemented. | `ClientProfiles` |
| `AUTH-10` | Complete registration and profile onboarding as one client journey | Partial | Identity registration and profile creation exist, but no workflow coordinates their completion or recovery. | Cross-context: `Identity.Client` and `ClientProfiles` |
| `AUTH-11` | View and revoke active sessions or devices | Missing | Refresh tokens can be revoked, but there is no client-visible session inventory. | `Identity.Client` |
| `AUTH-12` | Enroll and use MFA or passkeys | Missing | A passkey domain entity exists, but enrollment, challenge, and authentication transactions do not. | `Identity.Client` |
| `AUTH-13` | Close a client account | Partial | Client-profile deletion exists; coordinated identity closure, stored-card handling, and legal data retention do not. | Cross-context decision required |

## Service Catalogue and Amount Inquiry

The accepted [catalogue and amount-inquiry requirement](requirements/mock-amount-inquiry.md)
covers every transaction in this section and links to `QUOTE-08`. The planned
mock company supplies both catalogue feeds and balances until real companies
are available. Requirement coverage does not change implementation status;
close each row only after its transaction-specific acceptance checks pass.

| ID | Transaction | Status | Current implementation or missing work | Owner |
| --- | --- | --- | --- | --- |
| `CAT-01` | Browse payable services | Complete | `GetCatalogueUseCase` returns active services that allow quoting and payment. | `PayableServices` |
| `CAT-02` | Search and filter payable services | Partial | Exact category filtering exists. Text, provider, country, and service-code search do not. | `PayableServices` |
| `CAT-03` | View service details and required reference fields | Missing | There is no public service-detail transaction that supplies the data needed to build a payment form. | `PayableServices` |
| `CAT-04` | Synchronize the external service catalogue | Partial | `SyncExternalCatalogueUseCase` exists but currently reads Processor's retained catalogue instead of a real external feed. | `PayableServices` and `ExternalConnections` |
| `CAT-05` | Maintain catalogue and activate payment routes | Security gap | Historical baseline status; the current code has `VerifiedAdmin` protection. Administrator runtime acceptance and route-validation checks remain to be verified under the requirement. | `PayableServices` |
| `INQ-01` | Validate the customer reference and check the amount due | Missing | The accepted [mock amount-inquiry requirement](requirements/mock-amount-inquiry.md) defines deterministic external HTTP scenarios; the adapter and public transaction are not implemented. | `PayableServices` and `ExternalConnections` |
| `INQ-02` | Persist a stable amount-inquiry result | Missing | The accepted requirement defines ownership, idempotency, amount, currency, provider ID, references, and 15-minute expiry; persistence is not implemented. | `PayableServices` |

Both `CAT-05` endpoints now require the `VerifiedAdmin` policy in the merged
Keycloak implementation. The historical baseline status above is retained
until transaction acceptance verifies an administrator token and rejects
anonymous, client, and support tokens; it does not mean the current controllers
lack authorization attributes.

The catalogue currently returns a configured base amount. It does not represent
a real-time amount owed by the customer to the external company.

## Payment Methods

| ID | Transaction | Status | Current implementation or missing work | Owner |
| --- | --- | --- | --- | --- |
| `CARD-01` | Add a stored card | Complete | `RegisterCreditCardUseCase` registers the card with the operator and stores its token and safe metadata. | `Payments.Api` and `PaymentSwitch.Processor` |
| `CARD-02` | List stored cards | Complete | Client-owned cards are available through the authenticated card API. | `Payments.Api` and `PaymentSwitch.Processor` |
| `CARD-03` | View one stored card | Complete | The transaction checks client ownership. | `Payments.Api` and `PaymentSwitch.Processor` |
| `CARD-04` | Update card metadata or select the default card | Complete | `UpdateCreditCardUseCase` implements both operations. | `Payments.Api` and `PaymentSwitch.Processor` |
| `CARD-05` | Delete a stored card | Complete | `DeleteCreditCardUseCase` is exposed through the authenticated card API. | `Payments.Api` and `PaymentSwitch.Processor` |
| `CARD-06` | Complete a provider challenge such as 3-D Secure | Missing | No redirect, challenge, callback, or continuation transaction is modeled. | Product and payment-operator decision required |
| `CARD-07` | Tokenize a card without sending PAN and CVV through Pagarte services | Decision required | The current backend receives card data and forwards it to the operator. Provider-hosted or client-side tokenization would reduce PCI scope. | Security and payment-operator decision required |

## Quotes

| ID | Transaction | Status | Current implementation or missing work | Owner |
| --- | --- | --- | --- | --- |
| `QUOTE-01` | Create an itemized quote | Complete | `CreateQuoteUseCase` validates the service, route, references, currency, and amount composition. | `PayableServices` |
| `QUOTE-02` | Display base value, taxes, fees, discounts, and total | Complete | The immutable quote stores and returns ordered amount components and the complete total. | `PayableServices` |
| `QUOTE-03` | Retrieve a quote by ID | Complete | `GetQuoteUseCase` returns a quote only to its owning client. | `PayableServices` |
| `QUOTE-04` | Reject an expired quote | Complete | Unpaid quotes expire after the configured 60-minute lifetime. | `PayableServices` |
| `QUOTE-05` | List a client's quotes | Missing | Only retrieval by a known quote ID exists. | `PayableServices` |
| `QUOTE-06` | Cancel an unpaid quote | Missing | No explicit cancellation status or use case exists. | `PayableServices` |
| `QUOTE-07` | Requote an expired or declined attempt | Partial | A new quote can be created manually, but no dedicated client transaction carries forward safe inputs. | `PayableServices` |
| `QUOTE-08` | Create a quote from a verified amount inquiry | Missing | The accepted [mock amount-inquiry requirement](requirements/mock-amount-inquiry.md) makes a valid persisted inquiry the source of amount and currency; quote binding is not implemented. | `PayableServices` |

One quote currently represents one payable service and one payment attempt.

## Payment Execution

| ID | Transaction | Status | Current implementation or missing work | Owner |
| --- | --- | --- | --- | --- |
| `PAY-01` | Confirm and pay a quote with a stored card | Complete | `ConfirmQuoteUseCase` sends the immutable quote package and selected card to Processor. | `PayableServices` and `PaymentSwitch.Processor` |
| `PAY-02` | Accept a payment idempotently | Complete | `AcceptConfirmedQuoteUseCase`, the unique source quote, and the package fingerprint prevent duplicate execution. | `PaymentSwitch.Processor` |
| `PAY-03` | Execute the company-first payment saga | Complete | The saga checks card readiness, pays the company, charges the full total, reconciles unknown outcomes, and rolls back when required. | `PaymentSwitch.Processor` and `ExternalConnections` |
| `PAY-04` | Refresh payment execution status | Complete | The client can retrieve execution state through the quote execution endpoint. | `PayableServices` and `PaymentSwitch.Processor` |
| `PAY-05` | Cancel a payment execution | Decision required | Cancellation is unsafe after an external side effect may have occurred. Valid cancellation states and compensation rules must be defined first. | `PaymentSwitch.Processor` |
| `PAY-06` | View one completed or failed payment | Partial | Processor has `GetPaymentUseCase`, but no authenticated public HTTP transaction exposes it. | Public boundary decision required |
| `PAY-07` | View payment history | Partial | Processor has `GetPaymentHistoryUseCase`, but `Payments.Api` does not expose payment history. | Public boundary decision required |
| `PAY-08` | View or download a receipt | Missing | No receipt model or public transaction exists. | Public boundary and notification decision required |
| `PAY-09` | Receive payment-result notifications | Partial | Worker can consume email messages, but payment completion does not currently produce the initial durable notification message. | `PaymentSwitch.Processor` and `PaymentSwitch.Worker` |

## Refunds and Disputes

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
| `OPS-01` | List payments requiring manual intervention | Missing | The saga can enter manual intervention, but no operator queue exists. | `PaymentSwitch.Processor` plus an operational API |
| `OPS-02` | Reconcile or resolve a manual-intervention payment | Missing | No authorized operational command records the resolution and safely resumes or closes the saga. | `PaymentSwitch.Processor` |
| `OPS-03` | Search payments for client support | Missing | Internal queries are client-oriented and there is no support authorization model. | Operational API decision required |
| `OPS-04` | Receive and reconcile provider webhooks | Missing | Current adapters use request/response calls and polling reconciliation only. | `ExternalConnections` and `PaymentSwitch.Processor` |
| `OPS-05` | Reconcile provider settlement and produce financial reports | Missing | There is no settlement-file, ledger, or provider financial-reconciliation workflow. | New financial operations boundary likely required |
| `OPS-06` | Audit privileged operational actions | Missing | No administrator identity model or append-only operational audit transaction exists. | Identity and operations decision required |

## Security and Architecture Prerequisites

These items are not customer transactions, but they block safe completion of
several transactions above.

| ID | Prerequisite | Status | Required outcome |
| --- | --- | --- | --- |
| `SEC-01` | Authenticate client profile, card, and quote operations | Complete | Public profile, stored-card, and quote endpoints validate access tokens and derive client ownership from the token. |
| `SEC-02` | Authenticate every internal Processor gRPC service | Partial | `PaymentExecutionService` validates the workload API key; card, payment-query, and catalogue gRPC services still require an explicit workload boundary. |
| `SEC-03` | Add administrator/support identities and permissions | Missing | Maintenance, route activation, refunds, manual review, and support search need roles or scoped permissions. |
| `SEC-04` | Decide whether catalogue browsing is public or authenticated | Decision required | The controller is currently public while the context documentation describes authenticated catalogue access. |
| `ARCH-01` | Resolve duplicate catalogue ownership and the external feed | Open | Choose the canonical catalogue and implement the real external catalogue adapter. |
| `ARCH-02` | Resolve company endpoints and credentials server-side | Open | The execution package should carry stable route or destination identifiers rather than connection details. |
| `ARCH-03` | Implement PayableServices application-owned transaction boundaries | Open | Register `IUnitOfWork`, remove repository-owned commits, and define the catalogue-sync commit strategy. |
| `TEST-01` | Add database-backed and transport-level integration coverage | Partial | Cover authentication, duplicate confirmations, SQL concurrency, restart recovery, provider contracts, and production-like migrations. |

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
