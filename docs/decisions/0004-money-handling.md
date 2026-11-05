# ADR 0004: Global Money Handling

## Status

Accepted; implemented for quote and payment execution.

## Context

At the time of the decision, the backend mixed .NET `decimal`, SQL decimal
columns, and protobuf `double`. Binary floating-point transport can change an exact
monetary value, and the existing contracts do not define currency scale,
rounding, range, or total-validation rules.

## Decision

Use one shared exact-money model across components: decimal calculations,
centrally defined ISO currency scales, component rounding, and checked signed
64-bit minor units plus currency for finalized service/message amounts. Reject
invalid or inconsistent money before external operations. Quotes finalize their
approved amounts once; downstream execution validates and preserves them.

The default midpoint rule is ToEven. The accepted direction allows an explicit
jurisdiction/provider policy and configurable business transaction limits,
with effective policies retained for audit. Those extensions require explicit
implementation; their current limits are stated below.

The maintained [Money Handling Standard](../standards/money-handling.md) contains
implementation conventions, operation-amount distinctions, and verification
entry points. This ADR retains the architectural choice and consequences.

## Consequences

- Shared contracts need a reusable money message or equivalent value type.
- Migrating the former protobuf `double` monetary fields required coordinated
  producer/consumer changes; finalized quote/payment contracts now use minor units.
- Persistence mappings and tests must verify precision, limits, rounding,
  negative components, overflow, and cross-service round trips.
- Quote construction is the boundary where component values become a final,
  immutable total. Downstream services validate and copy that value rather
  than independently recalculating it.
- `Utilities.Money` provides the shared ISO scale catalogue, exact conversion,
  ToEven rounding, and checked sum. Quote/payment persistence uses minor units
  for finalized amounts and expanded decimal precision for source catalogue
  values.
- Quote boundaries currently support ToEven only; the shared utility accepting
  other rounding modes does not enable them for quotes.
- Configurable business minimum and maximum amounts remain a separate policy
  extension; the representable range is currently enforced by checked signed
  64-bit minor units.

## Related Documentation

- [Quote Execution Saga and Legacy Quote Removal](0003-quote-execution-saga.md)
- [Quote Lifecycle](../flows/quote-lifecycle.md)
- [Payment Lifecycle](../flows/payment-lifecycle.md)
