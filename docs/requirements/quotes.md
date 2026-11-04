# Requirement: Complete Quotes and Payment Confirmation

## Status and Scope

| Field | Value |
| --- | --- |
| Status | Accepted revised product definitions; implementation pending |
| Transactions | QUOTE-01 through QUOTE-08, with required payment-execution changes |
| Primary owner | PayableServices |
| Integration owners | PaymentSwitch.Processor and ExternalConnections; coordinate refund compatibility with Worker |
| Patterns | Composite for quote items; Strategies for pricing and business-key selection |
| Quote lifetime | Exactly five minutes from server-side creation |
| Customer flow | Consult, review, Pay; consult again after expiry or invalidation |

This revision supersedes the earlier resumable-freshness and linked-replacement
proposal. There is no automatic quote refresh or additional approval workflow.
Tapping Pay authorizes the fixed displayed total. A valid, unchanged, unpaid
bill proceeds directly. The [target flow](../flows/quote-review-and-payment.md)
charges the card first, then notifies the company: OK completes the payment;
definitive NOT OK requires a full card refund. Company notification records the
bill payment; it does not transfer money. Later reconciliation and settlement
with the company are outside this application.

## Current Baseline and Required Changes

- Itemized immutable quotes, owner-only retrieval, inquiry consumption, and
  confirmation exist. Listing and cancellation use cases are missing.
- Current legacy quotes expire after 60 minutes; inquiry-backed quotes inherit
  inquiry expiry. The target uses CreatedAt + five minutes for new quotes.
- Current Processor execution calls the company before charging the card and
  rolls back the company on charge failure. The new order is not implemented.
- Current status refresh requires a local PaymentId, so lost acceptance responses
  need recovery by source quote ID before any retry or expiry rejection.
- No production data exists. Historical/test records still must not acquire
  invented bill identities or pricing evidence during migration.

## Ownership

- PayableServices owns quote items, pricing and key Strategies, business-key
  evidence, inquiry binding, expiry, history, cancellation, and submission intent.
- Processor owns payment acceptance, card eligibility, durable charge, company
  notification, compensation refund, duplicate protection, and recovery.
- ExternalConnections translates provider-neutral inquiry, notification/status,
  charge/status, and refund/status contracts. It does not decide payment state.
- Processor owns compensation for company rejection. Existing Worker refund
  work must not independently refund that same charge. Preserve one durable
  refund operation and its idempotency key across all entry points.
- The company supplies bill evidence and acknowledges or rejects notification
  after the charge. The operator charges/refunds the complete quote total.
- Mobile sends owned quote ID and Pagarte card ID. It cannot override amounts,
  identity selections, route, or provider results. Operator replacement remains
  behind adapters and the existing registry.

## Functional Requirements

### QR-001: Create and Identify a Quote (QUOTE-01, QUOTE-08)

- Require VerifiedClient and the resolved internal ClientId. Unknown and foreign
  quote IDs have equivalent not-found responses.
- Inquiry-backed creation consumes an owned, unexpired, positive inquiry exactly
  once in the same transaction as quote creation. Identical retries return the
  same quote; conflicting reuse fails. Client overrides are rejected.
- Use a Composite to organize quote items and a service-specific key Strategy
  to select the items that identify the obligation. Mandatory identity evidence
  comes from validated references and company evidence, not client-selected flags.
- Snapshot the key Strategy ID/version, selected item names and canonical values,
  and derived business key. Scope the identity to the company/service. A bill
  date or billing period is selected where that service uses it; the Strategy
  defines which date and its format. Never use inquiry time or charge time as
  the bill date. Missing required identity items prevent bill-payment quoting.
- Example electricity Strategy: company + service + account + billing period.
  Account 00123 for 2026-10 differs from account 00123 for 2026-11, even when both
  bills are USD 100. Changing any selected key value means a different obligation
  and requires a new inquiry/quote. Changing a non-key item does not change the
  business key, but changed bill amount/currency still invalidates payment.
- Canonicalization must be deterministic, preserve significant leading zeroes,
  and avoid ambiguous concatenation. Strategy-version changes alone must not
  permit paying the same obligation twice; activation requires compatibility
  mapping or blocking affected outstanding obligations.
- Technical QuoteId, request idempotency key, and bill business key are distinct.
  Several inquiries may observe one bill; new QuoteIds cannot bypass bill-level
  duplicate protection. A key is not an authorization credential.
