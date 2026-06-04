using Microsoft.EntityFrameworkCore;
using PayableServices.Infrastructure;
using PayableServices.Persistence;

namespace PayableServices.Api;

public static class Program
{
	public static async Task Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		builder.Services.AddControllers();
		builder.Services.AddOpenApi();
		builder.Services.AddPayableServicesInfrastructure(builder.Configuration);

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
		app.MapControllers();
		app.Run();
	}
}
