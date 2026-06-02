using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PaymentServices.Persistence.DesignTime;

public sealed class PaymentServicesDbContextFactory
	: IDesignTimeDbContextFactory<PaymentServicesDbContext>
{
	public PaymentServicesDbContext CreateDbContext(string[] args)
	{
		var optionsBuilder = new DbContextOptionsBuilder<PaymentServicesDbContext>();
		optionsBuilder.UseSqlServer(
			"Server=(localdb)\\MSSQLLocalDB;Database=PaymentServicesDb;Trusted_Connection=True;TrustServerCertificate=True");

		return new PaymentServicesDbContext(optionsBuilder.Options);
	}
}
