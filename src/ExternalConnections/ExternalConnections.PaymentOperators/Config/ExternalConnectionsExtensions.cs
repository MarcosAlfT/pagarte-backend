using ExternalConnections.CompanyPayments;
using ExternalConnections.PaymentOperators.PaymentOperators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExternalConnections.PaymentOperators.Config;

public static class ExternalConnectionsExtensions
{
    public static IServiceCollection AddExternalConnections(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient<CompanyAdapter>()
            .AddStandardResilienceHandler();

        services.AddHttpClient("payment-operator", client =>
        {
            var apiUrl = configuration["PaymentOperator:ApiUrl"];
            if (!string.IsNullOrWhiteSpace(apiUrl))
            {
                client.BaseAddress = new Uri(apiUrl);
            }

            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddStandardResilienceHandler();

        services.AddScoped<MockPaymentOperatorAdapter>();
        services.AddScoped<DLocalPaymentOperatorAdapter>();
        services.AddScoped<IPaymentOperatorAdapterFactory, PaymentOperatorAdapterFactory>();
        services.AddScoped<ICompanyAdapter, CompanyAdapter>();

        return services;
    }
}
