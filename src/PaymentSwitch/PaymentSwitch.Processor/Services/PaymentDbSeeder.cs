using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Domain.Enums;
using PaymentSwitch.Processor.Infrastructure;

namespace PaymentSwitch.Processor.Services
{
	public static class PaymentDbSeeder
	{
		public static async Task SeedAsync(PaymentDbContext context, IConfiguration configuration)
		{
			await SeedPaymentOperatorsAsync(context, configuration);
			await SeedCompaniesAsync(context);
			await SeedServicesAsync(context);
			await SeedFeeConfigurationsAsync(context);
		}

		private static async Task SeedPaymentOperatorsAsync(
			PaymentDbContext context, IConfiguration configuration)
		{
			if (await context.PaymentOperators.AnyAsync()) return;

			var provider = configuration["PaymentOperator:Provider"];
			if (string.IsNullOrWhiteSpace(provider))
			{
				throw new InvalidOperationException("PaymentOperator:Provider is not configured.");
			}

			context.PaymentOperators.Add(PaymentOperator.Create(
				provider,
				configuration["PaymentOperator:Name"] ?? provider,
				PaymentOperatorScope.International,
				priority: 100));

			await context.SaveChangesAsync();
		}

		private static async Task SeedCompaniesAsync(PaymentDbContext context)
		{
			if (await context.Companies.IgnoreQueryFilters().AnyAsync()) return;

			context.Companies.AddRange(
				new Company { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "City Water Company", ApiEndpoint = "https://api.citywater.com/payments", ApiKey = "water-key-encrypted", IsActive = true },
				new Company { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "National Electric", ApiEndpoint = "https://api.nationalelectric.com/payments", ApiKey = "electric-key-encrypted", IsActive = true },
				new Company { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Internet Provider Co", ApiEndpoint = "https://api.internet.com/payments", ApiKey = "internet-key-encrypted", IsActive = true },
				new Company { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "Gas Utility Corp", ApiEndpoint = "https://api.gasutility.com/payments", ApiKey = "gas-key-encrypted", IsActive = true }
			);
			await context.SaveChangesAsync();
		}

		private static async Task SeedServicesAsync(PaymentDbContext context)
		{
			if (await context.Services.IgnoreQueryFilters().AnyAsync()) return;

			context.Services.AddRange(
				new Service { Id = Guid.NewGuid(), Name = "Water Bill", Description = "Monthly water service", Category = "Utilities", CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111"), BaseAmount = 0, Currency = "USD", IsActive = true },
				new Service { Id = Guid.NewGuid(), Name = "Electricity Bill", Description = "Monthly electricity service", Category = "Utilities", CompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222"), BaseAmount = 0, Currency = "USD", IsActive = true },
				new Service { Id = Guid.NewGuid(), Name = "Internet Basic", Description = "100Mbps plan", Category = "Telecom", CompanyId = Guid.Parse("33333333-3333-3333-3333-333333333333"), BaseAmount = 49.99m, Currency = "USD", IsActive = true },
				new Service { Id = Guid.NewGuid(), Name = "Internet Premium", Description = "500Mbps plan", Category = "Telecom", CompanyId = Guid.Parse("33333333-3333-3333-3333-333333333333"), BaseAmount = 89.99m, Currency = "USD", IsActive = true },
				new Service { Id = Guid.NewGuid(), Name = "Gas Bill", Description = "Monthly gas service", Category = "Utilities", CompanyId = Guid.Parse("44444444-4444-4444-4444-444444444444"), BaseAmount = 0, Currency = "USD", IsActive = true }
			);
			await context.SaveChangesAsync();
		}

		private static async Task SeedFeeConfigurationsAsync(PaymentDbContext context)
		{
			if (await context.FeeConfigurations.IgnoreQueryFilters().AnyAsync()) return;

			context.FeeConfigurations.AddRange(
				new FeeConfiguration { Id = Guid.NewGuid(), Type = FeeType.PaymentOperator, CalculationType = CalculationType.Percentage, Value = 2.9m, Currency = "USD", IsActive = true, EffectiveDate = DateTime.UtcNow.AddYears(-1) },
				new FeeConfiguration { Id = Guid.NewGuid(), Type = FeeType.Platform, CalculationType = CalculationType.Percentage, Value = 1.5m, Currency = "USD", IsActive = true, EffectiveDate = DateTime.UtcNow.AddYears(-1) },
				new FeeConfiguration { Id = Guid.NewGuid(), Type = FeeType.Company, CalculationType = CalculationType.FixedAmount, Value = 0.30m, Currency = "USD", IsActive = true, EffectiveDate = DateTime.UtcNow.AddYears(-1) }
			);
			await context.SaveChangesAsync();
		}
	}
}
