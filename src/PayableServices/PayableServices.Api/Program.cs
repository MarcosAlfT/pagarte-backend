using Microsoft.EntityFrameworkCore;
using PayableServices.Infrastructure;
using PayableServices.Persistence;
using Pagarte.Security;
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

		builder.Services.AddPagarteJwtSecurity(builder.Configuration, builder.Environment);
		builder.Services.AddClientIdentityResolution(builder.Configuration, builder.Environment);

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
		app.UseRouting();
		app.UseAuthentication();
		app.UseAuthorization();
		app.UseClientIdentityResolution();
		app.MapControllers();
		app.Run();
	}
}
