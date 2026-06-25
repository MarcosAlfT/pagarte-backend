using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases
{
	public sealed class GetServiceCatalogUseCase(IServiceRepository serviceRepository)
	{
		private readonly IServiceRepository _serviceRepository = serviceRepository;

		public async Task<IEnumerable<Service>> ExecuteAsync(string? category)
			=> await _serviceRepository.GetAllActiveAsync(category);
	}
}
