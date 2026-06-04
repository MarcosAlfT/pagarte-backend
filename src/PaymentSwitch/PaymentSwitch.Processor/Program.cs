using Microsoft.EntityFrameworkCore;
using ExternalConnections.PaymentOperators.Config;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.GrpcServices;
using PaymentSwitch.Processor.Infrastructure;
using PaymentSwitch.Processor.Infrastructure.Repository;
using PaymentSwitch.Processor.Interfaces;
using PaymentSwitch.Processor.Services;
using Infrastructure.RabbitMQ;

namespace PaymentSwitch.Processor
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);
			var configuration = builder.Configuration;

			// Database - Worker owns PaymentDb
			builder.Services.AddDbContext<PaymentDbContext>(options =>
				options.UseSqlServer(configuration.GetConnectionString("PaymentDb")));

			// Repositories
			builder.Services.AddScoped<ICreditCardRepository, CreditCardRepository>();
			builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
			builder.Services.AddScoped<IPaymentQuoteRepository, PaymentQuoteRepository>();
			builder.Services.AddScoped<IPaymentOperatorRepository, PaymentOperatorRepository>();
			builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
			builder.Services.AddScoped<IFeeConfigurationRepository, FeeConfigurationRepository>();
			builder.Services.AddScoped<IMessagePublisher, RabbitMqPublisher>();

			// External connections (payment operator, companies) with Polly resilience
			builder.Services.AddExternalConnections(configuration);

			// Business services
			builder.Services.AddScoped<IPaymentOperatorResolver, PaymentOperatorResolver>();
			builder.Services.AddScoped<PaymentEngineService>();
			builder.Services.AddHostedService<OutboxPublisherService>();

			// gRPC server
			builder.Services.AddGrpc();

			// RabbitMQ publisher
			builder.Services.AddRabbitMq(builder.Configuration);

			var app = builder.Build();
			var logger = app.Services.GetRequiredService<ILogger<Program>>();

			// Migrations and seed data
			using (var scope = app.Services.CreateScope())
			{
				logger.LogInformation("Applying Payment database migrations.");

				var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
				await db.Database.MigrateAsync();
				await PaymentDbSeeder.SeedAsync(db, configuration);

				logger.LogInformation("Payment database migrations and seed data completed.");
			}

			// gRPC endpoints - only accessible from private subnet
			app.MapGrpcService<CreditCardGrpcService>();
			app.MapGrpcService<PaymentGrpcService>();
			app.MapGrpcService<PaymentExecutionGrpcService>();
			app.MapGrpcService<ServiceCatalogGrpcService>();

			app.Lifetime.ApplicationStarted.Register(() =>
			{
				_ = Task.Run(async () =>
				{
					try
					{
						using var scope = app.Services.CreateScope();
						var rabbitFactory = scope.ServiceProvider.GetRequiredService<RabbitMqConnectionFactory>();
						var connection = await rabbitFactory.GetConnectionAsync();
						using var channel = await connection.CreateChannelAsync();
						await PaymentSwitchTopology.DeclareAllAsync(channel);
					}
					catch (Exception ex)
					{
						logger.LogError(ex, "Failed to declare payment switch RabbitMQ topology.");
					}
				});
			});

			app.Run();
		}
	}
}
