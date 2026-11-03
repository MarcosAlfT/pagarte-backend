using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ExternalConnections.CompanyInquiries;
using Microsoft.Extensions.DependencyInjection;
using PayableServices.Application.Abstractions;
using PayableServices.Application.UseCases;
using PayableServices.Infrastructure.Clients;
using PayableServices.Persistence;
using PaymentSwitch.Contracts;
using Utilities.Money;

namespace PayableServices.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPayableServicesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var grpcUrl = configuration["PaymentSwitchProcessor:GrpcUrl"];
        if (string.IsNullOrWhiteSpace(grpcUrl))
        {
            throw new InvalidOperationException("PaymentSwitchProcessor:GrpcUrl is not configured.");
        }

        var allowUntrustedWorkerCertificate =
            configuration.GetValue<bool>("PaymentSwitchProcessor:AllowUntrustedCertificate");

        services.AddGrpcClient<PaymentExecutionService.PaymentExecutionServiceClient>(
                o => o.Address = new Uri(grpcUrl))
            .ConfigurePrimaryHttpMessageHandler(() => CreateGrpcHttpHandler(
                allowUntrustedWorkerCertificate));

        services.AddCompanyInquiries(configuration, environment);
        services.AddHostedService<InquiryRecoveryService>();
        services.AddScoped<IPaymentExecutionClient, PaymentSwitchExecutionClient>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ICurrencyCatalog, CurrencyCatalog>();
        services.AddPayableServicesPersistence(configuration);

        services.AddScoped<GetCatalogueUseCase>();
        services.AddScoped<GetServiceDetailsUseCase>();
        services.AddScoped<CreateInquiryUseCase>();
        services.AddScoped<GetInquiryUseCase>();
        services.AddScoped<CreateQuoteUseCase>();
        services.AddScoped<ConfirmQuoteUseCase>();
        services.AddScoped<GetQuoteUseCase>();
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
