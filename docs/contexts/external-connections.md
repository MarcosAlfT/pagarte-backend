# ExternalConnections

## Purpose

`ExternalConnections` isolates third-party payment-operator and company
integration details from application and domain workflows.

## Owns

- Payment-operator adapter contracts and implementations.
- Payment-operator selection support.
- Card registration, charge, and refund provider calls.
- Company payment-delivery HTTP adaptation.

## Does Not Own

- Public APIs.
- Payment, quote, catalogue, or client business state.
- Workflow orchestration.
- Retry policy decisions owned by application use cases.

## Runtime Boundary

```text
PaymentSwitch.Processor
  -> ExternalConnections.PaymentOperators
  -> external payment operator

PaymentSwitch.Worker
  -> ExternalConnections.PaymentOperators
  -> external payment operator

PaymentSwitch.Worker
  -> ExternalConnections.CompanyPayments
  -> external company payment endpoint
```

## Projects and Layers

- `ExternalConnections.PaymentOperators` contains payment-operator
  configuration, adapters, and factories.
- `ExternalConnections.CompanyPayments` contains `ICompanyAdapter` and the
  HTTP implementation used for company payment delivery.

## Main Use Cases

This area implements technical calls requested by other contexts. It does not
define application use cases of its own.

## Domain Rules

Adapters translate external contracts into internal results. They should not
decide whether a payment, refund, or retry is allowed.

## Data Ownership

`ExternalConnections` owns no business database.

## Integrations

- Used by Processor for card registration and authorization.
- Used by Worker for payment delivery and refunds.
- The external catalogue/feed adapter described by the earlier design is not
  implemented. PayableServices currently reads Processor's internal service
  catalogue instead.

## Change Guidance

Keep provider-specific request, authentication, and response handling here.
Expose stable internal abstractions and keep provider contracts out of
Application and Domain projects.