- Keep configured-price creation for non-inquiry services, using the same pricing,
  lifetime, and ownership rules. Do not claim bill verification for services
  without authoritative inquiry/identity support.

### QR-002: Composite and Strategy Pricing (QUOTE-01, QUOTE-02)

- Monetary leaves represent service amount, fees, taxes, penalties, and discounts.
  Groups aggregate leaves or groups without contributing a second charge.
  Identity/reference items contribute no money merely because they are in the
  Composite. Reject cycles, duplicate membership, and mixed currencies.
- Pricing Strategies cover provider base, fixed amounts, percentage fee/tax,
  and fixed/percentage discounts. Configure calculation bases/dependencies
  explicitly; reject missing bases and dependency cycles.
- Select pricing and key Strategies through trusted registries. Use distinct
  responsibilities; do not hardcode service/operator branches in use cases.
- Use decimal calculation, ISO currency scale, checked minor-unit arithmetic,
  and per-leaf ToEven rounding before summation. ToEven is the only supported
  rounding mode in this scope; reject overrides consistently at both boundaries.
- A positive total equals the sum of finalized leaves exactly. Discounts are
  negative leaves. Groups are display subtotals, never independent charges.
- Snapshot leaves, grouping, rates/bases, Strategy IDs/versions, currency, route,
  and total once. Reads and payment submission do not reprice this snapshot.
- All client-relevant charges are visible; internal metadata and credentials
  remain private. USD is the initial company fixture currency; use shared money
  scale logic rather than assuming every currency has two decimals.
- During the five-minute validity window, a configuration change with the same
  total but different fee/tax allocation does not rewrite the quote. Neither a
  new Strategy version nor new rounding configuration changes its amounts.
  A disabled/invalid route blocks payment; never silently substitute a route.

### QR-003: Retrieve and Expire (QUOTE-03, QUOTE-04)

- Set ExpiresAt = CreatedAt + five minutes using server UTC. At now >= ExpiresAt,
  an unsubmitted quote cannot initiate payment. Return the effective expired
  state consistently in detail, list, and submission responses.
- Inquiry evidence must be fresh at creation; its earlier expiry does not shorten
  or extend this fixed quote lifetime. Every new Pay requires a new bill check.
  This revises the inquiry cap in MAI-007/MAI-009 for future quote implementation.
- Expiry preserves the original snapshot/history but requires the client to
  consult again. Never extend the existing deadline or refresh it in place.
- Returning from card registration within five minutes permits normal Pay.
  Returning after five minutes requires a new inquiry and quote. Registration,
  retrieval, and listing never charge or automatically consult the company.
- Processor must durably accept a new execution before ExpiresAt; merely tapping
  Pay or reserving submission intent does not extend the deadline. Verification
  or transport that crosses the deadline cannot authorize a new execution.
- An execution already accepted continues through confirmation/refund/recovery
  after expiry. Recover an existing or uncertain submission before reporting an
  expired quote as safe to replace. No customer-facing freshness lifecycle exists.

### QR-004: Verify, Charge, Notify, Confirm or Refund

- Tapping Pay is the customer's authorization for the displayed immutable total;
  no separate approval is required when checks succeed.
- Reserve durable submission intent and check for an existing/unresolved payment
  first. For new execution, obtain fresh company evidence with a new inquiry
  operation key, not a replay of the creation inquiry. Store verification evidence
  separately without consuming another inquiry into the same quote.
- Verify the same Strategy-selected business key, unpaid bill, service amount,
  currency, and eligible original route. Do not recalculate fees/taxes at Pay.

| Verification result | Behavior before charging |
| --- | --- |
| Same unpaid bill, service amount and currency; valid quote/route/card | Continue the same Pay action with the stored total. |
| Any selected key value, service amount, or currency changed | Invalidate the old quote; require a new consultation and quote. |
| Already paid / nothing due | Do not charge; explain the outcome without inventing a Pagarte payment. |
| Unavailable / malformed evidence | Return a safe verification error; retry only while the quote remains valid. |
| Expired quote or invalid route | Do not charge; require a new consultation when the service is available. |

- Processor atomically prevents concurrent or already successful payments for
  the same bill key across quotes and clients, without exposing another client's
  payment. Pending charge, company confirmation, or refund keeps that protection.
- Check card eligibility/readiness and charge the full total once. Only confirmed
  charge success allows company notification. Unknown charge results require
  reconciliation; they cannot authorize notification or a new charge key.
