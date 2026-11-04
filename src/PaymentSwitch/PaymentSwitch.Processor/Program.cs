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
			var app = await BuildAsync(args);
			var logger = app.Services.GetRequiredService<ILogger<Program>>();
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

			await app.RunAsync();
		}

		public static async Task<WebApplication> BuildAsync(string[] args, Action<WebApplicationBuilder>? configure = null)
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
			builder.Services.AddOptions<CardRegistrationOptions>().Bind(configuration.GetSection("PaymentMethods"))
				.Validate(value => !string.IsNullOrWhiteSpace(value.ConsentVersion) && value.ConsentVersion.Length <= 100,
					"Configure a saved-card consent version of 1 to 100 characters.").ValidateOnStart();
			builder.Services.AddDataProtection();
			builder.Services.AddScoped<ICardRegistrationRepository, CardRegistrationRepository>();
			builder.Services.AddScoped<ICardDetachmentRepository, CardDetachmentRepository>();
			builder.Services.AddScoped<IPaymentOperatorRouteRepository, PaymentOperatorRouteRepository>();
			builder.Services.AddScoped<PinPaymentOperatorRoutesUseCase>();
			builder.Services.AddScoped<IPaymentOperatorRouteConfiguration, PaymentOperatorRouteConfiguration>();
			builder.Services.AddScoped<DetachCreditCardUseCase>();
			builder.Services.AddScoped<ICardReviewRepository, CardReviewRepository>();
			builder.Services.AddScoped<ReviewCardOperationUseCase>();
			builder.Services.AddScoped<VerifiedCardPersistenceService>();
			builder.Services.AddScoped<GetCardRemovalUseCase>();
			builder.Services.AddScoped<ICardMutationScope, CardMutationScope>();
			builder.Services.AddScoped<IRegistrationTokenProtector, RegistrationTokenProtector>();
			builder.Services.AddScoped<StartCardRegistrationUseCase>();
			builder.Services.AddScoped<CompleteCardRegistrationUseCase>();
			builder.Services.AddScoped<ContinueCardRegistrationUseCase>();
			builder.Services.AddScoped<ReconcileTerminatedCardRegistrationUseCase>();
			builder.Services.AddScoped<GetCardRegistrationUseCase>();
			builder.Services.AddScoped<ReceiveCardSetupNotificationUseCase>();
			builder.Services.AddHostedService<CardRegistrationDispatcherService>();
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
			builder.Services.AddAuthorization(options => options.AddPolicy("CardOperationsWorkload", policy => policy.RequireAuthenticatedUser().RequireClaim("card-operations", "true")));

			// gRPC server
			builder.Services.AddGrpc();

			// RabbitMQ publisher
			builder.Services.AddRabbitMq(builder.Configuration);

			configure?.Invoke(builder);
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
				await scope.ServiceProvider.GetRequiredService<PinPaymentOperatorRoutesUseCase>().ExecuteAsync();

				logger.LogInformation("Payment database migrations and seed data completed.");
			}

			app.UseAuthentication();
			app.UseAuthorization();

			// Stored-card and execution boundaries require workload authentication.
			app.MapGrpcService<CreditCardGrpcService>().RequireAuthorization();
			app.MapGrpcService<CardOperationsGrpcService>().RequireAuthorization("CardOperationsWorkload");
			app.MapGrpcService<PaymentGrpcService>();
			app.MapGrpcService<PaymentExecutionGrpcService>();
			app.MapGrpcService<ServiceCatalogGrpcService>();

			return app;
		}
	}
}
