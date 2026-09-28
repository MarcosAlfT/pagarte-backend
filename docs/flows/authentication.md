# Authentication Flow

## Purpose

This flow describes client-token and internal-workload authentication for quote
execution.

## Client Access

```text
Client
  -> Identity.Client.Api
      -> registration, email confirmation, or login
      -> OpenIddict access token

Client with access token
  -> ClientProfiles.Api requires client-profiles-api audience
  -> Payments.Api requires payments-api audience
  -> PayableServices.Api quote endpoints require payable-services-api audience
```

Quote controllers derive `ClientId` from the validated `sub` claim. Quote
request bodies cannot select or override ownership.

## Workload Access

```text
authenticated client
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

- `Identity.Client` owns users, credentials, refresh tokens, audiences, and
  token policy.
- Each public API owns validation of its required audience.
- Processor owns workload authentication at `PaymentExecutionService`.
- Application and Domain projects do not depend on ASP.NET `HttpContext`.

## Change Rules

- Add a new audience in Identity and the consuming API together.
- Rotate workload and company credentials through deployment configuration.
- Do not place signing keys, API keys, passwords, card tokens, refresh tokens,
  or connection strings in source control or RabbitMQ messages.
- Keep token issuance in Identity.
