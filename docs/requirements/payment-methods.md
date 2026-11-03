# Requirement: Payment Methods

## Status and Scope

| Field | Value |
| --- | --- |
| Status | Draft for review; proposed behavior is not implemented |
| Accepted integration choice | dLocal mobile tokenization; Pagarte owns backend payment operations |
| Architectural requirement | Replaceable operators behind stable backend and mobile integration interfaces |
| Transactions | `CARD-01` through `CARD-07` in [transactions](../transactions.md) |
| Owners | `Payments.Api`, `PaymentSwitch.Processor`, `ExternalConnections.PaymentOperators` |
| Initial verification | Deterministic mock payment operator and backend integration tests |
| Linked work | Internal workload authentication and payment execution compatibility |

Complete the stored-card lifecycle: add, list, view, update/default, remove,
complete a provider challenge, and tokenize card details directly with dLocal
from the mobile app. Pagarte receives a temporary token and uses backend APIs
to obtain a reusable saved-card reference and verified metadata. PAN/CVV shall
not pass through Pagarte services. A mock payment operator may substitute for dLocal while
the actual application workflows and persistence are implemented. It is distinct
from MockCompany.Api, which supplies service catalogues and customer balances.

The operator and mobile-tokenization approach are selected. This draft does not
claim a live integration or mark incomplete transactions complete.

The requester accepted dLocal mobile tokenization and the other lifecycle
behaviors. The mobile app communicates with dLocal for card collection and
tokenization, while Pagarte controls ownership, saved cards, quotes, charges,
refunds, and reconciliation. CARD-07 requires keeping PAN/CVV out of Pagarte.

dLocal is the first operator, not a dependency of the business model. Selecting
another already-integrated operator shall require a central configuration
change. Adding a new operator shall be confined to the operator integration
area and its registration/configuration, including its mobile tokenization
implementation; shared card and payment use cases shall remain unchanged.

## Current Implementation and Gaps

- The public card controller requires `VerifiedClient`; identity resolution
  supplies the internal client ID. Processor owns card records in PaymentDb.
- List, detail, update, and soft-delete operations exist. Detail/update check
  ownership; lists and repository lookups exclude deleted cards.
- Registration accepts PAN (full card number) and CVV, passes them through
  HTTP/gRPC to the operator, then persists `request.CardNumber` in the card
  entity. The existing inventory's description of safe metadata alone is
  inaccurate. Public responses do not return this field.
- Registration has no durable registration session or idempotency contract.
- Default selection clears existing defaults before saving, but the card
  mapping has no uniqueness constraint enforcing one active default per client.
- Deletion is local soft deletion. The current workflow does not describe
  operator detachment or coordination with an ongoing payment.
- No provider challenge/continuation workflow is modeled. Both the raw-card
  backend input and full card-number persistence must change for this design.
- Existing `IPaymentOperatorAdapter`, registration gateway, and operator
  resolver provide a starting abstraction. Their presence does not establish
  replacement support for the proposed token/session/challenge lifecycle.

Evidence: [registration use case](../../src/PaymentSwitch/PaymentSwitch.Processor/Application/UseCases/RegisterCreditCardUseCase.cs),
[card entity](../../src/PaymentSwitch/PaymentSwitch.Processor/Domain/Entities/CreditCard.cs),
[card repository](../../src/PaymentSwitch/PaymentSwitch.Processor/Infrastructure/Repository/CreditCardRepository.cs),
[card controller](../../src/Payments/Payments.Api/Controllers/CreditCardController.cs),
and [default update](../../src/PaymentSwitch/PaymentSwitch.Processor/Application/UseCases/UpdateCreditCardUseCase.cs).

## Proposed Flow

```text
Verified mobile client -> request card-registration session from Payments.Api
  -> Processor persists owned session for configured dLocal integration
  -> mobile tokenizes card fields directly with dLocal
  -> mobile sends temporary token + Pagarte session ID to Payments.Api
  -> Processor uses ExternalConnections to save/verify the card with dLocal
  -> dLocal returns reusable card_id and verified metadata
      -> challenge required: return safe next action and await completion
      -> pending/unknown: reconcile provider status
      -> declined/cancelled/expired: finish without a usable card
      -> verified success: persist card_id + safe metadata as one usable card
  -> client lists/selects/updates/removes stored card by Pagarte card ID
```

