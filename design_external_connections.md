# ExternalConnections Design

`ExternalConnections` is the shared adapter layer for external payment and company integrations. It isolates third-party integration details from the application and domain layers.

## Scope

This area contains:

- `ExternalConnections.PaymentOperators`
- `ExternalConnections.CompanyPayments`

## Runtime Boundary

```text
ExternalConnections.PaymentOperators
  -> payment operator adapters
  -> register card
  -> charge card
  -> refund card

ExternalConnections.CompanyPayments
  -> company payment adapters
  -> catalogue/feed bridge
  -> company payment delivery
```

## Responsibilities

- Provide the payment-operator adapter factory used by `PaymentSwitch.Processor` and `PaymentSwitch.Worker`.
- Keep provider-specific charge, registration, and refund details outside the payment core.
- Provide the company-payment adapter used by `PaymentSwitch.Worker`.
- Provide the company-payment feed bridge used by `PayableServices` catalogue sync.

## Notes

- This layer should not own business state.
- It should not expose public HTTP APIs.
- It should adapt third-party contracts into internal application abstractions.