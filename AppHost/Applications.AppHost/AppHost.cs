var builder = DistributedApplication.CreateBuilder(args);

var rabbitConnectionString = builder.AddParameter("rabbit-connection-string", secret: true);

// Client Identity Service
var identity = builder.AddProject<Projects.ClientIdentity_Api>("client-identity", launchProfileName: "https");

// Clients API
var clientsApi = builder.AddProject<Projects.Clients_API>("clients", launchProfileName: "https")
	.WithReference(identity)
	.WithEnvironment("AuthSettings__Authority", identity.GetEndpoint("https"));

// Pagarte Services
var pagarteServices = builder.AddProject<Projects.Pagarte_Services>("pagarte-services", launchProfileName: "Pagarte.Services")
	.WithEnvironment("RabbitMQ__Mode", "FromEnvironment")
	.WithEnvironment("ConnectionStrings__PagQueue", rabbitConnectionString);

// Pagarte API
var pagarteApi = builder.AddProject<Projects.Pagarte_API>("pagarte-api")
	.WithReference(identity)
	.WithReference(pagarteServices)
	.WithEnvironment("AuthSettings__Authority", identity.GetEndpoint("https"))
	.WithEnvironment("PagarteServices__GrpcUrl", pagarteServices.GetEndpoint("https"));

// Pagarte Engine
builder.AddProject<Projects.Pagarte_Engine>("pagarte-engine")
	.WithEnvironment("RabbitMQ__Mode", "FromEnvironment")
	.WithEnvironment("ConnectionStrings__PagQueue", rabbitConnectionString);


builder.Build().Run();
