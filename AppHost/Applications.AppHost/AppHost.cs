using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var rabbitConnectionString = builder.AddParameter("rabbit-connection-string", secret: true);
var paymentDbConnectionString = builder.AddParameter("payment-db-connection-string", secret: true);
var clientProfilesDbConnectionString = builder.AddParameter("client-profiles-db-connection-string", secret: true);
var payableServicesDbConnectionString = builder.AddParameter("payable-services-db-connection-string", secret: true);
var workloadApiKey = builder.AddParameter("payable-services-workload-api-key", secret: true);
var qecApiKey = builder.AddParameter("qec-company-api-key", secret: true);
var keycloakDbPassword = builder.AddParameter("keycloak-db-password", secret: true);
var keycloakDbUsername = builder.AddParameter("keycloak-db-username");
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);
var resolverApiKey = builder.AddParameter("client-identity-resolver-api-key", secret: true);
const string keycloakIssuer = "http://localhost:8080/realms/pagarte";

var keycloakPostgres = builder.AddPostgres("keycloak-db", keycloakDbUsername, keycloakDbPassword)
    .WithImageTag("17.6")
    .WithDataVolume("pagarte-keycloak-db");
var keycloakDb = keycloakPostgres.AddDatabase("keycloak-store", "keycloak");

var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak:26.8.1")
    .WithArgs("start-dev", "--import-realm")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithEnvironment("KC_DB", "postgres")
    .WithEnvironment("KC_DB_URL", "jdbc:postgresql://keycloak-db:5432/keycloak")
    .WithEnvironment("KC_DB_USERNAME", keycloakDbUsername)
    .WithEnvironment("KC_DB_PASSWORD", keycloakDbPassword)
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", keycloakAdminPassword)
    .WithEnvironment("KC_HOSTNAME", "http://localhost:8080")
    .WithBindMount(Path.Combine(AppContext.BaseDirectory, "keycloak-realm"), "/opt/keycloak/data/import", isReadOnly: true)
    .WaitFor(keycloakDb);

if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("This local AppHost requires Development.");
var mockCompany = builder.AddProject<Projects.MockCompany_Api>("mock-company", launchProfileName: null)
    .WithHttpEndpoint(name: "http")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("MockCompany__ApiKey", qecApiKey);

// Client profiles API
var clientProfilesApi = builder.AddProject<Projects.ClientProfiles_Api>("client-profiles-api", launchProfileName: "https")
    .WithEnvironment("AuthSettings__Authority", keycloakIssuer)
    .WithEnvironment("ConnectionStrings__ClientProfilesDb", clientProfilesDbConnectionString)
    .WithEnvironment("ClientIdentityResolver__ApiKey", resolverApiKey)
    .WaitFor(keycloak);

// Payment switch processor
var paymentSwitchProcessor = builder.AddProject<Projects.PaymentSwitch_Processor>("payment-switch-processor", launchProfileName: "PaymentSwitch.Processor")
    .WithEnvironment("RabbitMQ__Mode", "FromEnvironment")
    .WithEnvironment("ConnectionStrings__PagQueue", rabbitConnectionString)
    .WithEnvironment("ConnectionStrings__PaymentDb", paymentDbConnectionString)
    .WithEnvironment("WorkloadAuth__ApiKey", workloadApiKey)
    .WithEnvironment("CompanyCredentials__QEC__ApiKey", qecApiKey)
    .WithEnvironment("CompanyCredentials__MockCompany__ApiKey", qecApiKey);

var payableServicesApi = builder.AddProject<Projects.PayableServices_Api>("payable-services-api", launchProfileName: "PayableServices.Api")
    .WithReference(mockCompany)
    .WaitFor(mockCompany)
    .WithReference(clientProfilesApi)
    .WithReference(paymentSwitchProcessor)
    .WithEnvironment("CompanyProviders__MockCompany__Enabled", "true")
    .WithEnvironment("CompanyProviders__MockCompany__BaseUrl", mockCompany.GetEndpoint("http"))
    .WithEnvironment("CompanyProviders__MockCompany__ApiKey", qecApiKey)
    .WithEnvironment("ConnectionStrings__PayableServicesDb", payableServicesDbConnectionString)
    .WithEnvironment("AuthSettings__Authority", keycloakIssuer)
    .WithEnvironment("ClientIdentityResolver__BaseUrl", clientProfilesApi.GetEndpoint("https"))
    .WithEnvironment("ClientIdentityResolver__ApiKey", resolverApiKey)
    .WithEnvironment("PaymentSwitchProcessor__GrpcUrl", paymentSwitchProcessor.GetEndpoint("https"))
    .WithEnvironment("PaymentSwitchProcessor__AllowUntrustedCertificate", "true")
    .WithEnvironment("WorkloadAuth__ApiKey", workloadApiKey)
    .WaitFor(keycloak);

// Payments API
var paymentsApi = builder.AddProject<Projects.Payments_Api>("payments-api")
    .WithReference(clientProfilesApi)
    .WithReference(paymentSwitchProcessor)
    .WithEnvironment("AuthSettings__Authority", keycloakIssuer)
    .WithEnvironment("ClientIdentityResolver__BaseUrl", clientProfilesApi.GetEndpoint("https"))
    .WithEnvironment("ClientIdentityResolver__ApiKey", resolverApiKey)
    .WithEnvironment("PaymentSwitchProcessor__GrpcUrl", paymentSwitchProcessor.GetEndpoint("https"))
    .WaitFor(keycloak);

// Payment switch worker
builder.AddProject<Projects.PaymentSwitch_Worker>("payment-switch-worker")
    .WithEnvironment("RabbitMQ__Mode", "FromEnvironment")
    .WithEnvironment("ConnectionStrings__PagQueue", rabbitConnectionString)
    .WithEnvironment("ConnectionStrings__PaymentDb", paymentDbConnectionString);


builder.Build().Run();
