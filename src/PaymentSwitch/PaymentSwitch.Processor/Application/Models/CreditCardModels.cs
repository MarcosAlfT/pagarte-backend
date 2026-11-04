using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Models
{
	public sealed record UpdateCreditCardCommand(
		string ClientId,
		Guid CardId,
		string CardHolderName,
		bool IsDefault);

	public sealed record DeleteCreditCardCommand(
		string ClientId,
		Guid CardId);
}
