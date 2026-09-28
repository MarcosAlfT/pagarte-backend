# ADR 0004: Global Money Handling

## Status

Accepted; implemented for quote and payment execution.

## Context

The backend currently uses a mixture of .NET `decimal`, SQL decimal columns,
and protobuf `double`. Binary floating-point transport can change an exact
monetary value, and the existing contracts do not define currency scale,
rounding, range, or total-validation rules.

## Decision

The following rules apply to every Pagarte component that creates, calculates,
transports, persists, charges, refunds, rolls back, or displays money:

- Currency is an uppercase ISO 4217 code.
- A centrally maintained currency definition supplies the permitted minor-unit
  scale. A caller cannot choose an arbitrary scale.
- Protobuf and asynchronous message contracts transport finalized monetary
  values as signed 64-bit integer minor units plus the currency code. They do
  not use `double` or `float`.
- .NET calculations use `decimal` and checked arithmetic. Binary floating-point
  values are not used for business calculations or converted directly into
  money.
- Intermediate percentage calculations retain sufficient decimal precision.
  Each legally or contractually distinct amount component is rounded to the
  currency scale before the quote total is calculated.
- The default midpoint rule is `MidpointRounding.ToEven`. A jurisdiction or
  external-provider rule may override it only through explicit configuration;
  the effective rule is snapshotted with the quote.
- A quote total equals the checked sum of its finalized components. Discounts
  and other subtractive components are represented explicitly. The final total
  must be greater than zero unless a separate business requirement permits a
  zero-value transaction.
- Conversion to minor units must be exact after required rounding. Overflow,
  unsupported currency, excess scale, non-positive disallowed totals, and
  inconsistent component currencies are rejected before an external call.
- Business minimum and maximum transaction amounts are configurable by
  currency. The effective limits used to validate a quote are auditable.
- A charge, refund, rollback, payment record, quote, outbox message, and
  external request for the same operation use the same finalized minor-unit
  amount and currency.

Examples:

```text
USD 10.25  -> currency = USD, minor units = 1025
JPY 10     -> currency = JPY, minor units = 10
KWD 10.123 -> currency = KWD, minor units = 10123
```

## Consequences

- Shared contracts need a reusable money message or equivalent value type.
- Existing protobuf `double` monetary fields must be migrated or versioned.
- Persistence mappings and tests must verify precision, limits, rounding,
  negative components, overflow, and cross-service round trips.
- Quote construction is the boundary where component values become a final,
  immutable total. Downstream services validate and copy that value rather
  than independently recalculating it.
- `Utilities.Money` provides the shared ISO scale catalogue, exact conversion,
  ToEven rounding, and checked sum. Quote/payment persistence uses minor units
  for finalized amounts and expanded decimal precision for source catalogue
  values.
- Configurable business minimum and maximum amounts remain a separate policy
  extension; the representable range is currently enforced by checked signed
  64-bit minor units.

## Related Documentation

- [Quote Execution Saga and Legacy Quote Removal](0003-quote-execution-saga.md)
- [Quote Lifecycle](../flows/quote-lifecycle.md)
- [Payment Lifecycle](../flows/payment-lifecycle.md)
