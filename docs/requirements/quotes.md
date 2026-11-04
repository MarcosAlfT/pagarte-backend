# Requirement: Complete the Quote Lifecycle

## Status and Scope

| Field | Value |
| --- | --- |
| Status | Accepted product definitions; implementation pending |
| Date | 2026-11-04 |
| Transactions | `QUOTE-01` through `QUOTE-08` |
| Primary owner | PayableServices |
| Integration owners | PaymentSwitch.Processor and ExternalConnections |
| Pricing architecture | Composite for component structure; Strategy for calculation rules |
| User experience | Saved payment summary, resumable across card registration and other tasks |

The client shall create, review, retrieve, list, cancel, and renew a quote and
submit it for payment with an exact approved amount. Quotes remain internal
business records; the mobile app may label them Payment summary.

The accepted [review and payment flow](../flows/quote-review-and-payment.md)
replaces the proposed five-minute hard cutoff with freshness and mandatory
verification before new payment submission. The current implementation still
enforces time expiry; this document does not mark the new behavior implemented.

## Baseline and Superseded Rules

- Creation, exact itemized totals, owner-only retrieval, confirmation, and
  inquiry-to-quote atomic consumption exist. Inquiry-backed creation takes its
  base from provider evidence and excludes client overrides.
- Legacy configured-price quotes currently expire after 60 minutes. Inquiry-backed
  quotes expire no later than their inquiry. Confirmation rejects an expired
  unpaid quote; retrieval can still return its stored Unpaid state.
- Listing and cancellation use cases are missing. Cancelled already exists in
  the enum; the inventory's earlier assertion that the status was absent was
  inaccurate. Dedicated requoting is missing.
- Confirmation can succeed remotely before local PaymentId persistence. The
  current refresh path refuses lookup without that local ID. Submission recovery
  and cancellation must account for this gap.
- Existing inquiry freshness limits remain limits on balance evidence. They
  shall no longer close a saved quote or force the customer to repeat inputs.
  This supersedes the quote-lifetime cap in MAI-007/MAI-009 of the
  [amount-inquiry requirement](mock-amount-inquiry.md) for the new lifecycle.
- The legacy execution expiry field/validator must be revised together with
  the handoff. Removing only the API's expiry check is insufficient.

## Ownership and Boundaries

- PayableServices owns quote snapshots, pricing, list/detail, freshness,
  cancellation, replacements, approval evidence, and durable submission intent.
- Processor owns payment acceptance, selected-card eligibility, unique payment
  creation, company payment, charging, compensation, and execution recovery.
- ExternalConnections owns provider HTTP translation and bill/payment evidence.
  Provider-neutral interfaces belong to the consuming application boundary.
- The company supplies authoritative bill identity/status/amount/version and
  conditional payment or equivalent duplicate protection. The mock must exercise
  those contracts while real company integrations are unavailable.
- The mobile app retains/retrieves the quote ID across navigation and card
  registration. It cannot choose an amount, provider state, or execution result.
- dLocal and other payment operators remain outside quote pricing rules. This
  work preserves provider-neutral card IDs and the existing operator registry.

## Functional Requirements

### `QR-001` Create and Own a Quote (`QUOTE-01`, `QUOTE-08`)

- Require VerifiedClient and the resolved internal ClientId for every public
  quote operation. Unknown and foreign IDs have equivalent not-found responses.
- Accept a valid owned inquiry for inquiry-backed services. Derive service,
  currency, references, bill identity, and base from verified evidence. Reject
  attempts to override them; keep exactly one original quote per consumed inquiry.
- Stale initial evidence triggers an explicit refresh workflow using retained
  inputs; never silently treat the old balance as fresh. The client need not
  rebuild its service/reference form. Pending refresh returns resumable state.
- Preserve atomic quote creation/inquiry consumption and repeat-request replay.
  Give legacy configured-price creation equivalent client-scoped idempotency.
- Persist the financial snapshot, source evidence, pricing version, ownership,
  and UTC timestamps. Catalogue changes must not rewrite an existing snapshot.
- Keep legacy configured-price quoting available, but it must not claim external
  unpaid-bill verification without a corresponding provider capability. Require
  a verified bill adapter for bill-payment services using this requirement.

### `QR-002` Composite and Strategy Pricing (`QUOTE-01`, `QUOTE-02`)