The card-entry experience remains in the mobile app. Pagarte never treats a
client-supplied success flag as proof of completion. Tokenization or card saving
does not prove issuer authentication or guarantee a future payment will succeed
without a challenge.

## Ownership

- Payments.Api owns authenticated public session/card endpoints and safe HTTP
  responses. It derives client identity; it does not store cards or tokens.
- Processor owns sessions, ownership, defaults, lifecycle state, idempotency,
  recovery, card persistence, and eligibility for new payments. Use cases own
  commits and decisions; transports remain thin.
- ExternalConnections adapts provider session, token-verification, challenge,
  status, and optional detachment contracts into stable application outcomes.
- The mobile app integrates dLocal tokenization and supported challenge
  presentation. dLocal owns tokenization and authoritative provider results;
  Pagarte performs saved-card and payment operations through backend APIs.
  The mock uses synthetic fixtures only.
- PayableServices continues to confirm a quote with a Pagarte card ID. It must
  not receive operator tokens or raw card fields.

## Functional Requirements

### `PM-001` Authentication and Ownership

- All client card/session operations require `VerifiedClient` and the resolved
  internal ClientId. Reject a missing profile before calling the operator.
- Protect the internal card gRPC boundary with the existing workload pattern.
  The card service class has no authorization attribute; verify any host-level
  enforcement and close missing protection before accepting this work.
- Foreign and unknown card/session IDs return the same not-found response.
  Validate malformed identifiers without an unhandled exception.
- Provider callbacks use provider authentication/signature verification and
  replay protection; they do not rely on a client bearer token. Bind callbacks
  to the stored operator, session, and owner.

### `PM-002` Registration and Tokenization (`CARD-01`, `CARD-07`)

- Create a durable owned registration session before provider interaction.
  The server selects the operator; clients cannot provide arbitrary endpoints
  or credentials. Store correlation IDs and a bounded session expiry.
- The mobile app shall use dLocal's documented native tokenization flow. Its
  current native guide describes a lightweight WebView bridge to dLocal Direct
  for Android, iOS, and React Native. Only the token and allowed non-sensitive
  context shall be sent to Pagarte; PAN/CVV must not enter its HTTP/gRPC flow.
- Only dLocal's client/public key belongs in the app. Keep signing credentials
  and server API secrets in the backend, with separate sandbox/production
  configuration. Do not log tokens or card fields in mobile or backend tooling.
- Remove raw PAN/CVV fields from Pagarte registration contracts and reject
  legacy raw-card submissions without forwarding them. Exclude card fields
  from persistence, logs, queues, outbox, caches, telemetry, and error responses.
- Distinguish the short-lived token from reusable dLocal card_id. dLocal Direct
  documents token expiry after ten minutes or one operation. Never persist that
  temporary token as the saved payment method or replay it as a fresh operation.
  Confirm the token-based save-card request for the enabled account/market in
  sandbox; do not copy raw-card examples or silently charge to obtain card_id.
- Verify the token/session server-side with the selected operator, including
  its association with the intended setup session and provider customer.
  A freely supplied opaque token alone is not sufficient proof of ownership.
- Persist only operator token/reference, operator identity, last four digits,
  brand/type, expiry, display metadata, owner, and lifecycle/audit fields.
  Treat tokens as secrets: exclude them from public responses and logs.
- Provider evidence supplies last four, brand, and expiry. Client values must
  not override verified attributes. Reject incomplete or expired card evidence.
- Remove CardNumber persistence and the raw-card registration path. Inspect
  references, tests, and migrations first. No production
  data exists according to the requester; do not assume every local database
  is empty or silently delete unrelated data. Document the migration outcome.
- Do not activate a card or make it default until setup succeeds durably.

### `PM-003` Idempotency and Recovery

- Require an idempotency key for session creation/completion. Scope keys to
  client and operation; store a fingerprint of allowed non-secret inputs.
- Identical repeats return the stored session/card; conflicting reuse returns
  conflict. Database constraints and concurrency control prevent duplicate
  sessions/cards from concurrent requests and repeated callbacks.
- Assign stable provider operation keys from persisted session IDs. Different
  clients using the same client key must not collide at the provider.
- Persist intent before external calls and results before reporting success.
  Do not hold a database transaction across a provider HTTP call.
