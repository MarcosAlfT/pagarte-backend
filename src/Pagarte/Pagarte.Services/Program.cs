using Microsoft.EntityFrameworkCore;
using ExternalConnections.CardOperators.Config;
using Pagarte.Messaging;
using Pagarte.Services.GrpcServices;
using Pagarte.Services.Infrastructure;
using Pagarte.Services.Infrastructure.Repository;
using Pagarte.Services.Interfaces;
using Pagarte.Services.Services;
using Shared.RabbitMQ;

namespace Pagarte.Services
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);
			var configuration = builder.Configuration;

			// Database - Worker owns PagarteDb
			builder.Services.AddDbContext<PagarteDbContext>(options =>
				options.UseSqlServer(configuration.GetConnectionString("PagarteDb")));

			// Repositories
			builder.Services.AddScoped<ICreditCardRepository, CreditCardRepository>();
			builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
			builder.Services.AddScoped<IPaymentQuoteRepository, PaymentQuoteRepository>();
			builder.Services.AddScoped<IPaymentOperatorRepository, PaymentOperatorRepository>();
			builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
			builder.Services.AddScoped<IFeeConfigurationRepository, FeeConfigurationRepository>();
			builder.Services.AddScoped<IMessagePublisher, RabbitMQPublisher>();

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
				logger.LogInformation("Applying Pagarte database migrations.");

				var db = scope.ServiceProvider.GetRequiredService<PagarteDbContext>();
				await db.Database.MigrateAsync();
				await PagarteDbSeeder.SeedAsync(db, configuration);

				logger.LogInformation("Pagarte database migrations and seed data completed.");
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
						var rabbitFactory = scope.ServiceProvider.GetRequiredService<RabbitMQConnectionFactory>();
						var connection = await rabbitFactory.GetConnectionAsync();
						using var channel = await connection.CreateChannelAsync();
						await PagarteTopology.DeclareAllAsync(channel);
					}
					catch (Exception ex)
					{
						logger.LogError(ex, "Failed to declare Pagarte RabbitMQ topology.");
					}
				});
			});

			app.Run();
		}
	}
}
