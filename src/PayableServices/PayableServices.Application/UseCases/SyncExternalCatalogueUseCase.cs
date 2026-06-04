using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PayableServices.Domain.Entities;
using PayableServices.Domain.Enums;

namespace PayableServices.Application.UseCases;

public sealed class SyncExternalCatalogueUseCase(
	ICompanyPaymentsClient companyPaymentsClient,
	IExternalCatalogueSourceRepository sourceRepository,
	IExternalCatalogueItemRepository itemRepository,
	IExternalCatalogueMappingRepository mappingRepository,
	IPayableServiceRepository payableServiceRepository,
	IPaymentRouteRepository paymentRouteRepository,
	IClock clock)
{
	public async Task<SyncExternalCatalogueResult> ExecuteAsync(
		SyncExternalCatalogueCommand request,
		CancellationToken cancellationToken = default)
	{
		var source = await sourceRepository.GetOrCreateAsync(
			request.SourceName,
			request.CountryId,
			cancellationToken);

		var catalogue = await companyPaymentsClient.GetCatalogueAsync(
			request.Category,
			cancellationToken);

		var seenExternalItemIds = new HashSet<Guid>();
		var syncedItems = 0;
		var mappedItems = 0;
		var reviewRequiredItems = 0;

		foreach (var service in catalogue.Services)
		{
			syncedItems++;
			seenExternalItemIds.Add(service.Id);

			var externalItem = await itemRepository.UpsertAsync(
				new ExternalCatalogueItem
				{
					Id = service.Id,
					ExternalCatalogueSourceId = source.Id,
					ExternalCategory = service.Category,
					ExternalSubcategory = string.Empty,
					ExternalName = service.Name,
					ExternalCode = service.Id.ToString(),
					ExternalStatus = "Active",
					IsAvailable = true,
					LastSeenAt = clock.UtcNow,
					RawReference = service.Description
				},
				cancellationToken);

			var payableService = await payableServiceRepository.GetByIdAsync(
				service.Id,
				cancellationToken);

			var route = payableService is null
				? null
				: await paymentRouteRepository.GetActiveByPayableServiceIdAsync(
					payableService.Id,
					cancellationToken);

			var mappingStatus = payableService is not null && route is not null
				? ExternalCatalogueMappingStatus.Mapped
				: ExternalCatalogueMappingStatus.ReviewRequired;

			await mappingRepository.UpsertAsync(
				new ExternalCatalogueMapping
				{
					Id = externalItem.Id,
					ExternalCatalogueItemId = externalItem.Id,
					PayableServiceId = payableService?.Id ?? Guid.Empty,
					PaymentRouteId = route?.Id ?? Guid.Empty,
					MappingStatus = mappingStatus,
					ReviewReason = mappingStatus == ExternalCatalogueMappingStatus.Mapped
						? null
						: "No active payable service route available for this external item."
				},
				cancellationToken);

			if (mappingStatus == ExternalCatalogueMappingStatus.Mapped)
			{
				mappedItems++;
			}
			else
			{
				reviewRequiredItems++;
			}
		}

		var inactivatedItems = await itemRepository.MarkUnavailableAsync(
			source.Id,
			seenExternalItemIds,
			clock.UtcNow,
			cancellationToken);

		return new SyncExternalCatalogueResult(
			true,
			syncedItems,
			mappedItems,
			reviewRequiredItems,
			inactivatedItems.Count,
			null);
	}
}