- Notify the company with the bill identity, expected service amount/currency,
  Pagarte payment reference, charge evidence, and stable notification key. Keep
  service amount and charged total as distinct fields; never call the full total
  the amount due to the service company. Store the breakdown for later accounting.
- Example: service USD 100 + fee USD 2 = operator charge USD 102. Company
  confirmation identifies the USD 100 obligation; USD 102 is the separate charged
  total if included in its adapter contract. This call transfers no funds.
- Company OK must acknowledge this exact payment/bill and record it as paid.
  Only then mark the payment successful. Same-key notification retries recover
  the original acknowledgment, not an unrelated AlreadyPaid rejection.
- Definitive NOT OK after a successful charge starts a refund for the full
  charged total (USD 102 in the example). Only confirmed operator refund success
  marks it Refunded. Retry/reconcile refund with the original charge and refund
  key; failed or unknown refund remains pending or requires operational attention.
- Timeout, lost response, or transient company error is not definitive NOT OK.
  Reconcile/retry notification with the same key. Never refund solely because
  acknowledgment was lost or a retry limit elapsed; retain Processing and flag
  operational attention when automation cannot establish the outcome.
- Checking before charging cannot prevent an external channel paying immediately
  afterward. The company must detect a different payment for the same bill and
  reject this notification; Pagarte then refunds its charge. This design provides
  compensation, not a guarantee that a temporary duplicate debit cannot occur.
- No transfer or settlement with the company occurs in this workflow.

### QR-005: List Quotes (QUOTE-05)

Provide owner-only history with lifecycle, service, and created-date filters,
stable newest-first ordering with ID tie-breaker, and bounded pagination (default
20, maximum 100). Reject invalid bounds/ranges. Return immutable total/currency,
expiry, safe reference summary, and the owner's associated payment status/ID.
An empty list succeeds. Reads never initiate external calls or payment.

### QR-006: Cancel Before Submission (QUOTE-06)

- Only an unpaid, unsubmitted quote can be cancelled. Repeated cancellation is
  idempotent; retain history and consumed inquiry. Cancellation is not a refund.
- Pay and Cancel must compete for one atomic durable transition. If Cancel wins,
  no submission can be issued. If Pay wins, cancellation is blocked while any
  submission can still take effect, even if no local PaymentId is saved.
- Persist attempt ID, package fingerprint, and version before dispatch. Workers
  resume the same intent and cannot dispatch a cancelled/unclaimed intent. A
  stale lease or lookup returning not found does not release a dispatched attempt.
- Release a dispatched attempt only after Processor durably records a terminal
  non-acceptance for that attempt and rejects delayed replays. Otherwise continue
  recovery; never report cancellation while a delayed request could be accepted.

### QR-007: Consult Again (QUOTE-07)

After expiry, invalidation, cancellation, or a definitive unsuccessful payment,
the client explicitly consults again and creates a new quote from a new eligible
inquiry. The client can re-enter its data. There is no automatic refresh,
predecessor/replacement chain, or additional replacement-approval endpoint.
The normal Pay action authorizes the new summary. A new quote never bypasses an
unresolved payment, incomplete refund, or a successful payment for the same key.
After confirmed refund, a new attempt also requires fresh unpaid-bill evidence.

### QR-008: Recovery and Observable Payment Status

- Persist transitions before external effects; use separate stable charge,
  notification, and refund keys. Recover by ClientId + SourceQuoteId even without
  local PaymentId. Processor acceptance and bill-key reservation are atomic.
- Identical submissions return the same payment; changed package/card conflicts.
  Stale callbacks/responses cannot regress terminal state or repeat a side effect.
- Processor owns restart recovery. Persist attempts, versions, external references,
  retry counters/schedule, and safe errors. Never hold SQL locks during HTTP calls.

| Durable execution situation | Client payment status | Next permitted work |
| --- | --- | --- |
| Submission accepted; checking/charging; charge unknown | Processing | Continue/reconcile same payment. |
| Card charged; company notification pending/unknown | Processing | Notify/reconcile only; no second charge. |
| Card charged; matching company OK | Succeeded | Read history/receipt. |
| Definite failure without charge | Failed | New consultation for another attempt. |
| Company definitive NOT OK; refund pending/unknown | RefundPending | Refund/reconcile the original full charge. |
| Operator confirms full refund | Refunded | New consultation if bill remains unpaid. |

