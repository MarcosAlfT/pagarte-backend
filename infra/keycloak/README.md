# Keycloak local setup and reconciliation

AppHost runs `quay.io/keycloak/keycloak:26.8.1` and a dedicated PostgreSQL 17.6
container with a persistent `pagarte-keycloak-db` volume. The first startup
imports [pagarte-realm.json](pagarte-realm.json). Later startup imports do not
update an existing realm.

Configure the AppHost parameters through user secrets or the deployment secret
store: `keycloak-db-username`, `keycloak-db-password`,
`keycloak-admin-password`, `client-identity-resolver-api-key`, and the existing
SQL Server connection strings and workload credentials. Use a high entropy
resolver key. The local issuer is `http://localhost:8080/realms/pagarte`; the
HTTP exception is for loopback development only. The browser redirect is
`http://localhost:3000/auth/callback`, and the native redirect is
`http://127.0.0.1:3001/auth/callback`. Replace these with exact registered URLs
for each deployment. Start AppHost with its `https` launch profile.

The realm enables self-registration, email verification, recovery, a 15 minute
access token, and optional audience scopes. Browser and native clients request
only the API audience scopes they need. Configure SMTP separately in the
environment; no SMTP credentials are committed. Users receive `client` through
`default-roles-pagarte`. When provisioning an operator, remove the
`default-roles-pagarte` assignment before granting `support` or `admin`, then
verify that its issued token has no `client` role. API `VerifiedClient` also
rejects tokens carrying `admin` or `support` as a guard against mixed roles.

For an existing realm, back up Keycloak PostgreSQL and export the realm first.
Review the JSON diff, then set `PAGARTE_KEYCLOAK_ADMIN_PASSWORD` from a secret
store and run `./reconcile.ps1` in PowerShell. The pinned config CLI applies
realm, client, scope, and role changes through the Admin API while preserving
unspecified roles, clients, and client scopes. Review its output and verify the
realm again before accepting the change. Roll back with the database backup
and matching realm export if reconciliation changes existing behavior.

Before allowing card or quote traffic, inventory nonproduction
`IdentityClientDb`, `ClientProfilesDb`, `PaymentDb`, and `PayableServicesDb`.
Confirm no production data exists. Existing card, quote, payment, and execution
records containing the former token subject must be deliberately reset or
converted to the new internal client GUID; do not reinterpret those strings.
The additive ClientProfiles migration retains legacy `UserId` values for this
check. The old Identity.Client projects stay in source control until the client
application cutover and this inventory are verified.

The requester confirmed there are no browser or native client applications
yet. The public-client registrations can be exercised with a synthetic PKCE
test; future applications must use Keycloak before release.
