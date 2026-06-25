using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases
{
	public sealed class GetServiceUseCase(IServiceRepository serviceRepository)
	{
		private readonly IServiceRepository _serviceRepository = serviceRepository;

		public async Task<Service?> ExecuteAsync(Guid serviceId)
			=> await _serviceRepository.GetByIdAsync(serviceId);
	}
}
