using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PaymentSwitch.Processor.Infrastructure.DesignTime;

public sealed class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
	public PaymentDbContext CreateDbContext(string[] args)
	{
		var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PaymentDb");
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new InvalidOperationException("ConnectionStrings__PaymentDb is not configured.");
		}

		var options = new DbContextOptionsBuilder<PaymentDbContext>()
			.UseSqlServer(connectionString)
			.Options;
		return new PaymentDbContext(options);
	}
}
