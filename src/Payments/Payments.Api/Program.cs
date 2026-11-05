using Pagarte.Security;
using PaymentSwitch.Contracts;
using Payments.Api.GrpcClients;

namespace Payments.Api
{
    public class Program
    {
        public static void Main(string[] args) => Build(args).Run();

        public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;

			builder.Services.AddControllers(options => options.Filters.Add<Payments.Api.Controllers.CardRpcExceptionFilter>())
                .AddApplicationPart(typeof(Payments.Api.Controllers.CreditCardController).Assembly)
                .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
                    new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(Utilities.Responses.ApiResponse.CreateFailure(
                        context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor { ControllerName: "PaymentRecovery" }
                            ? "INVALID_RECOVERY_REQUEST" : "INVALID_CARD_REQUEST")));

			// gRPC clients call PaymentSwitch.Processor.
			var workerUrl = configuration["PaymentSwitchProcessor:GrpcUrl"];

			if (string.IsNullOrWhiteSpace(workerUrl))
			{
				throw new InvalidOperationException("PaymentSwitchProcessor:GrpcUrl is not configured.");
			}

			var allowUntrustedWorkerCertificate =
				builder.Environment.IsDevelopment()
				&& configuration.GetValue<bool>("PaymentSwitchProcessor:AllowUntrustedCertificate");

			builder.Services.AddGrpcClient<CreditCardService.CreditCardServiceClient>(
				o => o.Address = new Uri(workerUrl))
				.ConfigurePrimaryHttpMessageHandler(() => CreateGrpcHttpHandler(allowUntrustedWorkerCertificate));
			builder.Services.AddScoped<CreditCardGrpcClient>();
			builder.Services.AddGrpcClient<CardOperationsService.CardOperationsServiceClient>(o => o.Address = new Uri(workerUrl))
				.ConfigurePrimaryHttpMessageHandler(() => CreateGrpcHttpHandler(allowUntrustedWorkerCertificate));
			builder.Services.AddScoped<CardOperationsGrpcClient>();
			builder.Services.AddGrpcClient<PaymentRecoveryService.PaymentRecoveryServiceClient>(o => o.Address = new Uri(workerUrl))
				.ConfigurePrimaryHttpMessageHandler(() => CreateGrpcHttpHandler(allowUntrustedWorkerCertificate));
			builder.Services.AddScoped<PaymentRecoveryGrpcClient>();

			builder.Services.AddPagarteJwtSecurity(configuration, builder.Environment);
			builder.Services.AddClientIdentityResolution(configuration, builder.Environment);
            builder.Services.AddOpenApi();

            configure?.Invoke(builder);
            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();
			app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
			app.UseClientIdentityResolution();
            app.MapControllers();

			return app;
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
}
