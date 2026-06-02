using Microsoft.EntityFrameworkCore;
using PaymentServices.Domain.Entities;

namespace PaymentServices.Persistence;

public static class PaymentServicesDbSeeder
{
	public static async Task SeedAsync(
		PaymentServicesDbContext dbContext,
		CancellationToken cancellationToken = default)
	{
		if (await dbContext.Countries.AnyAsync(cancellationToken))
		{
			return;
		}

		var country = new Country
		{
			Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
			Code = "EC",
			Name = "Ecuador",
			Currency = "USD",
			IsActive = true,
			DisplayOrder = 1
		};

		var category = new Category
		{
			Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
			CountryId = country.Id,
			Name = "Utilities",
			Description = "Basic utilities",
			IsActive = true,
			DisplayOrder = 1,
			SearchKeywords = "electricity,water,gas"
		};

		var subcategory = new Subcategory
		{
			Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
			CategoryId = category.Id,
			Name = "Electricity",
			Description = "Electricity services",
			IsActive = true,
			DisplayOrder = 1,
			SearchKeywords = "light,power"
		};

		var provider = new Provider
		{
			Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
			CountryId = country.Id,
			Name = "Quito Electricity Company",
			IsActive = true
		};

		var service = new PayableService
		{
			Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
			CountryId = country.Id,
			CategoryId = category.Id,
			SubcategoryId = subcategory.Id,
			ProviderId = provider.Id,
			Name = "Electricity - Quito",
			Description = "Pay your electricity bill in Quito",
			Status = "Active",
			IsActive = true,
			DisplayOrder = 1,
			SearchKeywords = "electricity,quito,utility",
			Currency = country.Currency,
			BaseAmount = 25.00m,
			AllowsQuote = true,
			AllowsPayment = true
		};

		var route = new PaymentRoute
		{
			Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
			PayableServiceId = service.Id,
			RouteType = "DirectProviderApi",
			ProviderId = provider.Id,
			ExternalSourceCode = "QEC-API",
			ExternalServiceCode = "EEQ001",
			Status = "Active",
			IsActive = true,
			LastTestedAt = DateTime.UtcNow
		};

		var referenceField = new ReferenceFieldDefinition
		{
			Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
			PayableServiceId = service.Id,
			FieldKey = "accountNumber",
			DisplayLabel = "Account Number",
			DataType = "string",
			IsRequired = true,
			MinLength = 8,
			MaxLength = 20,
			ValidationRule = @"^\d+$",
			DisplayOrder = 1
		};

		var composition = new AmountComposition
		{
			Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
			PayableServiceId = service.Id
		};

		composition.Components.Add(new AmountComponent
		{
			Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
			AmountCompositionId = composition.Id,
			ComponentType = "ServiceAmount",
			Description = "Base service amount",
			Amount = service.BaseAmount,
			Currency = "USD",
			Source = "Platform",
			AppliesTo = "Quote",
			IsRequired = true,
			IsVisibleToClient = true,
			DisplayOrder = 1
		});

		await dbContext.AddRangeAsync(
			country,
			category,
			subcategory,
			provider,
			service,
			route,
			referenceField,
			composition,
			cancellationToken);

		await dbContext.SaveChangesAsync(cancellationToken);
	}
}
