# Requirement: Complete Quotes and Payment Confirmation

## Status and Scope

| Field | Value |
| --- | --- |
| Status | Composite pricing is implemented for services with explicit pricing definitions. Single-use submission, bill reservation, and charge-first execution with full compensation are implemented for supported Development bills. Live-provider acceptance remains pending. |
| Transactions | QUOTE-01 through QUOTE-08; QUOTE-06 is out of scope; required payment-execution changes remain in scope |
| Primary owner | PayableServices |
| Integration owners | PaymentSwitch.Processor and ExternalConnections; coordinate refund compatibility with Worker |
| Patterns | Composite for quote items; Strategies for pricing and business-key selection |
| Quote lifetime | Five minutes from server-side creation while Unpaid; Submitted work continues independently of that deadline |
| Customer flow | Consult, review, Pay; consult again after expiry or invalidation |

This revision supersedes the earlier resumable-freshness and linked-replacement
proposal. There is no automatic quote refresh or additional approval workflow.
Tapping Pay authorizes the fixed displayed total. A valid, unchanged, unpaid
bill proceeds directly. On the first valid Pay request, the backend atomically
changes the quote from Unpaid to Submitted and permanently consumes it. The
customer no longer sees that quote as a payment summary and can never pay it
again, including after failure or refund. The [target flow](../flows/quote-review-and-payment.md)
charges the card first, then notifies the company: OK completes the payment;
definitive NOT OK requires a full card refund. Company notification records the
bill payment; it does not transfer money. Later reconciliation and settlement
with the company are outside this application.

## Implementation Status

| Development point | Current implementation |
| --- | --- |
| 1. Single-use submission | The five-minute deadline applies only while Unpaid. A timely Pay atomically binds the card, server claim time, submission ID, and immutable package. Submitted quotes leave the customer list and stay consumed. |
| 2. Bill identity and verification | Development inquiry quotes snapshot the company, service, account, and provider period. PayableServices verifies the unpaid bill, amount, and currency. Both quote paths check the original route before dispatch. Processor reserves an inquiry-backed bill key atomically across quotes and clients. |
| 3. Charge-first execution | Eligible Development payments pin charge-first mode. Processor charges the full quote total, completes on matching company acknowledgment, and owns the full compensation refund on definitive rejection. Previously accepted payments retain their recorded mode. |
| 4. Durable recovery | Owner-scoped lookup returns a payment or recorded rejection by source quote ID. Leased workers resume the original intent, and verified admins can schedule an audited same-key retry for a stalled charge-first operation. |
| 5. Composite and Strategy pricing | Explicit pricing definitions produce immutable grouped breakdowns and finalized monetary lines. Rule versions, rates, and bases are retained for audit. Existing flat quotes retain their stored amounts and presentation. |

