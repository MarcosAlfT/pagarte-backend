using Pagarte.Security;
using PaymentSwitch.Contracts;
using Payments.Api.GrpcClients;

namespace Payments.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;

			builder.Services.AddControllers();

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

			builder.Services.AddPagarteJwtSecurity(configuration, builder.Environment);
			builder.Services.AddClientIdentityResolution(configuration, builder.Environment);
            builder.Services.AddOpenApi();

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

			app.Run();
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
