using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayableServices.Application.Abstractions;
using PayableServices.Persistence.Repositories;

namespace PayableServices.Persistence;

public static class DependencyInjection
{
	public static IServiceCollection AddPayableServicesPersistence(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddDbContext<PayableServicesDbContext>(options =>
			options.UseSqlServer(configuration.GetConnectionString("PayableServicesDb")));

		services.AddScoped<IQuoteRepository, QuoteRepository>();
		services.AddScoped<IPayableServiceRepository, PayableServiceRepository>();
		services.AddScoped<IExternalCatalogueSourceRepository, ExternalCatalogueSourceRepository>();
		services.AddScoped<IExternalCatalogueItemRepository, ExternalCatalogueItemRepository>();
		services.AddScoped<IExternalCatalogueMappingRepository, ExternalCatalogueMappingRepository>();
		services.AddScoped<IPaymentRouteRepository, PaymentRouteRepository>();

		return services;
	}
}
