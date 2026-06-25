using PaymentSwitch.Messaging;
using PaymentSwitch.Messaging.Messages;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;
using System.Text.Json;

namespace PaymentSwitch.Processor.Infrastructure.Outbox
{
	public sealed class PaymentRequestOutbox(
		PaymentDbContext dbContext,
		IClock clock)
		: IPaymentRequestOutbox
	{
		private readonly PaymentDbContext _dbContext = dbContext;
		private readonly IClock _clock = clock;

		public void AddPaymentRequest(
			Payment payment,
			PaymentQuote quote,
			string clientId)
		{
			var paymentRequestMessage = new PaymentRequestMessage
			{
				PaymentId = payment.Id,
				CompanyId = quote.Service.CompanyId,
				OperatorProvider = payment.OperatorProvider,
				OperatorPaymentId = payment.OperatorPaymentId!,
				Amount = quote.TotalAmount,
				Currency = quote.Currency,
				Reference = payment.Reference,
				ClientId = clientId,
				CreatedAt = _clock.UtcNow
			};

			var outboxMessage = OutboxMessage.Create(
				typeof(PaymentRequestMessage).FullName ?? nameof(PaymentRequestMessage),
				JsonSerializer.Serialize(paymentRequestMessage),
				PaymentSwitchQueues.Exchanges.Payments,
				PaymentSwitchQueues.Queues.PaymentRequest,
				_clock.UtcNow);

			_dbContext.OutboxMessages.Add(outboxMessage);
		}
	}
}