Operational attention is metadata on the unresolved payment, not false success
or failure. Preserve clear refund status because money was already charged.
Before execution exists, expiry, changed bill, unavailable verification, unsupported
integration, and cancellation blocked are ordinary operation errors, not separate
approval/freshness payment workflows. Public errors need stable codes and safe
messages; keep transport schemas in their API/gRPC sources during implementation.

## Acceptance Scenarios

1. Nested Composite sums each monetary leaf once; pricing/key Strategies select
   their respective rules deterministically. Test cycles, discounts, rounding,
   overflow, leading zeroes, key-field changes, and Strategy-version compatibility.
2. A USD 102 quote created at 10:00 expires at 10:05. At 10:04 it can be accepted
   after verification; at exactly 10:05 new acceptance fails. A payment accepted
   at 10:04 continues after 10:05. A 12-minute card registration requires requery.
3. Bill and stored price unchanged: one Pay causes one USD 102 charge, company
   acknowledgment for the USD 100 bill, then success with no extra approval.
4. Company NOT OK after charge: one full USD 102 refund. Pending/failed/unknown
   refund never appears refunded; restart and duplicate delivery do not refund twice.
5. Lost charge response: recover the same charge before notifying. Lost company
   response: recover its acknowledgment without charging again or guessing rejection.
6. Changed key item/date, bill amount, or currency requires a new consultation;
   same-value next-month bill is distinct. Pricing configuration changes alone
   do not rewrite an unexpired snapshot. Disabled route cannot be substituted.
7. External payment before verification prevents charging. External payment after
   verification produces company rejection after charge and full compensation.
8. Concurrent quotes/clients for one bill cannot create duplicate local payments.
   Successful and unresolved executions remain protected despite stale company balance.
9. Lost Processor response/local commit is recovered by source quote ID after
   expiry. Cancel/Pay races, delayed dispatch, stale workers, and restarts preserve
   a single winner; a not-found lookup never proves a delayed submission impossible.
10. Owner-only list/detail/cancel, atomic inquiry consumption, immutable historical
    reads, new inquiry after expiry, and no payment caused by card registration.

Use deterministic clocks and isolated mutable company fixtures. The mock must
support stable bill-key evidence, external payment, notification OK/NOT OK,
idempotent acknowledgment/status lookup, and lost responses. Operator fixtures
must support charge/refund failures and unknown outcomes. Exercise HTTP/gRPC,
SQL concurrency, restart recovery, and failure injection as well as unit tests.

## Delivery and Compatibility

1. Implement Composite/pricing/key Strategies and persist their evidence, exact
   five-minute expiry, list/cancel, and ordinary new-inquiry/new-quote behavior.
2. Extend provider-neutral bill verification and post-charge notification/status
   contracts with distinct service and total amounts. Validate mock capabilities;
   real adapters must demonstrate idempotency, lookup, acknowledgment/rejection,
   and refund recovery before enabling this flow.
3. Coordinate submission recovery, duplicate protection, and acceptance expiry
   across PayableServices and Processor. Implement charge -> notify -> complete
   or refund with durable transitions and exclusive compensation ownership.
4. Introduce an execution-flow version or drain old active executions before
   cutover. Never resume a company-first payment using the new order. Coordinate
   Worker refund messages/status updates so they cannot duplicate compensation.
5. Preserve old quote/inquiry snapshots. Unexecuted legacy quotes without the new
   identity/expiry contract become non-payable and require a new inquiry; do not
   bind them to today's bill or reopen expired records. Existing executions keep
   their original evidence/flow. Represent old flat components as a legacy flat
   snapshot (or presentation-only group), with unavailable Strategy evidence
   explicitly absent; never invent versions, rates, or bill identities.
6. Verify acceptance, update transaction completion with evidence, and only then
   change current-state architecture/flow descriptions to the implemented behavior.

## Out of Scope

Automatic refresh, linked replacements, additional approval screens, multi-service
carts, partial payments/refunds, currency conversion, recurring automatic payment,
company settlement/transfers, reconciliation processing, card-registration redesign,
and production mobile UI implementation. Full compensation refund for company
rejection is explicitly in scope.

## Related Documents

- [Accepted decision](../decisions/0005-resumable-quotes-and-payment-revalidation.md)
- [Target quote/payment flow](../flows/quote-review-and-payment.md)
- [Current quote implementation](../flows/quote-lifecycle.md)
- [Current payment lifecycle](../flows/payment-lifecycle.md)
- [Amount inquiry](mock-amount-inquiry.md)
- [Money rules](../decisions/0004-money-handling.md)
- [Transaction inventory](../transactions.md)