- Use a Composite structure: monetary leaves represent service amount, fee,
  tax, penalty, or discount; groups contain leaves or groups and aggregate them.
  A leaf contributes exactly once. Group subtotals are presentation values,
  never additional charged components. Reject cycles, duplicate membership,
  excessive nesting, and mixed currencies.
- Use Strategy implementations for versioned calculation rules. Include
  provider base, configured fixed amount, percentage fee/tax, and fixed or
  percentage discount. Specify percentage basis and dependencies explicitly;
  do not assume every percentage applies to the grand total.
- Select strategies from trusted configuration through a registry. Domain
  calculation and transport code must not branch on operator names. Adding a
  supported pricing strategy must not require rewriting quote use cases.
- Resolve dependency ordering deterministically; reject circular or missing
  bases. Use decimal calculations, currency-defined minor units, ToEven by
  default, checked arithmetic, and per-component rounding before summation.
  An explicit configured rounding override must be snapshotted.
- Store final leaves, group structure, strategy IDs/versions, applicable rates
  and bases, and totals as evidence. A read must not execute pricing strategies.
  Processor receives finalized leaves using the existing exact money model.
- The total must be positive and equal the sum of leaves. Discounts are
  explicit negative contributions; invalid amounts and overflow fail locally.
- Use the shared currency scale rather than the inquiry path's current /100
  assumption. USD remains the initial company fixture currency; this does not
  expand the accepted external feed to other currencies.
- Display every monetary contribution needed to explain the payable total.
  Internal metadata may be omitted; IsVisibleToClient is not authorization.
  Do not expose sensitive internal fields by returning hidden lines unchanged.
- Price calculation creates a snapshot once. Revalidation may calculate a
  candidate snapshot for comparison, but cannot mutate the reviewed snapshot.

### `QR-003` Retrieval, Freshness, and Resume (`QUOTE-03`, `QUOTE-04`)

- Store lifecycle state separately from freshness. A saved unpaid quote can
  be NeedsRefresh without becoming a closed or deleted quote.
- Remove the five-minute hard cutoff and the inquiry-lifetime cap on saved
  quote usability. Evidence freshness deadlines cause revalidation, not forced
  re-entry of the customer's details. Revalidate on every new Pay submission,
  even if the displayed quote is still considered fresh.
- Return immutable breakdown, safe reference summary, effective lifecycle,
  freshness, evidence timestamps, associated payment/replacement IDs, and
  available actions. Expose safe reasons when payment is unavailable.
- Opening or listing a quote reads stored state; it must not charge, submit,
  or automatically contact the company. An explicit refresh may obtain new
  evidence without creating a payment.
- Preserve the quote ID through card-registration navigation, app restart, and
  login renewal. The backend keeps owned retrieval/listing available; mobile
  implementation is a linked acceptance dependency rather than backend code.
- New-card registration neither edits the quote nor reserves a payment. The
  client selects the resulting Pagarte card ID when it proceeds to Pay.
- Previously accepted payments continue independently of quote freshness.
  Existing Expired rows without execution become refreshable under migration;
  Paid, Cancelled, and final failed histories are never reopened automatically.

### `QR-004` Revalidate Before Payment

- Serialize confirmation against cancellation/requote and durably reserve a
  submission attempt before external verification. Bind its key, owner, quote
  snapshot/version, and selected card. Do not hold SQL locks during HTTP calls.
- First discover any existing or unresolved execution by source quote ID.
  Replay/reconcile it; never interpret its now-paid bill as a new validation
  failure or issue a second payment.
- For a new execution, query the company using a fresh operation key. Replaying
  the old inquiry key only returns historical evidence and is not revalidation.
- Check the same bill/obligation, not merely the account balance. Preserve a
  provider bill ID or a proven equivalent period/version identity. A later
  month's same-value bill is a different obligation requiring a new quote.
- Recalculate current price components with current eligible service/route and
  pricing configuration. Classify results as described below; malformed or
  mismatched provider evidence cannot authorize payment.

