# Money Handling Standard

## Scope and Authority

Apply this standard when calculating, transporting, persisting, charging,
refunding, or displaying monetary values. [ADR 0004](../decisions/0004-money-handling.md)
records the decision and its rationale. Quote-specific composition and snapshot
behavior lives in [PayableServices](../contexts/payable-services.md#quote-design-composite-and-strategy).

## Representation and Calculation

- Reuse [Utilities.Money](../../src/Utilities/Utilities.Money/MoneyValue.cs) and
  its [currency catalogue](../../src/Utilities/Utilities.Money/CurrencyCatalog.cs).
  Currency is an uppercase ISO code with centrally defined minor-unit scale;
  do not assume two decimal places or accept a caller-selected scale.
- Calculate using decimal and checked arithmetic. Do not introduce float or
  double business calculations or monetary transport fields.
- Finalized gRPC and message amounts use signed 64-bit minor units plus currency.
  Preserve that exact value in persistence and external request mapping.
- Retain decimal precision during percentage calculations, then round each
  distinct monetary component before summing. The default is ToEven; current
  quote creation and execution support only ToEven. Do not enable an override
  merely because the shared utility accepts a rounding parameter.
- Represent discounts as explicit negative components. Validate matching
  currencies/scales and a positive total where required. Use checked summation
  in the same finalized-line order used by the consuming validator.
- Reject unsupported currency, invalid scale, overflow, inconsistent totals,
  and disallowed non-positive amounts before external work. Conversion to minor
  units must be exact after the required rounding.

## Snapshots and Operation Amounts

Quote creation finalizes the monetary lines and approved total. Downstream
services validate and copy those amounts; they do not recalculate a stored
quote using newer configuration. Preserve historical snapshots and their
original evidence when extending pricing.

Use the same finalized amount and currency for the same operation across
records, messages, and provider requests. Distinct operations can carry
explicitly different amounts: a USD 100 bill plus USD 2 fee has a USD 102 card
charge, USD 100 company acknowledgment, and USD 102 full compensation refund.
A company acknowledgment does not transfer money. See the
[payment flow](../flows/payment-lifecycle.md) for operation ownership.

## Implementation Limits

ADR 0004 allows an explicitly configured jurisdiction/provider rounding policy
and configurable business minimum/maximum amounts. These are not general
implemented quote capabilities. Current quote boundaries require ToEven and
checked signed 64-bit range; additional business limits remain a policy
extension. Document and verify any future implementation before claiming support.

## Verification

Use [Utilities.Money.Tests](../../tests/Utilities.Money.Tests/MoneyValueTests.cs)
for shared arithmetic and currency behavior. Pricing or transport changes also
need the relevant quote/Processor tests from the [Testing Standard](testing.md),
including zero- and three-decimal currencies, negative components, overflow,
and preservation of finalized amounts across the affected boundary.