- On provider success followed by database failure, recover using the same
  session/reference/key. Do not create another operator registration blindly.
- Bind the submitted token to one owned session, using a protected token digest
  for conflict/replay detection without logging it. A token must not be claimed
  by another client/session. Do not retain the temporary token beyond the
  minimal provider-operation need; define its protected handling explicitly.
- Recovery shall use confirmed dLocal idempotency/status/reference capabilities,
  without PAN/CVV or blind reuse of a consumed token. Prove recovery from a
  successful save followed by a lost response or failed database commit in
  sandbox; an invented registration lookup endpoint is not an implementation.
  Unresolvable outcomes require a visible unresolved state and operational
  handling, not automatic creation with a new token. After a definitive failure
  or unused-token expiry, request new mobile tokenization and a new session.
- Unknown/time-out results remain pending and require status reconciliation;
  they are not proof of decline. Bound retry attempts and persist the next
  attempt/lease so recovery survives a process restart.
- A terminal failed/cancelled/expired session is replayed as such; retrying
  setup uses a new session/key. Define provider-token deduplication by client,
  operator, and verified provider reference, never by last four digits alone.

### `PM-004` Provider Challenge (`CARD-06`)

- Model registration states such as Pending, RequiresAction, Verifying,
  Succeeded, Failed, Cancelled, and Expired. Persist transitions with concurrency
  control. A timeout during verification remains unresolved until reconciled.
- Expose owned status retrieval and continuation operations with safe next
  actions and expiry for mobile presentation. Use the dLocal-supported challenge
  mechanism for the account/market, which may involve provider/issuer UI or an
  SDK. Verify completion through backend status/callbacks, not mobile flags.
- Mobile tokenization does not itself implement 3-D Secure. Confirm challenge
  timing, SDK/redirect requirements, and trusted return destinations separately;
  do not route issuer authentication secrets through custom Pagarte forms.
- Verify challenge success server-side before storing a usable card. Duplicate,
  delayed, invalid, foreign-session, and out-of-order callbacks must not create
  cards or move terminal sessions back into an active state.
- Cancellation or expiry prevents later activation. Reconcile any provider
  resource created after local expiry and detach it where supported.
- Initial mock coverage shall exercise no challenge, successful challenge,
  decline, cancellation, expiry, timeout, and repeated callback cases.
- This specifies card-setup challenges. Payment-time challenges require a
  separate execution design before real-provider acceptance: the current
  company-first saga must not interpret RequiresAction as card readiness or
  charge success. `CARD-06` remains Partial if only setup challenges are done
  and the chosen provider also requires payment-time continuation.

### `PM-005` List and Detail (`CARD-02`, `CARD-03`)

- Return only the owner's non-deleted cards, using safe display fields and
  explicit availability/expiry information. Never return tokens, PAN, or CVV.
- Use stable ordering (default first, creation time, ID) and bounded pagination.
  Empty collections succeed; missing/foreign IDs return not found.
- Expired cards may remain visible as unavailable but cannot be selected for
  new payment execution. Check expiry at execution time, not only registration.

### `PM-006` Metadata and Default Selection (`CARD-04`)

- Allow only documented display metadata and default selection. The existing
  editable cardholder-name field is display metadata; changing it must not
  change provider ownership or verified card attributes.
- At most one non-deleted default card per client shall exist, enforced by
  database uniqueness plus an atomic application transaction. Concurrent
  default changes must not leave two defaults or a partially applied update.
- Proposed default: a new card becomes default only when explicitly requested.
  Zero defaults is valid. Setting default false or deleting the default does
  not silently select a replacement.
- Reject default selection for expired, pending, deleted, or unavailable cards.
  A default is a UI preference and never bypasses payment eligibility checks.

### `PM-007` Remove a Card (`CARD-05`)

- Soft-delete an owned card and clear its default flag atomically; repeated
  deletion, including unknown/foreign IDs, returns the same successful no-op
  response to avoid exposing another owner's card.
- Reject new payment acceptance for deleted cards. Serialize deletion against
  payment acceptance so their ordering is deterministic.
- Preserve historical payment references and the evidence required for an
  already accepted payment, reconciliation, rollback, or refund. Removal shall
  not cancel an in-flight payment or destroy its ability to recover.
- If operator detachment is supported, defer it while accepted operations
  depend on the token and track retries durably. Local deletion succeeds even
  if external cleanup is pending; do not advertise completed provider removal.
