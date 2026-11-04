# Requirement: Payment Methods

## Status and Scope

Complete the stored-card lifecycle: register, list, view, edit display metadata,
select a default, remove, and complete supported setup challenges. dLocal is the
first provider; shared use cases and mobile screens must remain provider-neutral.
The mobile app tokenizes directly with the provider. Pagarte receives a temporary
token and obtains a reusable saved-card reference; PAN/CVV must never enter Pagarte.

| Area | Status |
| --- | --- |
| Backend lifecycle | Implemented with a Development-only mock, including consent, recovery, removal, and audited admin review. |
| dLocal HTTP layer | Token-only vault requests, lookup/deletion, signing, and account routing have offline contract tests. |
| Public dLocal setup/mobile | Pending; registration stays disabled until the binding, recovery, and challenge contracts are confirmed. |

[Lifecycle flow](../flows/card-registration.md) describes current behavior;
[test guide](../../tests/PaymentSwitch.Processor.Tests/README.md) records evidence
and setup; [Current Problems](../current-problems.md#6-payment-method-acceptance-and-recovery-gaps)
tracks live-integration gaps. Transaction statuses remain in
[the inventory](../transactions.md#payment-methods).

## Ownership

| Owner | Responsibility |
| --- | --- |
| Payments.Api | Authenticated card/session/admin HTTP routes and provider callback transport. |
| Processor | Ownership, sessions, defaults, persistence, idempotency, recovery, audits, and payment eligibility. |
| ExternalConnections | Provider payloads, signatures, token verification, status, challenges, and optional removal. |
| Mobile | Trusted tokenization/challenge modules and consent presentation; application pending. |
| PayableServices | Quote ownership; passes only a Pagarte card ID. |

Controllers, gRPC services, consumers, and schedulers remain thin adapters.
Application use cases own decisions and commits.

## Functional Requirements

### `PM-001` Authentication and Ownership

- Require VerifiedClient and a resolved internal ClientId for client operations;
  reject missing profiles before provider calls. Protect internal card gRPC with
  workload authentication, verified at the host boundary.
- Return the same not-found response for foreign/unknown card and session IDs;
  reject malformed IDs safely. Removal uses the uniform no-op rule in PM-007.
- Authenticate provider callbacks independently of client JWTs. Bind events to
  the original integration/session/owner and deduplicate them durably.
- Require VerifiedAdmin and a distinct operations workload credential for admin
  reviews. Derive the audit actor from the authenticated identity.

### `PM-002` Registration and Tokenization (`CARD-01`, `CARD-07`)

- Persist an owned, expiring session before provider interaction. The server
  selects the integration; clients cannot choose endpoints or credentials.
- Require explicit current-policy consent. Record owner, policy version, and
  server time; include the version in creation idempotency. Stale versions conflict.
- Mobile uses dLocal's supported native tokenization integration, currently
  documented as a Direct SDK WebView bridge. Only public keys belong in mobile;
  server credentials remain separate by account/environment.
- Reject retired raw-card submissions and unknown fields. Exclude PAN/CVV from
  contracts, storage, logs, messages, caches, telemetry, and error responses.
- Distinguish the temporary token from reusable card_id. Direct documents a
  ten-minute/single-operation token lifetime. Retain a protected token only for
  its initial operation; never save it as a card reference or blindly resend it.
- Verify provider customer/session binding server-side; possession of an opaque
  token is insufficient. Confirm Direct-token standalone saving for the account
  without charging the customer to obtain a card reference.
- Provider evidence supplies reference, last four, brand/type, and expiry.
  Reject incomplete/expired evidence; client display metadata cannot override it.
  Activate/default a card only after durable verified success.
- Store only the provider reference/identity, safe metadata, owner, and lifecycle
  audit fields. Remove CardNumber storage while preserving populated databases'
  card/payment references; document migration preconditions and legacy eligibility.

### `PM-003` Idempotency and Recovery

- Require separate creation/completion keys scoped to client and operation.
  Identical inputs replay stored results; conflicting reuse returns conflict.
  Enforce concurrency and uniqueness in the database.
- Derive stable provider operation keys from persisted session IDs. Scope token
  digests/claims to provider/account/environment; another session or owner cannot
  claim the same token. Deduplicate verified saved references, never last four.
- Commit intent before provider calls and results before reporting success;
  release database transactions during network calls.
- Recover lost responses and failed result commits with confirmed provider
  lookup/idempotency capabilities. Never invent session lookup endpoints or
  create another registration blindly. Persist leases, schedules, and bounded
  attempts; unknown results require reconciliation rather than assumed decline.
- Replay terminal sessions as terminal. Definitive failure or unused-token
  expiry requires new tokenization and a new session/key.
- Expose unresolved work for audited admin review. Require expected state, a
  request key, and coded reason; persist intent/result and preserve automatic
  budgets. Accept no caller-supplied success or arbitrary provider references.

### `PM-004` Provider Challenge (`CARD-06`)

- Persist Pending, RequiresAction, Verifying, success, failure, cancellation,
  expiry, and unresolved outcomes with concurrency protection. Return owned
  status, safe supported actions, and expiry to mobile.
- Use the account's supported SDK/issuer UI and trusted return destinations.
  Verify completion through provider evidence; mobile success/navigation and
  tokenization alone do not establish issuer authentication.
- Duplicate, delayed, invalid, foreign, and out-of-order callbacks must not
  create duplicate cards or revive terminal sessions. Cancellation/expiry prevents
  late activation; reconcile and remove isolated provider resources when supported.
- Cover no challenge, success, decline, cancellation, expiry, timeout, and replay.
  Payment-time challenges require a separate execution design: RequiresAction
  must never mean readiness or charge success. CARD-06 remains Partial if the
  provider requires payment continuation that is not implemented.

### `PM-005` List and Detail (`CARD-02`, `CARD-03`)

- Return only owned, non-deleted cards and safe metadata with availability/expiry.
  Never expose provider references or temporary tokens.
- Provide bounded stable ordering: default first, creation time, then ID. Empty
  lists succeed; missing/foreign details return not found.
- Expired cards may remain visible but are unavailable; recheck eligibility at
  payment acceptance, not only registration.

### `PM-006` Metadata and Default Selection (`CARD-04`)

- Allow display metadata/default edits only. Cardholder-name edits do not change
  provider ownership or verified attributes.
- Enforce at most one active default with database uniqueness and an atomic
  transaction. Concurrent updates or failure must preserve a consistent default.
- Default selection is explicit; zero defaults is valid. Clearing/removing a
  default does not choose a replacement. Reject unavailable defaults; a default
  preference never bypasses payment eligibility.

### `PM-007` Remove a Card (`CARD-05`)

- Atomically soft-delete an owned card and clear its default. Unknown, foreign,
  and already removed IDs return the same successful no-op.
- Serialize removal against payment acceptance. Reject new use after removal;
  retain references needed by accepted execution, reconciliation, rollback, or refunds.
- Defer provider deletion while shared resources or accepted operations depend
  on it. Persist cleanup status/retries; require an explicit provider guarantee
  before detaching references needed for future payment-ID refunds.
- Local deletion succeeds independently of provider cleanup. Report pending,
  retained, unsupported, or unresolved cleanup accurately; local removal is not
  proof of provider erasure.

### `PM-008` Development Mock and Failure Handling

- Keep the synthetic payment operator separate from MockCompany.Api's catalogue
  and balance behavior. Permit it only in Development/tests, without real card data.
- Test invalid/foreign tokens, unsupported integrations, malformed evidence,
  unavailable providers, timeouts, and success before database failure/restart.
- Use meaningful HTTP statuses and the existing response wrapper. Correlate logs
  using operation IDs; exclude tokens, card/challenge secrets, and raw provider bodies.

### `PM-009` Replaceable Payment Operators

- Extend existing gateways/resolver patterns with provider-neutral setup,
  metadata, challenge, recovery, and removal contracts. Keep provider names,
  payloads, signing, paths, and errors inside integration adapters.
- Use one server-owned registry for selection, identity, capabilities, markets,
  currencies, and secret references. Validate required capabilities before collection;
  unsupported behavior must fail without raw-card fallback or simulated success.
- Snapshot provider/account/environment/version in sessions. Keep integration
  identities and credential references immutable while referenced. A default
  switch affects new registrations only; existing cards, callbacks, payments,
  refunds, and recovery retain their original route.
- Return safe mobile initialization only: integration ID, supported method/version,
  and public key where needed. Dispatch through trusted installed mobile modules;
  ordinary screens must not depend on provider SDKs or arbitrary executable URLs.
- References are not portable between providers. Retain old integrations until
  dependent work is resolved or require explicit re-registration/migration.
  Disabling new registration must preserve recovery, deletion, and refund routing.
- Switching compatible installed integrations requires configuration only.
  Adding a provider may require a backend adapter and mobile module/release;
  incompatible capabilities require an explicit design decision.
- Run the shared lifecycle contract against dLocal and a mock; separately test
  provider HTTP/signatures. Prove A-to-B default switching, retained A recovery,
  foreign-token/account rejection, unsupported module/version rejection, and
  independent registration disablement. Synthetic accounts alone do not prove
  complete provider replacement or live compatibility.

## Acceptance by Transaction

| ID | Required evidence |
| --- | --- |
| CARD-01 | One owned verified card; no duplicates across requests, callbacks, concurrency, or crash recovery. |
| CARD-02 | Safe stable pagination, empty list, removed exclusion, and explicit availability. |
| CARD-03 | Owned safe detail; uniform foreign/missing and malformed-ID handling. |
| CARD-04 | Allowed edits, atomic defaults, concurrency, invalid-default rejection, and rollback. |
| CARD-05 | Idempotent removal, acceptance ordering, and preserved execution/refund recovery. |
| CARD-06 | Verified challenge outcomes/replays; real timing and payment continuation when required. |
| CARD-07 | Direct mobile tokenization; no PAN/CVV in Pagarte traffic/storage/logs; expiry/reuse and live integration verified. |

Record mock, offline HTTP, and live-provider evidence separately in the test guide.

## Confirmations Required Before Enabling dLocal Registration

- Account/market/card support, Direct-token standalone saving, trusted payer and
  customer/session binding, recovery after a lost card reference, and removal/refund guarantees.
- Supported setup callback schema, signatures/correlation, and setup versus
  payment challenge timing. A payment signature alone cannot prove card setup.
- Mobile framework/module, public-key configuration, approved consent wording,
  and issuer UI. No mobile project exists yet.

## Out of Scope

Other payment-method families; quote/catalogue/pricing changes; a production
frontend; a payment-time saga redesign without its separate decision; raw-card
fallbacks; and compliance certification claims from mocks/tokenization alone.

## dLocal References

- [Native payment flow](https://docs.dlocal.com/docs/native-payment-dlocal-direct)
- [dLocal Direct](https://docs.dlocal.com/docs/using-dlocal-direct)
- [Saving cards](https://docs.dlocal.com/docs/saving-cards)
- [Create a card](https://docs.dlocal.com/reference/create-a-card)
