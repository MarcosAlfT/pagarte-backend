using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PayableServices.Persistence.DesignTime;

public sealed class PayableServicesDbContextFactory
	: IDesignTimeDbContextFactory<PayableServicesDbContext>
{
	public PayableServicesDbContext CreateDbContext(string[] args)
	{
		var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PayableServicesDb");
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new InvalidOperationException("ConnectionStrings__PayableServicesDb is not configured.");
		}

		var optionsBuilder = new DbContextOptionsBuilder<PayableServicesDbContext>();
		optionsBuilder.UseSqlServer(connectionString);

		return new PayableServicesDbContext(optionsBuilder.Options);
	}
}
