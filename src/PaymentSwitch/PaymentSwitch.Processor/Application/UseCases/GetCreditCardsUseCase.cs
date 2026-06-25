using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases
{
	public sealed class GetCreditCardsUseCase(ICreditCardRepository creditCardRepository)
	{
		private readonly ICreditCardRepository _creditCardRepository =
			creditCardRepository;

		public async Task<IEnumerable<CreditCard>> ExecuteAsync(string clientId)
			=> await _creditCardRepository.GetByClientIdAsync(clientId);
	}
}
