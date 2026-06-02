using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentServices.Application.Abstractions;
using PaymentServices.Application.UseCases;
using PaymentServices.Infrastructure.Clients;
using PaymentServices.Persistence;
using Pagarte.Contracts;

namespace PaymentServices.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddPaymentServicesInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var grpcUrl = configuration["PagarteServices:GrpcUrl"];
		if (string.IsNullOrWhiteSpace(grpcUrl))
		{
			throw new InvalidOperationException("PagarteServices:GrpcUrl is not configured.");
		}

		var allowUntrustedWorkerCertificate =
			configuration.GetValue<bool>("PagarteServices:AllowUntrustedCertificate");

		services.AddGrpcClient<ServiceCatalogService.ServiceCatalogServiceClient>(
				o => o.Address = new Uri(grpcUrl))
			.ConfigurePrimaryHttpMessageHandler(() => CreateGrpcHttpHandler(
				allowUntrustedWorkerCertificate));

		services.AddGrpcClient<PaymentExecutionService.PaymentExecutionServiceClient>(
				o => o.Address = new Uri(grpcUrl))
			.ConfigurePrimaryHttpMessageHandler(() => CreateGrpcHttpHandler(
				allowUntrustedWorkerCertificate));

		services.AddScoped<ICompanyPaymentsClient, PagarteCompanyPaymentsClient>();
		services.AddScoped<IPaymentExecutionClient, PagartePaymentExecutionClient>();
		services.AddSingleton<IClock, SystemClock>();
		services.AddPaymentServicesPersistence(configuration);

		services.AddScoped<GetCatalogueUseCase>();
		services.AddScoped<CreateQuoteUseCase>();
		services.AddScoped<ConfirmQuoteUseCase>();
		services.AddScoped<SyncExternalCatalogueUseCase>();
		services.AddScoped<ActivatePaymentRouteUseCase>();

		return services;
	}

	private static HttpMessageHandler CreateGrpcHttpHandler(bool allowUntrustedCertificate)
	{
		var handler = new HttpClientHandler();

		if (allowUntrustedCertificate)
		{
			handler.ServerCertificateCustomValidationCallback =
				HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
		}

		return handler;
	}
}