- Document unsupported detachment explicitly. Never promise local deletion
  erases the provider's stored instrument.

### `PM-008` Development Mock and Failure Handling

- Use a dedicated mock payment-operator contract; do not put stored-card
  behavior into the company catalogue/balance mock.
- Provide synthetic setup/token fixtures and challenge simulation without
  entering real PAN/CVV. Disable mock configuration outside Development/tests.
- Exercise invalid/foreign tokens, unsupported operator, unavailable provider,
  malformed metadata, timeout, and success-before-database-failure cases.
- Map validation/not-found/conflict/provider-pending/failure outcomes into
  documented HTTP responses and the existing public response wrapper. Avoid
  reporting every failure as HTTP success in the revised contract.
- Logs correlate session/card/operation IDs without card fields, tokens,
  challenge secrets, or raw provider request/response bodies.

### `PM-009` Replaceable Payment Operators

- Application-owned interfaces shall describe token-based registration,
  saved-card metadata, challenge actions, status/recovery, and removal using
  provider-neutral models. Extend existing gateways/resolver patterns instead
  of adding a parallel selection mechanism.
- Keep dLocal HTTP payloads, authentication/signing, endpoint paths, token
  lifetime rules, error codes, and callback parsing inside its integration
  adapter. Translate `card_id` to an opaque provider card reference internally.
  Public card IDs, use cases, controllers, and database lifecycle states must
  not depend on dLocal field names or branch on provider names.
- Use one server-owned operator registry/configuration for new registrations,
  containing adapter selection, provider-account identity, environment,
  capability configuration, and secret references. Do not distribute hardcoded
  default-provider choices among API, Processor, and mobile code.
- Registration sessions shall snapshot the selected provider, account,
  environment, and integration version. Return only safe mobile initialization
  information (provider identifier, supported method/version, public key when
  needed); never server secrets or arbitrary executable content/URLs.
- The mobile app shall expose one provider-neutral tokenization/challenge
  interface. dLocal's SDK/WebView belongs in its implementation. Ordinary app
  screens call that interface rather than dLocal directly. Dispatch only to
  trusted installed integrations compatible with the returned configuration.
  An unsupported provider/version must fail clearly before collecting data.
- Declare capabilities explicitly: tokenized saving, lookup/recovery,
  challenge mechanisms, supported markets/currencies, and deletion. Validate
  them before enabling an integration. Normalize unsupported behavior; do not
  emulate verification success or fall back to sending PAN/CVV to Pagarte.
- A configuration switch affects new registrations only. Existing sessions,
  saved cards, accepted payments, refunds, callbacks, and retries retain their
  original provider/account/environment. Use that identity for subsequent
  operations rather than the current default. Deduplication and provider
  idempotency keys shall include the relevant provider/account scope.
- Treat provider card references as non-portable. Never send a dLocal card_id
  to another operator. Keep the old integration available for existing cards
  and outstanding operations, or require explicit re-registration/migration.
  Do not rewrite references or claim transparent token migration.
- Separate disabling new registrations from disabling an integration entirely.
  Full retirement must account for stored cards, unresolved operations, and
  refunds; changing the default shall not make pending work unrecoverable.
- Scope of the promise: switching between compatible installed integrations
  requires configuration only. Supporting a new provider may require a new
  backend adapter and mobile module/release. An incompatible capability needs
  an explicit design decision; one-file replacement is not guaranteed.

### Operator Replacement Acceptance

- Run the same provider-neutral lifecycle contract tests against dLocal and a
  mock integration. Keep provider payload/signature tests separate. dLocal
  sandbox acceptance remains required; a mock does not prove live compatibility.
- Switch the configured default from operator A to B in a test environment:
  new sessions use B without edits to shared use cases or public contracts.
  Existing A sessions/cards and payment/refund recovery still use A.
- Reject a B token submitted to an A session and mismatched account/environment
  evidence. Test unsupported mobile modules and missing required capabilities.
- Verify disabling A for new registration does not disable its status,
  callbacks, deletion, or outstanding-payment/refund recovery.
- Prove no provider-specific naming or branching is required in card business
  rules to add the second compatible test integration. Keep the actual provider
  UI/SDK integration tests distinct from backend contract tests.

## Acceptance by Transaction

