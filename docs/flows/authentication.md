# Authentication Flow

## Purpose

This flow describes Keycloak user access and internal-workload authentication
for quote execution.

## Client Access

No browser or native client application exists yet. The flow below is the
contract for future clients; the versioned public registrations were checked
with a synthetic Authorization Code + PKCE login.

```text
Client application
  -> Keycloak Authorization Code + PKCE
      -> registration, email verification, recovery, and session management
      -> signed access token with requested API audience

Client with access token
  -> ClientProfiles.Api requires client-profiles-api audience
  -> Payments.Api requires payments-api audience
  -> PayableServices.Api quote endpoints require payable-services-api audience
  -> protected endpoints require verified email and the named realm role
```

ClientProfiles creates its profile and `(iss, sub)` mapping atomically. Payments
and PayableServices send the validated issuer and subject to the protected
ClientProfiles resolver and receive the internal `Client.Id`. Their card and
quote operations use that GUID. Request bodies cannot select ownership.

## Workload Access

```text
authenticated client
  -> Payments or PayableServices resolves Client.Id with a scoped workload key
  -> PayableServices loads the client's persisted quote
  -> PayableServices sends x-workload-api-key over gRPC
  -> PaymentSwitch.Processor validates the workload credential
  -> Processor validates the selected stored card belongs to ClientId
```

Client authentication and workload authentication are independent. The
workload credential authorizes PayableServices to call the internal boundary;
it does not prove the card belongs to the client.

An internationally issued card may be used when supported by the configured
operator and currency. A stored card owned by another Pagarte client is always
rejected.

## Ownership

- Keycloak owns users, credentials, refresh tokens, audiences, and token policy.
- ClientProfiles owns the external-identity to Pagarte-client mapping.
- Each public API owns validation of its required audience.
- Processor owns workload authentication at `PaymentExecutionService`.
- Application and Domain projects do not depend on ASP.NET `HttpContext`.

## Change Rules

- Add a new audience in the Keycloak realm configuration and consuming API together.
- Rotate workload and company credentials through deployment configuration.
- Do not place signing keys, API keys, passwords, card tokens, refresh tokens,
  or connection strings in source control or RabbitMQ messages.
- Keep token issuance in Keycloak.
