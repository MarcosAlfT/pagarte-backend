# Pagarte.API Design

`Pagarte.API` is the public Pagarte HTTP boundary. It is intentionally narrow and currently exposes only the credit-card surface.

## Scope

The public API keeps:

- Credit card listing.
- Credit card lookup.
- Credit card registration.
- Credit card update.
- Credit card deletion.

The public API does not expose payment quote endpoints or service catalog endpoints anymore.

## Runtime Boundary

```text
Pagarte.API
  -> HTTP API
  -> OpenIddict validation
  -> gRPC client to Pagarte.Services
  -> Responses
  -> Pagarte.Contracts
```

## Responsibilities

- Validate the client identity token.
- Extract the authenticated client id from the token subject.
- Keep controllers thin.
- Call `Pagarte.Services` over gRPC for credit-card operations.
- Return the standard `Responses` wrapper.

## Notes

- This project is not responsible for payment persistence.
- It does not call payment operators directly.
- It does not own the service catalog or payment quote workflows.
- Its job is the public API edge in front of the internal payment service.