| Result | Required behavior |
| --- | --- |
| Same unpaid bill, currency, amount, and material breakdown | Persist fresh validation linked to the original quote and continue the same Pay action without another review screen. |
| Changed amount/currency/bill identity or material price breakdown | Create a linked immutable replacement, show differences, and require explicit approval. Do not submit payment for the original. |
| Already paid / nothing due | Record safe evidence and disable new payment for this obligation. Do not label it a successful Pagarte payment or invent a receipt. Distinguish confirmed AlreadyPaid from NothingDue where supported. |
| Unavailable / timeout / malformed evidence | Keep the quote and return a retryable or review-required outcome; do not submit a payment or guess a balance. |
| Service removed or unsupported route | Keep history and explain unavailability; do not redirect the payment to a different destination silently. |

- Store each validation result separately from the original inquiry and quote.
  Retain both the originally reviewed evidence and the evidence used for payment.
  One inquiry/one original quote remains intact; refresh evidence is not a
  reusable second quote authorization.
- Bind execution to the approved snapshot and latest validation revision with
  a server-controlled validity window or provider reservation/version. Its exact
  lifetime is integration policy, not a customer-facing five-minute countdown.
  If it becomes stale before submission, revalidate transparently. Accepted
  execution retries/status recovery must not be rejected by that deadline.
- A read-before-pay cannot eliminate the race with payment through another
  channel. Processor's company payment must conditionally accept the same bill
  and expected amount/version, or use equivalent provider reservation/atomic
  duplicate protection. Idempotency alone covers repeats of one operation;
  it does not stop a different channel paying the same bill.
- If the company rejects a paid/changed obligation at that final boundary,
  stop before the operator charge. Unknown company acceptance is reconciled,
  never treated as definite non-payment. Preserve company-first charging and
  compensation behavior, including existing finalized full-total semantics.
- Enable real bill-payment execution only when the adapter's bill identity and
  conditional/duplicate protection are demonstrated. Missing capabilities must
  remain an explicit blocker, not a claim of guaranteed duplicate prevention.

### `QR-005` List Quotes (`QUOTE-05`)

- Provide owner-only history with status, service, and created-date filters,
  stable newest-first ordering with ID tie-breaker, and bounded pagination
  (default 20, maximum 100). Reject invalid bounds and reversed date ranges.
- Return summaries with total/currency, freshness, lifecycle, and linked IDs.
  Freshness filters derive from server time consistently with detail responses.
  Empty results succeed; quote history is distinct from payment history.

### `QR-006` Cancel a Quote (`QUOTE-06`)

- Allow cancellation of unpaid quotes only when no submission or execution can
  still take effect. Repeated cancellation returns the cancelled result.
- Keep the snapshot/history and consumed inquiry binding. Cancellation does
  not refund, cancel an accepted payment, or make an inquiry reusable.
- A concurrent confirmation/cancellation must have one durable winner. Pending
  submission or uncertain acceptance blocks cancellation until reconciled.
  Checking only local PaymentId is not proof no payment exists.
- Use concurrency fencing so an expired worker lease or delayed response
  cannot submit or overwrite state after cancellation. Do not claim cancelled
  while a previous worker can still reach Processor with valid authorization.

### `QR-007` Requote and Approval (`QUOTE-07`)

- Create a new immutable quote linked by predecessor/replacement IDs. Refresh
  company evidence and pricing; reuse validated references as convenience only.
- Require explicit approval of a changed replacement's ID and snapshot before
  payment. Another change during its Pay action repeats this approval rule.
- Idempotent requests and uniqueness prevent multiple replacements for the same
  operation. A superseded original cannot initiate a new payment.
- Allow replacement for refresh-required, cancelled, or final failed quotes
  when no execution remains unresolved. Paid quotes cannot be used to bypass
  a settled bill; a new bill starts a new inquiry/quote.
- Never offer safe retry/requote during processing, uncertain company/card
  outcomes, incomplete rollback, or manual review.

### `QR-008` Submission Recovery and Status Consistency

- Persist submission intent before the Processor call and reconcile by
  ClientId + SourceQuoteId even when local PaymentId is missing. One quote
  retains at most one accepted payment; package/card conflicts are rejected.
- A lost response or failed local commit leaves resumable pending state.
  Refresh and restart recovery discover the authoritative Processor result.
  Only a definitive rejection with no execution can release the attempt for
  another selected card, cancellation, or replacement.
- Define versioned transitions for unpaid, validation/submission pending,
  processing, paid, payment failed, cancelled, superseded, and no-longer-due
  outcomes. These may be separate related records rather than one overloaded
  enum. Freshness is separate from all terminal business outcomes.
