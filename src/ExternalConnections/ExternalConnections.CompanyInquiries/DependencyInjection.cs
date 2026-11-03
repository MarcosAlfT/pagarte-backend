using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PayableServices.Application.Abstractions;

namespace ExternalConnections.CompanyInquiries;

public static class DependencyInjection
{
    public static IServiceCollection AddCompanyInquiries(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() && configuration.GetValue<bool>("CompanyProviders:MockCompany:Enabled"))
            throw new InvalidOperationException("Mock company is only available in development.");
        services.AddHttpClient("CompanyInquiry", client => client.Timeout = TimeSpan.FromSeconds(5))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<CompanyInquiryAdapter>();
        services.AddScoped<IAmountInquiryProvider>(sp => sp.GetRequiredService<CompanyInquiryAdapter>());
        services.AddScoped<ICompanyCatalogueProvider>(sp => sp.GetRequiredService<CompanyInquiryAdapter>());
        services.AddScoped<ICompanyRouteConfiguration>(sp => sp.GetRequiredService<CompanyInquiryAdapter>());
        return services;
    }
}
