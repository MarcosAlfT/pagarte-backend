# Mock Company

## Purpose

`MockCompany.Api` is a synthetic company HTTP resource for local development
and automated tests. It supplies complete catalogue snapshots and deterministic
account balances while real company integrations are unavailable.

## Owns

Synthetic service codes, account scenarios, provider identifiers, and HTTP
fixtures. `ELEC-EC`, `WATER-EC`, and `NET-US` exercise multiple categories,
providers, and countries; all valid fixture services use USD.

## Does Not Own

Pagarte databases, catalogue approval, client identities, inquiry persistence,
quotes, fees, taxes, or payment-saga decisions.

## Runtime Boundary

AppHost starts the resource and passes its endpoint and API key through server
configuration. It requires Development and an API key, and rejects unauthenticated
company calls. PayableServices waits for the mock resource before starting.
The mock is disabled by default outside the local AppHost configuration.

## Domain Rules

- `10000001` returns USD 128.45; `10000002` returns a verified zero balance.
- `10000003` and other valid unknown accounts return reference not found.
- `10000004` returns unavailable; `10000005` returns a deterministic provider
  timeout response; `10000006` returns malformed balance evidence.
- Responses are keyed by the server-generated provider key. Provider IDs derive
  from that key; timestamps derive from the immutable request reservation time,
  validated against the mock clock. Replaying after a mock restart therefore
  preserves the same balance ID and timestamps.
- Test clocks and transport timeout injection avoid timing-dependent tests.
- Catalogue fixtures cover initial, repeated, updated, removed, duplicate-code,
  malformed, incomplete, incompatible currency/reference, and unavailable feeds.
  Fixture selection is server configuration used by tests; public clients cannot
  select a provider destination or feed fixture.
- Synthetic payment/status/rollback endpoints satisfy the existing company HTTP
  contract for local execution; payment-state decisions remain in Processor.

## Data Ownership

The mock uses an in-memory response cache and writes no Pagarte database.
Its deterministic balance evidence survives cache recreation because it derives
from the provider key and stored reservation timestamp.

## Integrations

`ExternalConnections.CompanyInquiries` translates its provider HTTP schema.
See [PayableServices](payable-services.md) and
[acceptance tests](../../tests/PayableServices.Tests/README.md).

## Change Guidance

The accepted [Quotes requirement](../requirements/quotes.md) needs additional
mutable bill fixtures: Strategy-selected identity items including billing dates,
external-channel payment, changed amount/period, and post-charge notification
OK/NOT OK with idempotent acknowledgment/status lookup. Inject lost responses
and a different-channel payment between verification and notification to verify
recovery or compensation. Notifications record bill payment, not money transfer.
These fixtures are pending and must retain isolated, deterministic test state;
current fixed balance evidence does not verify these cases.

Keep all fixtures synthetic. A real company replaces the adapter/configuration;
it must not replace inquiry ownership, idempotency, or quote domain rules.
