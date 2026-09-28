using Microsoft.EntityFrameworkCore;
using PayableServices.Infrastructure;
using PayableServices.Persistence;
using OpenIddict.Validation.AspNetCore;
using PayableServices.Api.Security;
using PayableServices.Application.Abstractions;

namespace PayableServices.Api;

public static class Program
{
	public static async Task Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		builder.Services.AddControllers();
		builder.Services.AddOpenApi();
		builder.Services.AddPayableServicesInfrastructure(builder.Configuration);
		builder.Services.AddHttpContextAccessor();
		builder.Services.AddScoped<ICurrentActorProvider, HttpCurrentActorProvider>();

		builder.Services.AddOpenIddict()
			.AddValidation(options =>
			{
				var authority = builder.Configuration["AuthSettings:Authority"]
					?? throw new InvalidOperationException("AuthSettings:Authority is not configured.");
				var audience = builder.Configuration["AuthSettings:Audience"]
					?? throw new InvalidOperationException("AuthSettings:Audience is not configured.");
				options.SetIssuer(authority);
				options.AddAudiences(audience);
				options.UseSystemNetHttp();
				options.UseAspNetCore();
			});
		builder.Services.AddAuthentication(options =>
		{
			options.DefaultAuthenticateScheme =
				OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
			options.DefaultChallengeScheme =
				OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
		});
		builder.Services.AddAuthorization();

		var app = builder.Build();

		using (var scope = app.Services.CreateScope())
		{
			var dbContext = scope.ServiceProvider.GetRequiredService<PayableServicesDbContext>();
			await dbContext.Database.MigrateAsync();
			await PayableServicesDbSeeder.SeedAsync(dbContext);
		}

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
}
