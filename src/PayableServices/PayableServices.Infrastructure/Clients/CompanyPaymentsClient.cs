using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PaymentSwitch.Contracts;
using Utilities.Money;

namespace PayableServices.Infrastructure.Clients;

public sealed class CompanyPaymentsClient(
	ServiceCatalogService.ServiceCatalogServiceClient serviceCatalogClient,
	ICurrencyCatalog currencyCatalog)
	: ICompanyPaymentsClient
{
	private readonly ServiceCatalogService.ServiceCatalogServiceClient _serviceCatalogClient = serviceCatalogClient;

	public async Task<CatalogueResponse> GetCatalogueAsync(
		string? category = null,
		CancellationToken cancellationToken = default)
	{
		var response = await _serviceCatalogClient.GetServicesAsync(
			new GetServicesRequest { Category = category ?? string.Empty });

		var services = response.Services.Select(service => new CatalogueItemDto(
			Guid.Parse(service.Id),
			service.Name,
			service.Description,
			service.Category,
			MoneyValue.FromMinorUnits(
				service.BaseAmountMoney.MinorUnits,
				service.BaseAmountMoney.CurrencyCode,
				currencyCatalog).Amount,
			service.BaseAmountMoney.CurrencyCode)).ToArray();

		return new CatalogueResponse(services);
	}

	public async Task<CatalogueItemDto?> GetServiceAsync(
		Guid serviceId,
		CancellationToken cancellationToken = default)
	{
		var response = await _serviceCatalogClient.GetServiceAsync(
			new GetServiceRequest { ServiceId = serviceId.ToString() });

		if (!response.Found || response.Service is null)
		{
			return null;
		}

		var service = response.Service;
		return new CatalogueItemDto(
			Guid.Parse(service.Id),
			service.Name,
			service.Description,
			service.Category,
			MoneyValue.FromMinorUnits(
				service.BaseAmountMoney.MinorUnits,
				service.BaseAmountMoney.CurrencyCode,
				currencyCatalog).Amount,
			service.BaseAmountMoney.CurrencyCode);
	}
}
