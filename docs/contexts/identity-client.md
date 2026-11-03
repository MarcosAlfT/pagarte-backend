# Retired Identity.Client Source

`Identity.Client` no longer starts in AppHost and its projects are excluded from
the solution. Keycloak now owns interactive authentication, credentials,
sessions, refresh tokens, and access-token issuance. The source and historical
`IdentityClientDb` migrations remain temporarily for a data inventory and
client-application cutover; no running Pagarte service should use that database.

See [Authentication Flow](../flows/authentication.md) and the
[migration requirement](../requirements/keycloak-security-migration.md).
Do not deploy or restart `Identity.Client.Api` as a second authority.
