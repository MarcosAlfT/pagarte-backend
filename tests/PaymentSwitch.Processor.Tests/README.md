# Processor and Payment-Method Acceptance Tests

## Running

```powershell
dotnet test tests/PaymentSwitch.Processor.Tests/PaymentSwitch.Processor.Tests.csproj
```

SQL Server LocalDB is the default. Set PAGARTE_TEST_SQL for another local test
server; the identity needs database create/delete permissions. Each rig uses
and removes a unique PagarteCardTests_ database, never an application database.

TestServer runs the actual Payments HTTP/Processor gRPC routes, signed test JWTs,
identity resolution, and SQL migrations/constraints. Explicit recovery calls replace
RabbitMQ/background loops. A separate per-test provider journal survives host restarts.

`QuoteSubmissionGrpcTests` runs the actual PayableServices execution client over
workload-authenticated SubmitClaimedQuote and SubmitVerifiedBill gRPC and SQL
acceptance/rejection records. It verifies late acceptance of a timely claim, invalid timestamps,
concurrent delivery, unchanged rejection after card eligibility changes, and
blocking new execution through legacy ConfirmQuote. Bill tests cover cross-client
reservation while in flight and after success, stale evidence at acceptance and
before company payment, and fresh verification following reservation release.
`ChargeFirstGrpcTests` covers fresh evidence before charge, lost charge and
refund commits, company acknowledgment recovery across provider restart, retry
exhaustion, late external settlement, Worker isolation, and original-mode recovery.
Live company and operator acceptance remains pending.

## Local Runtime Configuration

AppHost selects Development-only mock-a with EC/USD and synthetic removal/refund
capabilities. Supply secrets through user secrets or the existing parameter mechanism:

| AppHost parameter | Purpose |
| --- | --- |
| card-token-digest-key | Stable token claims; at least 32 ASCII characters. |
| mock-card-callback-signing-key | Mock callback HMAC; at least 32 ASCII characters. |
| card-operations-workload-api-key | Payments/Processor admin reviews; must differ from the general workload key. |

Keep values out of source control. Outside AppHost, configure:

| Configuration | Required behavior |
| --- | --- |
| PaymentOperators:DefaultIntegration | New-registration selection. |
| PaymentOperators:Integrations:{id} | Adapter, Account, Environment, Version, RegistrationEnabled, SupportedCountries/Currencies; mock CallbackKeyReference or dLocal CredentialReference. |
| CardRemovalEnabled / RefundsSurviveDetachment | Separate integration capabilities, default false; synthetic AppHost account enables both. |
| PaymentOperatorCallbackSecrets:{reference} | Mock callback secret. |
| PaymentMethods:TokenDigestKey | Stable while token claims are retained. |
| PaymentMethods:ConsentVersion | Policy shown before registration; default saved-card-v1. Approved wording/mobile UI remain pending. |
| CardOperationsAuth:ApiKey | Distinct admin workload credential in Payments and Processor. |

Processor pins integration identities/credential references; Worker reads them before
consuming work and briefly waits for schema/pins. Use a new ID for identity/reference
changes; retain old routes for dependent work. Rotate secret values under the same reference.

