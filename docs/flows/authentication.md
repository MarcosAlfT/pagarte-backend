# Authentication Flow

## Purpose

This flow describes Keycloak user access and internal-workload authentication
for card operations and quote execution.

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
  -> Payments sends card/session requests over gRPC with x-workload-api-key
  -> PayableServices loads the client's persisted quote and sends the same header
  -> PaymentSwitch.Processor validates the workload credential
  -> Processor validates the selected stored card belongs to ClientId
```

Client authentication and workload authentication are independent. The
workload credential authorizes Payments and PayableServices to call the card
and execution boundaries; it does not prove card ownership. The ClientProfiles
resolver has a separate credential. The local AppHost currently shares one
Processor workload key between Payments and PayableServices.

An internationally issued card may be used when supported by the configured
operator and currency. A stored card owned by another Pagarte client is always
rejected.

## Card-Setup Provider Notifications

Payments exposes an anonymous, bounded callback route independently of client
JWT authentication. It forwards the body and bounded allowlisted authentication headers over the authenticated
card gRPC boundary. The selected ExternalConnections adapter verifies provider
authenticity and freshness; Processor binds the event to its original
integration/session and deduplicates the inbox. Events only schedule status
lookup. The enabled setup verifier is synthetic and Development-only. dLocal raw-body HMAC
verification is independently tested, while its setup-event correlation remains gated. See [Card Registration](card-registration.md).

## Card Operations Administration

VerifiedAdmin protects the payment-method review HTTP boundary. Payments derives the
audit actor from the signed issuer/subject and calls CardOperationsService with a
separate CardOperationsAuth:ApiKey. Processor grants that credential a card-operations
claim; the general workload key cannot call review RPCs. The operations key must differ
from the general key. Application use cases persist the actor, coded reason, expected
state, request key, intent, and provider-derived result. Client card endpoints do not
expose these actions. The public API never accepts a caller-supplied audit actor.

## Ownership

- Keycloak owns users, credentials, refresh tokens, audiences, and token policy.
- ClientProfiles owns the external-identity to Pagarte-client mapping.
- Each public API owns validation of its required audience.
- Processor requires workload authentication at `CreditCardService` and
  `PaymentExecutionService`. Payment-query and catalogue gRPC protection remains
  tracked as partial SEC-02 work.
- Application and Domain projects do not depend on ASP.NET `HttpContext`.

## Change Rules

- Add a new audience in the Keycloak realm configuration and consuming API together.
- Rotate workload and company credentials through deployment configuration.
- Do not place signing keys, API keys, passwords, card tokens, refresh tokens,
  or connection strings in source control or RabbitMQ messages.
- Keep token issuance in Keycloak.
