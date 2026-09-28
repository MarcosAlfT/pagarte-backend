using Microsoft.EntityFrameworkCore;
using ExternalConnections.PaymentOperators.Config;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.UseCases;
using PaymentSwitch.Processor.GrpcServices;
using PaymentSwitch.Processor.Infrastructure;
using PaymentSwitch.Processor.Infrastructure.Gateways;
using PaymentSwitch.Processor.Infrastructure.Repository;
using PaymentSwitch.Processor.Interfaces;
using PaymentSwitch.Processor.Services;
using Infrastructure.RabbitMQ;
using Microsoft.AspNetCore.Authentication;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.Services;
using PaymentSwitch.Processor.Security;
using Utilities.Money;

namespace PaymentSwitch.Processor
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);
			var configuration = builder.Configuration;

			// Database - Processor owns PaymentDb
			builder.Services.AddDbContext<PaymentDbContext>(options =>
				options.UseSqlServer(configuration.GetConnectionString("PaymentDb")));

			// Repositories
			builder.Services.AddScoped<ICreditCardRepository, CreditCardRepository>();
			builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
			builder.Services.AddScoped<IPaymentOperatorRepository, PaymentOperatorRepository>();
			builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
			builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
			builder.Services.AddScoped<IUnitOfWork>(provider =>
				provider.GetRequiredService<PaymentDbContext>());
			builder.Services.AddSingleton<IClock, PaymentSwitch.Messaging.SystemClock>();
			builder.Services.AddSingleton<ICurrencyCatalog, CurrencyCatalog>();
			builder.Services.AddScoped<IMessagePublisher, RabbitMqPublisher>();

			// External connections (payment operator, companies) with Polly resilience
			builder.Services.AddExternalConnections(configuration);

			// Business services
			builder.Services.AddScoped<IPaymentOperatorResolver, PaymentOperatorResolver>();
			builder.Services.AddScoped<ICardPaymentGateway, PaymentOperatorCardPaymentGateway>();
			builder.Services.AddScoped<ICompanyPaymentGateway, CompanyPaymentGateway>();
			builder.Services.AddScoped<
				ICreditCardRegistrationGateway,
				PaymentOperatorCreditCardRegistrationGateway>();
			builder.Services.Configure<PaymentExecutionOptions>(
				configuration.GetSection(PaymentExecutionOptions.SectionName));
			builder.Services.AddScoped<ConfirmedQuoteValidator>();
			builder.Services.AddScoped<AcceptConfirmedQuoteUseCase>();
			builder.Services.AddScoped<ContinuePaymentExecutionUseCase>();
			builder.Services.AddScoped<GetPaymentExecutionStatusUseCase>();
			builder.Services.AddScoped<RegisterCreditCardUseCase>();
			builder.Services.AddScoped<UpdateCreditCardUseCase>();
			builder.Services.AddScoped<DeleteCreditCardUseCase>();
			builder.Services.AddScoped<GetServiceCatalogUseCase>();
			builder.Services.AddScoped<GetServiceUseCase>();
			builder.Services.AddScoped<GetCreditCardsUseCase>();
			builder.Services.AddScoped<GetCreditCardUseCase>();
			builder.Services.AddScoped<GetPaymentUseCase>();
			builder.Services.AddScoped<GetPaymentHistoryUseCase>();
			builder.Services.AddScoped<PublishPendingOutboxMessagesUseCase>();
			builder.Services.AddHostedService<OutboxPublisherService>();
			builder.Services.AddHostedService<PaymentSagaDispatcherService>();

			builder.Services.AddAuthentication(WorkloadApiKeyAuthenticationHandler.SchemeName)
				.AddScheme<AuthenticationSchemeOptions, WorkloadApiKeyAuthenticationHandler>(
					WorkloadApiKeyAuthenticationHandler.SchemeName,
					_ => { });
			builder.Services.AddAuthorization();

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
				var clock = scope.ServiceProvider.GetRequiredService<IClock>();
				await db.Database.MigrateAsync();
				await PaymentDbSeeder.SeedAsync(db, configuration, clock);

				logger.LogInformation("Payment database migrations and seed data completed.");
			}

			app.UseAuthentication();
			app.UseAuthorization();

			// The execution endpoint additionally requires workload authentication.
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