Configured dLocal credentials live under PaymentOperatorCredentials:{reference}:
matching Account/Environment, separate XLogin/XTransKey/SecretKey, optional PublicKey.
Sandbox/Production choose fixed HTTPS hosts; global fallback is forbidden. Legacy raw
DLocal uses PaymentOperator:XLogin (or ApiKey), XTransKey, and SecretKey; ApiSecret is
not accepted. Public dLocal setup remains disabled; see the
[remaining contracts](../../docs/requirements/payment-methods.md#confirmations-required-before-enabling-dlocal-registration).

Share a durable Data Protection key ring across Processor instances. Losing keys before
submission can cause TOKEN_UNAVAILABLE and require new tokenization. The default key
ring is per-user. PaymentOperators:MockStateDirectory optionally selects the development
journal; its default is pagarte-mock-card-setup under the system temp directory. Retain
it across restarts. It stores synthetic state/tombstones, never submitted tokens.

## Manual Mock Flow

Use a verified client token and only synthetic data:

1. GET api/creditcard/registrations/policy; obtain consent to the displayed policy.
2. POST api/creditcard/registrations with Idempotency-Key, cardHolderName, optional
   isDefault, consentAccepted=true, and consentVersion. Missing/stale consent is 400/409.
3. Build `mock:{integrationId}:{sessionIdWithoutHyphens}:{scenario}` from the response's
   integration/session IDs and SyntheticToken version 1.
4. POST api/creditcard/registrations/{id}/complete with a separate Idempotency-Key and
   temporaryToken. Read status or POST {id}/continue when due; the scheduler also resumes it.
5. List/view/update/remove through api/creditcard using the returned Pagarte card ID.
   GET api/creditcard/{id}/removal reports asynchronous provider cleanup separately.

Fixtures: success, decline, malformed, challenge-success, challenge-decline,
challenge-cancel, challenge-expire, timeout, save-before-timeout, unavailable,
same-instrument. The last returns a stable reference to test deduplication and explicit
re-registration. Challenges simulate issuer interaction through status, without issuer UI.

Creation/completion keys contain 1–100 printable characters; sessions expire after
15 minutes. The existing response wrapper uses these statuses:

| Outcome | HTTP |
| --- | --- |
| Pending, Verifying, Unresolved | 202 |
| RequiresAction, Succeeded, Cancelled, local removal | 200 |
| Failed / Expired | 422 / 410 |
| Invalid / missing or foreign / conflict / unavailable | 400 / 404 / 409 / 503 |

Cleanup can be pending, deferred, removed, unsupported, retained, or require review.
Unknown submitted removal blocks new registration; local deletion never proves erasure.

## Admin Review Flow

Use a verified admin token. Client/support roles are denied; the operations workload
key stays server-side.

1. GET api/admin/payment-methods/reviews?limit=50 lists stuck operations (limit 1–100).
2. POST api/admin/payment-methods/reviews/{kind}/{targetId} with Idempotency-Key and
   action, expectedState, reasonCode.
3. GET api/admin/payment-methods/reviews/{reviewId}/audit using the returned review ID.

| Kind | Expected state | Actions |
| --- | --- | --- |
| Registration | Unresolved | Reconcile |
| SetupCleanup / CardRemoval | RequiresReview | Reconcile, RetryRemoval |

Reason codes: CustomerSupportReview, ProviderStatusAvailable, ProviderIncidentResolved.
Actor comes from the signed issuer/subject; supplied actor/success/reference fields are
rejected. Changed key inputs conflict; identical inputs replay the audit. Automatic
budgets remain unchanged, and removal retries still check dependencies/refund guarantees.

A lost result commit leaves Pending; same-key replay returns 202 without another call.
After the two-minute lease expires, a new explicit review can recheck evidence and mark
the prior audit ResultUnknown. Expired/cancelled setup cannot activate; uncertain results
retain review barriers. Full state behavior is in the [flow](../../docs/flows/card-registration.md).

## Mock Callback Contract

POST api/payment-method-callbacks/{integrationId} without client JWT authentication.
The exact JSON body contains sessionId and eventId. Supply X-Operator-Timestamp as Unix
seconds and X-Operator-Signature as hex HMAC-SHA256 of UTF-8
`mock-callback-v1\n{timestamp}.` followed by the raw body. The newline is an actual LF.
Use the integration's referenced callback key; timestamps must be within five minutes.
Accepted notifications only schedule provider lookup and are durably deduplicated.

PaymentMethods:CallbackHeaders defaults to X-Operator-Timestamp, X-Operator-Signature,
X-Date, Authorization. Transport allows ten headers/4096 characters and 8192 body bytes;
the adapter chooses its verifier. Deprecated gRPC timestamp/signature fields are ignored.
A valid dLocal payment signature alone is not an accepted setup event.

## Evidence

| Test area | Coverage |
| --- | --- |
| Lifecycle/SQL | Ownership, profile/workload auth, consent, retired raw inputs, safe responses, keys/token claims, concurrency/default rollback, expiry, migration preconditions. |
| Recovery/removal | Provider success before failed SQL commit/restart, retry caps after lease expiry, late resource cleanup, shared references, accepted work/refunds, immutable route/credential mutations. |
| Admin HTTP/gRPC/SQL | VerifiedAdmin/separate-key enforcement, audit replay/conflicts, stale-state rejection, safe retries, lost commits, and no late activation. |
| Offline dLocal HTTP | Token-only/trusted-payer request mapping, exact-body signatures, separate accounts, strict metadata/reference checks, bounded responses/timeouts, no save retries, verified deletion, and signature tampering. |
| Existing money movement | Full-total company-first saga; explicit PAID/SUCCESS, pending/unknown results, and mismatched charge-reference rejection. |
| Claimed quote submission | Actual PayableServices handoff client, workload-authenticated gRPC/SQL, timely claim accepted after expiry, concurrent delivery, immutable rejection, legacy-new-execution refusal, and downgrade protection. |

Mock-a/mock-b prove shared lifecycle routing within the synthetic adapter. Injected dLocal
HTTP fixtures make no network calls and do not prove bound setup, Direct-token merchant
compatibility, live recovery, or mobile/issuer UI. Those remain in
[Current Problems](../../docs/current-problems.md#6-payment-method-acceptance-and-recovery-gaps).

## Verification Record (Implementation Run)

2026-11-04 submission slice: all 103 Processor/card/provider tests, 53
PayableServices tests, nine security tests, and seven money tests passed.
Only isolated test databases were migrated. New quote submission evidence and
acceptance/rejection outcomes block destructive downgrade. The company-first
saga was used in this run. The bill verification cases above cover subsequent work.

2026-11-03: 97 Processor/payment-method tests passed. After final full-response
hardening, 32 focused dLocal/configuration tests passed again. AppHost built; EF found
no pending model changes. Markdown links, Git whitespace, UTF-8, and CRLF checks passed.
The dLocal timeout is 30 seconds with a 32 KiB full-response cap.

An earlier run passed 36 catalogue/inquiry, nine security, and seven money tests;
those unchanged suites were not rerun for the hardening pass. Existing NuGet advisories
for MessagePack, Microsoft.OpenApi, and System.Security.Cryptography.Pkcs remain.
Only isolated acceptance databases were migrated; application databases/configuration
were untouched. Full Docker/Aspire and live dLocal/mobile acceptance were not run.
