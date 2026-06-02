using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentServices.Application.Abstractions;
using PaymentServices.Persistence.Repositories;

namespace PaymentServices.Persistence;

public static class DependencyInjection
{
	public static IServiceCollection AddPaymentServicesPersistence(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddDbContext<PaymentServicesDbContext>(options =>
			options.UseSqlServer(configuration.GetConnectionString("PaymentServicesDb")));

		services.AddScoped<IQuoteRepository, QuoteRepository>();
		services.AddScoped<IPayableServiceRepository, PayableServiceRepository>();
		services.AddScoped<IExternalCatalogueSourceRepository, ExternalCatalogueSourceRepository>();
		services.AddScoped<IExternalCatalogueItemRepository, ExternalCatalogueItemRepository>();
		services.AddScoped<IExternalCatalogueMappingRepository, ExternalCatalogueMappingRepository>();
		services.AddScoped<IPaymentRouteRepository, PaymentRouteRepository>();

		return services;
	}
}
