# Card Registration and Removal

## Implementation Status

The backend lifecycle and admin review work with a Development-only synthetic
operator. dLocal vault HTTP/signature/account routing has offline tests; public
dLocal setup remains disabled pending confirmed binding/recovery/challenge contracts.
Mobile is pending. See the [requirement](../requirements/payment-methods.md),
[test guide](../../tests/PaymentSwitch.Processor.Tests/README.md), and
[remaining gaps](../current-problems.md#6-payment-method-acceptance-and-recovery-gaps).

## Runtime Flow

```text
Verified client -> Payments.Api -> workload-authenticated CreditCardService
  -> Start: validate policy consent/capabilities; persist owned Pending session
  -> Mobile: tokenize through a trusted installed provider module
  -> Complete: claim completion key/token digest; protect token; schedule work
  -> Continue: claim lease; consume protected token; commit Verifying intent
      -> release transaction; save once or look up the existing provider operation
      -> owner lock; persist verified card or pending/terminal outcome
  -> Dispatcher: resume due recovery after interruption/uncertainty
```

| Limit | Current value |
| --- | --- |
| Session | 15 minutes |
| Submission eligibility | Ten minutes from backend submission, capped by session expiry |
| Lease / retry / scan | Two minutes / ten seconds / five seconds |
| Automatic setup and cleanup budgets | Five claimed rounds each; checked before another call |

The local token deadline does not prove provider issuance time; provider expiry
validation remains required. Data Protection encrypts the temporary token until
its first attempt. Continuation clears it before calling the provider; recovery
uses the same stable operation key and never blindly resends a consumed token.
NotFound can require new tokenization; ambiguity remains Verifying, then Unresolved
at the cap. Expired ciphertext is cleared when recovery next observes it.

Creation/completion keys are independent, case-sensitive, and client-scoped.
Changed inputs conflict. Provider/account/environment-scoped token claims prevent
cross-session/owner reuse. Only provider-verified metadata creates an available card.

Consent records authenticated owner, policy version, and server time. Stale policy
versions conflict; an identical creation request can replay after a policy change.
Product policy wording and its mobile presentation remain pending.

## Integration Routing

Sessions snapshot integration/account/environment/version. Processor pins configured
identities and credential references in PaymentOperatorRoutes; Worker loads them
read-only before refunds. Factory calls reject repointed routes, including after
configuration mutation. A default switch affects new registrations only.

Use a new integration ID for identity/reference changes; rotate credential values
under the retained reference. Keep old routes for dependent work. Legacy raw Mock/
DLocal routes retain compatibility routing. Configured dLocal clients require
matching account/environment credentials and cannot borrow global credentials.

## Challenges and Notifications

The mock's PresentChallenge action simulates success, decline, cancellation, or
waiting through provider lookup. Client continuation carries no success flag.
Cancellation/expiry prevents activation even after a late provider success.

The anonymous callback endpoint forwards raw bytes and allowlisted authentication
headers over protected gRPC. Limits are 8192 body bytes and ten headers/4096 characters.
The adapter authenticates the event; Processor binds its integration/session and
persists a unique integration/event inbox. Duplicates succeed without rescheduling;
unknown/mismatched sessions are no-ops and terminal sessions ignore late events.
Callbacks wake authoritative lookup, never establish success themselves.

The mock uses purpose-scoped HMAC with a five-minute timestamp tolerance. dLocal's
raw-body HMAC helper has independent tests, but setup-event schema/ownership remains
unconfirmed; dLocal setup callbacks are rejected. Deprecated gRPC timestamp/signature
fields are no longer consumed; deploy HTTP and Processor together. Exact callback
headers/signing fixtures are in the test guide.

Tokenization does not prove issuer authentication. Real setup UI/timing and any
payment-time continuation remain separate acceptance work.

## Stored Cards and Payment Ordering

List/detail expose safe display metadata, availability, and expiry only. Listing
is capped at 100, ordered by default, descending creation time, then ID. Display
name is editable; provider reference, brand, last four, and expiry are verified fields.

A shared owner SQL application lock orders registration, defaults, removal, and
payment acceptance. Filtered uniqueness protects one active default and saved-reference
deduplication. Clearing the previous default and setting another share one transaction;
failure rolls back both. Zero defaults is valid; removal selects no replacement.

Removal soft-deletes and clears the default immediately. Foreign, unknown, and
already deleted IDs are successful no-ops. Removal first rejects new acceptance;
acceptance first preserves the reference for execution/recovery/refunds. New acceptance
also checks setup verification, expiry, and declared market/currency support.
The existing company-first saga keeps its transitions.

Provider cleanup is separate and visible through the owned removal-status route.
It defers while shared references, pending/unknown registrations, or accepted work
need the resource. Any payment history blocks detachment unless the adapter guarantees
payment-ID refunds survive it; unresolved/nonterminal payment work still blocks it.
Deferred dependencies are checked after one minute. Uncertain submitted removal
blocks new registration to prevent a late delete affecting re-registration.

Calls use durable leases and a stable removal key outside SQL transactions.
Interrupted result commits cannot bypass the five-attempt automatic cap. Cleanup
states distinguish Pending, Deferred, Removed, NoResourceFound, RetainedByProvider,
Unsupported, and RequiresReview as applicable. Terminated setups never activate;
only isolated setup resources may be deleted. Retention/local deletion is not erasure.

The mock supports optional idempotent tombstones and rejects detached references
for readiness/charge. AppHost enables removal and its synthetic refund guarantee.
Real dLocal removal/refund guarantees still require acceptance.

## Audited Admin Review

VerifiedAdmin protects HTTP review routes. Payments derives issuer/subject as actor;
a separate CardOperationsAuth key protects CardOperationsService. General workload
credentials cannot review operations. Routes, reason codes, and configuration live
in the test guide.

ReviewCardOperationUseCase supports authoritative Reconcile and, for cleanup/removal,
RetryRemoval. Each request requires expected state, a key, and coded reason. Intent
is committed/leased before the call; audit/result commit together afterward.
It reuses verified-card persistence and dependency/expiry checks. Clients cannot
supply success, actor, or arbitrary provider references; automatic counters stay intact.

Identical replays return the audit without another call. A lost result commit leaves
Pending. After lease expiry, a new explicit review may retry/recheck, marking the prior
pending audit ResultUnknown. Unknown results retain review barriers; RetainedByProvider
acknowledges retention. Expired/cancelled registrations cannot activate through review.

## dLocal HTTP Preparation

DLocalCardVaultClient prepares token-only save, known-card lookup, and deletion.
A trusted backend caller must supply payer identity; no public payer DTO/projection
or verified token binding is implemented. DLocalPaymentOperatorAdapter therefore
does not implement ITokenizedCardSetupAdapter and registration fails before collection.
A lost reference cannot be recovered through an invented session endpoint.

The transport signs exact UTF-8 bytes with separate login/transaction/signing keys,
uses fixed Sandbox/Production HTTPS hosts for configured accounts, and disables
redirects, automatic retries, and HTTP logging. Full responses have a 30-second timeout
and 32 KiB cap. Metadata must be complete, unexpired, supported, active, and reference-matched.
Deletion requires confirmed absence unless its response is 204/404. Payment/refund
completion requires PAID/SUCCESS; charge lookups reject another payment's reference.

Offline HTTP tests do not prove Direct-token vault compatibility, customer binding,
issuer authentication, live recovery, or refund/removal guarantees.

## Persistence and Migration

| Migration | Effect |
| --- | --- |
| TokenizedCardRegistration | Removes CardNumber; adds sessions, verified metadata/identity, digests, and uniqueness. |
| CardSetupCallbackInbox | Adds durable event deduplication. |
| TrackTerminatedCardSetupResources | Adds terminal-resource cleanup state. |
| RecordCardConsentAndDeferredRemoval | Adds historical consent fields, adapter identity, and deferred removal. |
| PinPaymentOperatorRoutes | Adds retained integration identities. |
| AuditCardReviewsAndPinCredentialReferences | Adds review audits and credential-reference pins. |

Populated databases with oversized owner IDs or duplicate active defaults require
remediation; migrations do not choose winners or delete card/payment history.
Legacy references/display data survive, but cards start unverified/unavailable for
new payments until re-registration. Existing executions retain their references.
An old default may remain visible/unavailable until changed or removed.

Successful setup evidence backfills adapter identity; historical consent is not
fabricated. Existing route pins keep an empty credential reference, and previously
removed rows are not automatically deleted at the provider.

Dropping CardNumber is irreversible as a data operation: downgrade recreates an empty
column, not its values. Historical migrations remain unchanged. Tests cover fresh and
populated databases; development migrated only isolated acceptance-test databases.

## Mobile Integration Contract

Mobile reads the policy, obtains consent, then initializes a trusted installed
module using the session's integration/method/version/public key. Unsupported modules
fail before collection. The module tokenizes directly, submits only token/session/key,
presents supported actions, and resumes through backend status/continuation.

The mock returns SyntheticToken version 1. No mobile project or live dLocal module/
public-key wiring exists. Ordinary screens must use one provider-neutral interface;
provider URLs, HTML, and issuer secrets must not enter custom Pagarte forms.
