using PaymentServices.Application.Abstractions;
using PaymentServices.Application.Models;
using Pagarte.Contracts;

namespace PaymentServices.Infrastructure.Clients;

public sealed class PagarteCompanyPaymentsClient(
	ServiceCatalogService.ServiceCatalogServiceClient serviceCatalogClient)
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
			(decimal)service.BaseAmount,
			service.Currency)).ToArray();

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
			(decimal)service.BaseAmount,
			service.Currency);
	}
}
