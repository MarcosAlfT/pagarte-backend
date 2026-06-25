using Grpc.Core;
using PaymentSwitch.Contracts;
using PaymentSwitch.Processor.Application.UseCases;

namespace PaymentSwitch.Processor.GrpcServices
{
	public class ServiceCatalogGrpcService(
		GetServiceCatalogUseCase getServiceCatalogUseCase,
		GetServiceUseCase getServiceUseCase)
		: PaymentSwitch.Contracts.ServiceCatalogService.ServiceCatalogServiceBase
	{
		private readonly GetServiceCatalogUseCase _getServiceCatalogUseCase =
			getServiceCatalogUseCase;
		private readonly GetServiceUseCase _getServiceUseCase = getServiceUseCase;

		public override async Task<GetServicesResponse> GetServices(
			GetServicesRequest request, ServerCallContext context)
		{
			var services = await _getServiceCatalogUseCase.ExecuteAsync(
				string.IsNullOrEmpty(request.Category) ? null : request.Category);
			var response = new GetServicesResponse();
			response.Services.AddRange(services.Select(MapService));
			return response;
		}

		public override async Task<GetServiceResponse> GetService(
			GetServiceRequest request, ServerCallContext context)
		{
			var service = await _getServiceUseCase.ExecuteAsync(
				Guid.Parse(request.ServiceId));
			if (service == null)
				return new GetServiceResponse { Found = false };

			return new GetServiceResponse { Found = true, Service = MapService(service) };
		}

		private static ServiceDto MapService(Domain.Entities.Service service) =>
			new()
			{
				Id = service.Id.ToString(),
				Name = service.Name,
				Description = service.Description,
				Category = service.Category,
				BaseAmount = (double)service.BaseAmount,
				Currency = service.Currency
			};
	}
}
