var builder = DistributedApplication.CreateBuilder(args);

var rabbitConnectionString = builder.AddParameter("rabbit-connection-string", secret: true);
var paymentDbConnectionString = builder.AddParameter("payment-db-connection-string", secret: true);
var payableServicesDbConnectionString = builder.AddParameter("payable-services-db-connection-string", secret: true);
var workloadApiKey = builder.AddParameter("payable-services-workload-api-key", secret: true);
var qecApiKey = builder.AddParameter("qec-company-api-key", secret: true);

// Identity client API
var identityClientApi = builder.AddProject<Projects.Identity_Client_Api>("identity-client-api", launchProfileName: "https");

// Client profiles API
var clientProfilesApi = builder.AddProject<Projects.ClientProfiles_Api>("client-profiles-api", launchProfileName: "https")
	.WithReference(identityClientApi)
	.WithEnvironment("AuthSettings__Authority", identityClientApi.GetEndpoint("https"));

// Payment switch processor
var paymentSwitchProcessor = builder.AddProject<Projects.PaymentSwitch_Processor>("payment-switch-processor", launchProfileName: "PaymentSwitch.Processor")
	.WithEnvironment("RabbitMQ__Mode", "FromEnvironment")
	.WithEnvironment("ConnectionStrings__PagQueue", rabbitConnectionString)
	.WithEnvironment("ConnectionStrings__PaymentDb", paymentDbConnectionString)
	.WithEnvironment("WorkloadAuth__ApiKey", workloadApiKey)
	.WithEnvironment("CompanyCredentials__QEC__ApiKey", qecApiKey);

var payableServicesApi = builder.AddProject<Projects.PayableServices_Api>("payable-services-api", launchProfileName: "https")
	.WithReference(identityClientApi)
	.WithReference(paymentSwitchProcessor)
	.WithEnvironment("ConnectionStrings__PayableServicesDb", payableServicesDbConnectionString)
	.WithEnvironment("AuthSettings__Authority", identityClientApi.GetEndpoint("https"))
	.WithEnvironment("PaymentSwitchProcessor__GrpcUrl", paymentSwitchProcessor.GetEndpoint("https"))
	.WithEnvironment("PaymentSwitchProcessor__AllowUntrustedCertificate", "true")
	.WithEnvironment("WorkloadAuth__ApiKey", workloadApiKey);

// Payments API
var paymentsApi = builder.AddProject<Projects.Payments_Api>("payments-api")
	.WithReference(identityClientApi)
	.WithReference(paymentSwitchProcessor)
	.WithEnvironment("AuthSettings__Authority", identityClientApi.GetEndpoint("https"))
	.WithEnvironment("PaymentSwitchProcessor__GrpcUrl", paymentSwitchProcessor.GetEndpoint("https"));

// Payment switch worker
builder.AddProject<Projects.PaymentSwitch_Worker>("payment-switch-worker")
	.WithEnvironment("RabbitMQ__Mode", "FromEnvironment")
	.WithEnvironment("ConnectionStrings__PagQueue", rabbitConnectionString)
	.WithEnvironment("ConnectionStrings__PaymentDb", paymentDbConnectionString);


builder.Build().Run();
