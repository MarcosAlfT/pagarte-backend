using OpenIddict.Validation.AspNetCore;
using Pagarte.Contracts;
using Pagarte.API.GrpcClients;

namespace Pagarte.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;

			builder.Services.AddControllers();

			// gRPC clients call Pagarte.Services.
			var workerUrl = configuration["PagarteServices:GrpcUrl"];

			if (string.IsNullOrWhiteSpace(workerUrl))
			{
				throw new InvalidOperationException("PagarteServices:GrpcUrl is not configured.");
			}

			var allowUntrustedWorkerCertificate =
				builder.Environment.IsDevelopment()
				&& configuration.GetValue<bool>("PagarteServices:AllowUntrustedCertificate");

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
