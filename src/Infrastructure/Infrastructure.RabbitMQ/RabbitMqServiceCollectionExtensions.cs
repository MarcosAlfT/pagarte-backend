using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.RabbitMQ;

public static class RabbitMqServiceCollectionExtensions
{
	public static IServiceCollection AddRabbitMq(
		this IServiceCollection services,
		IConfiguration config)
	{
		services.Configure<RabbitMqConnectionOptions>(
			config.GetSection("RabbitMQ"));

		services.AddSingleton<RabbitMqConnectionFactory>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<RabbitMqConnectionOptions>>().Value;

			// Aspire case
			if (options.Mode == RabbitMqConnectionMode.FromEnvironment)
			{
				options.ConnectionString = config.GetConnectionString("PagQueue");
			}

			return new RabbitMqConnectionFactory(options);
		});

		return services;
	}
}