| ID | Completion evidence |
| --- | --- |
| `CARD-01` | One successful verified setup creates one owned card; duplicate requests/callbacks and crash recovery do not create duplicates; no PAN/CVV storage. |
| `CARD-02` | Stable bounded owner-only listing, empty list, deleted exclusion, expired availability, and safe response fields. |
| `CARD-03` | Owned detail, uniform foreign/missing response, malformed ID handling, and no secret fields. |
| `CARD-04` | Allowed metadata only, atomic default change, concurrent default requests, and invalid default rejection. |
| `CARD-05` | Idempotent removal, default clearing, new-payment rejection, deletion/acceptance race, and ongoing payment/refund recovery. |
| `CARD-06` | Verified challenge success/failure/cancel/expiry/replay/timeout; real-provider challenge timing confirmed; payment-time continuation verified if required. |
| `CARD-07` | Mobile tokenizes directly with dLocal; Pagarte accepts only token/session data; raw-card contracts and storage removed; temporary versus reusable tokens, expiry, reuse, sandbox integration, and absence of PAN/CVV in backend traffic/logs verified. |

Run unit tests for rules, database tests for uniqueness/atomicity/recovery, and
HTTP/gRPC tests for identity and transport behavior. Use mock HTTP contract
tests for external failures and callbacks. Run existing quote/payment/refund
regressions for token compatibility, ownership, expiry, and removal behavior.

Mock acceptance proves the backend workflow only. Record mock-complete and
real-provider-pending evidence separately; do not claim a live operator API or
3-D Secure integration has been verified from simulated challenges.

## Decisions Required Before Real-Provider Implementation

1. dLocal is selected. Confirm enabled countries/card types, token-based saving,
   reusable card_id, callback, idempotency/recovery, and deletion contracts for
   the account. The existing adapter is not proof of compatibility.
2. Confirm mobile framework and integration packaging for dLocal tokenization,
   public-key/environment configuration, and the supported challenge UI.
3. Confirm whether challenges occur at setup, payment, or both. If payment-time
   challenges are required, define durable execution continuation and company
   rollback behavior before extending the saga. This draft does not silently
   authorize an execution redesign.

The requester accepted the default-card and removal semantics above. Backend
mock work can be planned against them while these provider
decisions remain open.

## Delivery Order

1. Use the agreed dLocal mobile-tokenization flow and lifecycle/default rules.
2. Define provider-neutral token/session contracts, central operator selection,
   capability checks, and the mobile integration interface; protect internal gRPC.
3. Add mock operator setup/status/challenge scenarios and durable sessions.
4. Replace backend raw-card registration with token-based saving/verification
   and remove raw-card persistence. Define the mobile integration contract.
5. Harden list/detail/default/removal behavior and payment compatibility.
6. Complete challenge continuation, reconciliation, and restart recovery.
7. Run transaction and operator-replacement acceptance tests and update status
   with explicit evidence.
8. Integrate the chosen provider/client and complete its acceptance separately.

## Out of Scope

- Bank accounts, wallets, cash, transfers, and new payment-method families.
- Creating a production frontend or selecting a provider contract by assumption.
- Receiving raw PAN/CVV in Pagarte or adding a raw-card fallback when tokenization fails.
- Reworking catalogue, amount inquiries, quote pricing, or refund product scope.
- A payment-time challenge redesign until its linked execution decision is made.
- Compliance certification claims based solely on tokenization or mock tests.

## Documentation on Implementation

Update the [transaction inventory](../transactions.md), architecture,
Payments.Api, Processor and ExternalConnections context documents, and payment
lifecycle flow together. Keep current behavior distinct from this proposal.
Only close each transaction when its acceptance conditions are demonstrated.

## dLocal References

- [Native payment flow](https://docs.dlocal.com/docs/native-payment-dlocal-direct):
  mobile bridge, public-key configuration, and direct tokenization.
- [dLocal Direct](https://docs.dlocal.com/docs/using-dlocal-direct): temporary
  token creation, lifetime, and single-operation use.
- [Saving cards](https://docs.dlocal.com/docs/saving-cards): reusable card_id,
  retrieval, and deletion. Raw-card examples are not the selected input flow.
- [Create a card](https://docs.dlocal.com/reference/create-a-card): validate the
  enabled token-based request and required country/payer fields in sandbox.
