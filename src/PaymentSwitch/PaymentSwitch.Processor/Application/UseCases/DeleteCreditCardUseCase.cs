using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases
{
	public sealed class DeleteCreditCardUseCase(
		ICreditCardRepository creditCardRepository,
		IUnitOfWork unitOfWork)
	{
		private readonly ICreditCardRepository _creditCardRepository = creditCardRepository;
		private readonly IUnitOfWork _unitOfWork = unitOfWork;

		public async Task ExecuteAsync(
			DeleteCreditCardCommand request,
			CancellationToken cancellationToken = default)
		{
			await _creditCardRepository.DeleteAsync(request.CardId, request.ClientId);
			await _unitOfWork.SaveChangesAsync(cancellationToken);
		}
	}
}