- Prevent stale responses from regressing a final state or overriding a newer
  validation/submission. Persist safe errors and correlation/version evidence.
- No automatic charge may result merely from listing, retrieval, background
  price refresh, or successful card registration.

## Acceptance and Examples

| Transaction | Required evidence |
| --- | --- |
| QUOTE-01 | Owner validation, valid legacy/inquiry paths, idempotency conflicts/replay, and immutable versioned creation. |
| QUOTE-02 | Nested Composite groups, fixed/percentage Strategies, dependency rejection, rounding/discount/overflow tests, and no double-counted subtotals. |
| QUOTE-03 | Resume by ID after another task/restart; foreign ID protection; stored reads show consistent state and make no provider/payment call. |
| QUOTE-04 | Old quote remains accessible; fresh evidence required at Pay; expired evidence never authorizes a new execution; accepted work continues. |
| QUOTE-05 | Stable pagination and owner/service/status/date/freshness filtering without cross-client leakage. |
| QUOTE-06 | Repeat cancel, confirm/cancel races, stale worker fencing, and unresolved-submission blocking. |
| QUOTE-07 | Linked immutable replacement, fresh evidence, explicit changed-price approval, concurrent deduplication, and unresolved-payment blocking. |
| QUOTE-08 | Original inquiry binding remains atomic/unique; append-only revalidation evidence and no duplicate base amount. |

Required end-to-end scenarios:

1. Quote is USD 102. Client registers a card for 12 minutes, returns, and pays.
   Same unpaid bill and price cause one payment with no forced restart.
2. Company balance changes while the client is away. A replacement shows the
   new total; no payment starts before approval.
3. Customer pays through their bank before tapping Pay. Show already paid or
   nothing due with evidence; no operator charge occurs.
4. Another channel pays between revalidation and company acceptance. The
   conditional company operation rejects the duplicate before a card charge.
5. Provider verification fails. The quote survives and retry is available.
6. Processor accepts but response/local commit is lost. Status refresh finds
   the same payment even with no local PaymentId and after evidence expires.
7. Cancel, Pay, and requote race across API instances. Durable transitions
   produce one valid outcome, including worker crash and lease-recovery cases.
8. The same account has a new month's bill for the same amount. It does not
   silently replace the original obligation or reuse its authorization.

The mock company must expose mutable bill state, stable bill identity/version,
external-payment simulation, and conditional payment tests. Existing fixed
balance fixtures alone cannot prove revalidation or cross-channel protection.
Use isolated test state and controlled clocks. Test real HTTP/gRPC boundaries,
SQL concurrency/migrations, and failure injection in addition to unit tests.
Mobile navigation and real-company capability acceptance remain explicit checks.

## Delivery and Compatibility

1. Specify persisted quote/freshness/validation/submission records and API error
   outcomes; define fencing and provider evidence contracts before mutations.
2. Implement Composite/Strategy pricing with exact existing money behavior.
3. Add resume/list/detail and lifecycle migration, preserving historical
   snapshots and original inquiry links. No silent total recalculation.
4. Add fresh bill verification, candidate pricing, replacement approval, and
   conditional company-payment mock/adapter support.
5. Revise execution contract/validator and submission recovery as one compatible
   change. Version or coordinate deployment so old expiry enforcement cannot
   reject the new flow and new callers cannot bypass old protections.
6. Complete cancellation/requote with race protection and acceptance tests.
7. Update transaction status, current-state flows/contexts, and test evidence.

Provider bill identity, conditional acceptance, verification freshness policy,
and recovery support must be confirmed per integration. New product pricing
rates/tax policy are configuration decisions; this requirement supplies the
calculation mechanisms, not jurisdiction-specific tax rules.

## Out of Scope

Multi-service carts, partial payments, currency conversion, recurring automatic
payments, refund product changes, card-registration redesign, production mobile
UI implementation in this backend repository, and forced provider-token migration.

## Related Documents

- [Accepted architectural decision](../decisions/0005-resumable-quotes-and-payment-revalidation.md)
- [Current quote implementation](../flows/quote-lifecycle.md)
- [Payment lifecycle](../flows/payment-lifecycle.md)
- [Money rules](../decisions/0004-money-handling.md)
- [Transaction inventory](../transactions.md)
