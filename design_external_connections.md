# ExternalConnections Design

`ExternalConnections` is the shared adapter layer for external payment and company integrations. It replaces the old `Pagarte.Connections` assembly.

## Scope

This layer is for infrastructure adapters only. It should not own business rules, persistence, or workflow orchestration.

It currently contains:

- `ExternalConnections.CardOperators`
- `ExternalConnections.CompanyPayments`

## Runtime Boundary

```text
ExternalConnections.CardOperators
  -> payment operator adapters
  -> adapter factory
  -> resilience-enabled HttpClient configuration

ExternalConnections.CompanyPayments
  -> company payment adapter
  -> outbound HTTP calls to company APIs
```

## Responsibilities

- Provide the payment-operator adapter factory used by `Pagarte.Services` and `Pagarte.Engine`.
- Provide card registration, charge, and refund adapters for supported operators.
- Provide the company-payment adapter used by `Pagarte.Engine`.
- Keep outbound HTTP concerns isolated from the application and domain layers.

## Notes

- Operator selection is configured and resolved through the application/database model, not hardcoded in the business flow.
- Card operator tokens and payment ids are provider-specific and must retain the provider they came from.
- The catalogue sync bridge in `PaymentServices` also relies on the current company-payment feed path while the split is in progress.
