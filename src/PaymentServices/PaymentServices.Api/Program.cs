using Microsoft.EntityFrameworkCore;
using PaymentServices.Infrastructure;
using PaymentServices.Persistence;

namespace PaymentServices.Api;

public static class Program
{
	public static async Task Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		builder.Services.AddControllers();
		builder.Services.AddOpenApi();
		builder.Services.AddPaymentServicesInfrastructure(builder.Configuration);

		var app = builder.Build();

		using (var scope = app.Services.CreateScope())
		{
			var dbContext = scope.ServiceProvider.GetRequiredService<PaymentServicesDbContext>();
			await dbContext.Database.MigrateAsync();
			await PaymentServicesDbSeeder.SeedAsync(dbContext);
		}

		if (app.Environment.IsDevelopment())
		{
			app.MapOpenApi();
		}

		app.UseHttpsRedirection();
		app.MapControllers();
		app.Run();
	}
}
