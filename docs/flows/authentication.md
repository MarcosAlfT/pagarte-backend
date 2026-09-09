# Authentication Flow

## Purpose

This flow describes how client access tokens are issued and validated by the
public APIs.

## Current Flow

```text
Client
  -> Identity.Client.Api
      -> registration, email confirmation, or login
      -> OpenIddict access token

Client with access token
  -> ClientProfiles.Api
      -> validates issuer and client-profiles-api audience

Client with access token
  -> Payments.Api
      -> validates issuer and payments-api audience
```

## Ownership

- `Identity.Client` owns users, credentials, refresh tokens, and token policy.
- OpenIddict signs and validates access tokens.
- Each consuming API owns validation of its required audience.
- Application and Domain projects do not depend on ASP.NET `HttpContext`.

## Current Limits

`PayableServices.Api` does not currently configure the Identity.Client
authentication and authorization boundary described for the other public APIs.
This should be discussed before treating its catalogue and quote endpoints as a
secured production surface.

## Change Rules

- Add a new audience in Identity and the consuming API together.
- Do not place signing keys, passwords, refresh tokens, or connection strings in
  source control.
- Keep token issuance in Identity rather than duplicating it in another API.
