using OpenIddict.Validation.AspNetCore;
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

			// OpenIddict validation
			builder.Services.AddOpenIddict()
                .AddValidation(options =>
                {
                    var strAuthority = configuration.GetValue<string>("AuthSettings:Authority")
                        ?? throw new InvalidOperationException("AuthSettings:Authority is not configured.");
                    var strAudience = configuration.GetValue<string>("AuthSettings:Audience")
                        ?? throw new InvalidOperationException("AuthSettings:Audience is not configured.");

                    options.SetIssuer(strAuthority);
                    options.AddAudiences(strAudience);
                    options.UseSystemNetHttp();
                    options.UseAspNetCore();
                });


			builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            });


			builder.Services.AddAuthorization();
            builder.Services.AddOpenApi();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
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
