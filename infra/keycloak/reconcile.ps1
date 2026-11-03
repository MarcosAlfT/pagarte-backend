param(
    [string]$KeycloakUrl = 'http://host.docker.internal:8080'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:PAGARTE_KEYCLOAK_ADMIN_PASSWORD)) {
    throw 'Set PAGARTE_KEYCLOAK_ADMIN_PASSWORD from the deployment secret store.'
}
if ($KeycloakUrl -notmatch '^https://' -and $KeycloakUrl -ne 'http://host.docker.internal:8080') {
    throw 'A nonlocal Keycloak URL must use HTTPS.'
}

$configDir = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$previousPassword = $env:KEYCLOAK_PASSWORD
try {
    $env:KEYCLOAK_PASSWORD = $env:PAGARTE_KEYCLOAK_ADMIN_PASSWORD
    docker run --rm `
        -e "KEYCLOAK_URL=$KeycloakUrl" `
        -e 'KEYCLOAK_USER=admin' `
        -e KEYCLOAK_PASSWORD `
        -e 'KEYCLOAK_AVAILABILITYCHECK_ENABLED=true' `
        -e 'KEYCLOAK_AVAILABILITYCHECK_TIMEOUT=120s' `
        -e 'IMPORT_FILES_LOCATIONS=/config/pagarte-realm.json' `
        -e 'IMPORT_MANAGED_ROLE=no-delete' `
        -e 'IMPORT_MANAGED_CLIENT=no-delete' `
        -e 'IMPORT_MANAGED_CLIENT_SCOPE=no-delete' `
        -v "${configDir}:/config:ro" `
        'adorsys/keycloak-config-cli:6.5.1-26'
    if ($LASTEXITCODE -ne 0) { throw "Keycloak reconciliation failed with exit code $LASTEXITCODE." }
}
finally {
    $env:KEYCLOAK_PASSWORD = $previousPassword
}