The migrations preserve legacy evidence. The submission migration expires
unexecuted quotes from before the current claim lifecycle. The bill migration
invalidates unsubmitted inquiry-backed quotes without bill identity.
QUOTE-06 remains out of scope.
Live-provider acceptance is tracked in
[Current Problems](../current-problems.md#7-quote-acceptance-boundaries).

## Ownership

- PayableServices owns quote items, pricing and key Strategies, business-key
  evidence, inquiry binding, expiry, history, and submission intent.
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
  Quote creation checks the same display-ordered minor-unit sum as Processor.
- Snapshot leaves, grouping, rates/bases, Strategy IDs/versions, currency, route,
  and total once. Reads and payment submission do not reprice this snapshot.
- All client-relevant charges are visible; internal metadata and credentials
  remain private. USD is the initial company fixture currency; use shared money
  scale logic rather than assuming every currency has two decimals.
- Before submission and throughout execution, a configuration change with the
  same total but different fee/tax allocation does not rewrite the quote. Neither a
  new Strategy version nor new rounding configuration changes its amounts.
  A disabled/invalid route blocks payment; never silently substitute a route.

### QR-003: Retrieve and Expire (QUOTE-03, QUOTE-04)

- Set ExpiresAt = CreatedAt + five minutes using server UTC. The deadline applies
  only while the quote is unconsumed and Unpaid. At now >= ExpiresAt, that quote
  becomes Expired and cannot initiate Pay. Return that effective state consistently
  in detail, list, and submission responses.
- Inquiry evidence must be fresh at creation; its earlier expiry does not shorten
  or extend this fixed Unpaid window. Every new Pay requires a new bill check.
  This revises the inquiry cap in MAI-007/MAI-009 for future quote implementation.
- Expiry preserves the original snapshot/history but requires the client to
  consult again. Never extend the existing deadline or refresh it in place.
- Returning from card registration within five minutes permits normal Pay.
  Returning after five minutes without prior submission requires a new inquiry
  and quote. Registration,
  retrieval, and listing never charge or automatically consult the company.
- PayableServices checks server time while atomically claiming the first valid
  Pay as Unpaid -> Submitted. That claim must occur before ExpiresAt; client tap
  time alone cannot backdate a submission. Persist its authoritative SubmittedAt
  with the bound intent. This is the quote-expiry boundary, not later Processor
  acceptance.
- Once Submitted, original quote expiry cannot stop verification, dispatch,
  Processor acceptance, charging, confirmation, refund, or recovery of that one
  submission. Do not mark it Expired when its original deadline passes. Verify
  the existing intent before applying expiry to a technical request redelivery.
- Processor must validate that the server-owned submission was claimed before
  its quote deadline and matches the owner, snapshot, and selected card. It must
  not reject that valid handoff because its current clock is past ExpiresAt.
  SubmittedAt is server evidence, not an amount or timestamp supplied by the client.
- Fresh bill evidence, route/card eligibility, operation timeouts, retry policies,
  and outcome reconciliation still apply after submission. If delayed dispatch
  or recovery makes verification evidence stale, obtain fresh evidence for the
  same intent before charging and compare it to the approved bill/snapshot;
  never reprice, reopen the quote, or create a second customer attempt.
- Only an unconsumed Unpaid quote before its deadline can initiate Pay. A valid
  owned Pay request must atomically persist Unpaid -> Submitted, consumption
  time, selected card, and one submission intent before company verification
  or Processor dispatch. The customer removes the summary immediately on Pay;
  server state enforces the same restriction independently of the app.
- Consumption is permanent. Submitted, Processing, Paid, failed, invalidated,
  no-longer-due, and refunded outcomes never return that quote to Unpaid.
  Unsubmitted Expired quotes also never reopen.
  Keep the financial snapshot and status for backend audit/recovery, without
  redisplaying the consumed summary as a customer quote to pay.

The target quote statuses are distinct from payment-execution status:

| Quote status | Meaning |
| --- | --- |
| Unpaid | Not consumed; can initiate Pay only before expiry and with valid prerequisites. |
| Submitted | Permanently consumed by Pay; verification or handoff is in progress. |
| Processing | Processor accepted the associated payment; execution or outcome recovery continues. |
| Paid | The card charge and matching company acknowledgment succeeded. |
| PaymentFailed | The submitted attempt definitively failed without a charge; another attempt requires a new quote. |
| Invalidated | Verification found changed bill identity, amount, or currency; require a new quote. |
| NoLongerDue | The company confirms already paid/nothing due; no Pagarte success or receipt is invented. |
| Expired | The original deadline elapsed while the quote was still unconsumed and Unpaid; no Pay is allowed. Submitted work never becomes Expired merely because time passes. |
| RefundPending | The card was charged, the company definitively rejected it, and full refund is unresolved. |
| Refunded | The operator confirmed the full refund; the consumed quote remains unusable. |

Invalidated and NoLongerDue are implemented for inquiry-backed verification.
RefundPending and Refunded are available for Development charge-first payments.
Legacy Cancelled history remains non-payable. An uncertain dispatched
submission stays unresolved until safely reconciled, rather than being declared
Expired or reusable from the local clock alone. Freshness/operation policy can
fail a submitted attempt safely, but original quote expiry cannot. Failure before
Processor creates a payment can close the consumed quote without inventing a
PaymentId.

### QR-004: Verify, Charge, Notify, Confirm or Refund

- Tapping Pay is the customer's authorization for the displayed immutable total;
  no separate approval is required when checks succeed.
- Atomically consume the quote and reserve its only submission intent. Recover
  that same intent or its existing/unresolved payment on technical request
  redelivery; never start a second attempt from this quote. For new execution,
  obtain fresh company evidence with a new inquiry
  operation key, not a replay of the creation inquiry. Store verification evidence
  separately without consuming another inquiry into the same quote.
- Verify the same Strategy-selected business key, unpaid bill, service amount,
  currency, and eligible original route. Do not recalculate fees/taxes at Pay.

| Verification result | Behavior before charging |
| --- | --- |
| Same unpaid bill, service amount and currency; valid quote/route/card | Continue the same Pay action with the stored total. |
| Any selected key value, service amount, or currency changed | Invalidate the old quote; require a new consultation and quote. |
| Already paid / nothing due | Do not charge; explain the outcome without inventing a Pagarte payment. |
| Unavailable / malformed evidence | Return a safe verification error and close the consumed attempt as failed if no execution can take effect. Any new customer attempt requires a new consultation/quote; never restore Unpaid. |
| Quote expired before the Unpaid -> Submitted claim, or route is invalid | Do not charge; require a new consultation when the service is available. Passing the original quote deadline after a valid claim is not a verification failure. |

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

Provide an owner-only customer quote list with lifecycle, service, and
created-date filters, stable newest-first ordering with ID tie-breaker, and bounded pagination (default
20, maximum 100). Reject invalid bounds/ranges. Return immutable total/currency,
expiry, safe reference summary, and the owner's associated payment status/ID.
An empty list succeeds. Exclude permanently consumed quotes from this customer
list and do not redisplay their payment summaries, regardless of later success,
failure, expiry, or refund. Retain the backend records and payment association
for recovery/audit. Reads never initiate external calls or payment. Existing
listing now applies this exclusion; owned detail/status retains the stored
snapshot for read-only recovery/audit.

### QR-006: Quote Cancellation Out of Scope (QUOTE-06)

Explicit quote cancellation was removed from scope on 2026-11-05. The customer
may leave the payment summary without a backend mutation; the quote retains its
original five-minute deadline. If never submitted, it can still be paid before
expiry if its other checks pass. No cancellation endpoint or Pay/Cancel race
workflow is required. Preserve this ID and existing Cancelled history/enum
values for compatibility; do not renumber requirements
or rewrite historical records.

Leaving the summary after Pay does not stop an accepted or uncertain submission.
Submission ownership, fencing, idempotency, and recovery remain required by
QR-008. Payment-execution cancellation is a separate PAY-05 product decision.

### QR-007: Consult Again (QUOTE-07)

After expiry, invalidation, or a definitive unsuccessful payment,
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
- Technical redelivery resumes or returns the original intent/result, including
  while no PaymentId exists. It never permits another customer Pay action or
  changes the quote back to Unpaid. Changed package/card conflicts.
  Stale callbacks/responses cannot regress terminal state or repeat a side effect.
- PayableServices owns restart recovery for durable submission intents; Processor
  owns recovery of accepted payment executions. Persist attempts, versions, external references,
  retry counters/schedule, and safe errors. Never hold SQL locks during HTTP calls.
- Persist attempt ID, package fingerprint, and version before dispatch. Only the
  current durably claimed intent can dispatch; workers resume that same intent.
  A stale lease or lookup returning not found does not release a dispatched attempt.
- Close a dispatched attempt as non-accepted only after Processor durably
  records terminal non-acceptance for that attempt and rejects delayed replays.
  Otherwise continue recovery. Closing it can permit a fresh inquiry/new quote
  once bill-level barriers allow; the original quote is permanently consumed.
  Leaving the summary does not revoke submission.

| Durable execution situation | Current quote execution API status | Next permitted work |
| --- | --- | --- |
| Submission accepted; checking/charging; charge unknown | Processing | Continue/reconcile same payment. |
| Card charged; company notification pending/unknown | Processing | Notify/reconcile only; no second charge. |
| Card charged; matching company OK | Completed | Read the owned quote execution result. |
| Definite failure without charge | Declined for a failed payment, or Failed for a recorded rejection | New consultation for another attempt. |
| Company definitive NOT OK; refund pending/unknown | RefundPending | Refund/reconcile the original full charge. |
| Operator confirms full refund | Refunded | New consultation if bill remains unpaid. |

Operational attention is metadata on the unresolved payment, not false success
or failure. Preserve clear refund status because money was already charged.
Expiry before submission, changed bill, unavailable verification, unsupported
integration, and unresolved submission return stable operation errors with the
stored quote status. They do not create extra approval/freshness workflows or
make a consumed quote reusable. Public errors need safe messages; keep transport
schemas in their API/gRPC sources during implementation.

## Acceptance Scenarios

1. Nested Composite sums each monetary leaf once; pricing/key Strategies select
   their respective rules deterministically. Test cycles, discounts, rounding,
   overflow, leading zeroes, key-field changes, and Strategy-version compatibility.
2. A USD 102 Unpaid quote created at 10:00 expires at 10:05. A server claim at
   10:04:59 atomically persists Submitted and its intent. Verification and Processor
   acceptance at 10:05:02 may continue under the same approved snapshot and fresh
   bill checks; the quote never becomes Expired. At exactly 10:05, an unclaimed
   Unpaid quote rejects Pay without starting external work. Technical redelivery
   of the earlier valid claim resumes its original intent after the deadline.
   A 12-minute card registration without prior submission requires a new inquiry.
   An expiry update racing with Pay must produce one atomic winner: Expired or
   Submitted. An expiry worker must never overwrite a successful Submitted claim.
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
   expiry. Concurrent Pay requests, delayed dispatch, stale workers, and restarts
   preserve a single submission; a not-found lookup never proves a delayed
   submission impossible. Leaving the summary does not revoke submitted work.
10. Owner-only list/detail, atomic inquiry consumption, immutable historical
    reads, new inquiry after expiry, and no payment caused by card registration
    or by leaving the summary. An abandoned quote keeps its original deadline.
11. The first valid Pay atomically persists Submitted plus consumption and intent
    before external work, and removes that summary from the customer quote list.
    Success, verification/card failure, passing the original quote deadline,
    lost response, and refund never restore it to Unpaid. Direct requests and technical redelivery
    cannot start another attempt or select a different card for that consumed
    quote. A permitted later customer attempt always uses a new inquiry/quote.
    Crossing the original deadline during verification or handoff cannot expire
    or cancel a valid Submitted intent; independent operation failures still use
    their normal failure/recovery rules.

Use deterministic clocks and isolated mutable company fixtures. The mock must
support stable bill-key evidence, external payment, notification OK/NOT OK,
idempotent acknowledgment/status lookup, and lost responses. Operator fixtures
must support charge/refund failures and unknown outcomes. Exercise HTTP/gRPC,
SQL concurrency, restart recovery, and failure injection as well as unit tests.

## Delivery and Compatibility

- Deploy Processor's submission RPCs and associated migrations before the
  PayableServices cutover. For configured-price route rejection, deploy
  `RejectUnavailableQuoteRoute` before PayableServices applies
  `PersistUnavailableQuoteRoute` and uses the new RPC.
- Legacy unsubmitted quotes without the required claim or bill evidence require
  a new quote and, for inquiry-backed services, a new inquiry. Existing
  quote and inquiry snapshots are preserved. Historical flat snapshots retain
  their original amounts, with unavailable Strategy evidence absent.
- Existing payments retain their original evidence and pinned execution mode.
  Worker guards its legacy refund path by execution mode, and Processor owns
  charge-first compensation.
- Live adapters must demonstrate authoritative bill verification, idempotency,
  lookup, recoverable acknowledgment or rejection, and full refund recovery
  before charge-first execution is enabled for their routes.

See [Quote Lifecycle](../flows/quote-lifecycle.md#deployment-and-compatibility)
and [Payment Lifecycle](../flows/payment-lifecycle.md#deployment-rule).

## Out of Scope

Explicit quote cancellation (QUOTE-06), automatic refresh, linked replacements,
additional approval screens, multi-service carts, partial payments/refunds,
currency conversion, recurring automatic payment,
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
