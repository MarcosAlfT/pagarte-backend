using RabbitMQ.Client;

namespace PaymentSwitch.Messaging
{
	public static class PaymentSwitchTopology
	{
		public static async Task DeclareAllAsync(IChannel channel)  // IModel Ã¢â€ â€™ IChannel
		{
			await channel.ExchangeDeclareAsync("dlx.payments",
				ExchangeType.Direct, durable: true);
			await channel.ExchangeDeclareAsync("dlx.notifications",
				ExchangeType.Direct, durable: true);

			await channel.ExchangeDeclareAsync(PaymentSwitchQueues.Exchanges.Payments,
				ExchangeType.Direct, durable: true);
			await channel.ExchangeDeclareAsync(PaymentSwitchQueues.Exchanges.Notifications,
				ExchangeType.Direct, durable: true);

			await channel.QueueDeclareAsync(PaymentSwitchQueues.DeadLetterQueues.PaymentRequest,
				durable: true, exclusive: false, autoDelete: false);
			await channel.QueueDeclareAsync(PaymentSwitchQueues.DeadLetterQueues.RefundRequest,
				durable: true, exclusive: false, autoDelete: false);
			await channel.QueueDeclareAsync(PaymentSwitchQueues.DeadLetterQueues.EmailSend,
				durable: true, exclusive: false, autoDelete: false);

			await channel.QueueBindAsync(PaymentSwitchQueues.DeadLetterQueues.PaymentRequest,
				"dlx.payments", PaymentSwitchQueues.Queues.PaymentRequest);
			await channel.QueueBindAsync(PaymentSwitchQueues.DeadLetterQueues.RefundRequest,
				"dlx.payments", PaymentSwitchQueues.Queues.RefundRequest);
			await channel.QueueBindAsync(PaymentSwitchQueues.DeadLetterQueues.EmailSend,
				"dlx.notifications", PaymentSwitchQueues.Queues.EmailSend);

			await channel.QueueDeclareAsync(PaymentSwitchQueues.Queues.PaymentRequest,
				durable: true, exclusive: false, autoDelete: false,
				arguments: new Dictionary<string, object?>
				{
					{ "x-dead-letter-exchange", "dlx.payments" },
					{ "x-dead-letter-routing-key", PaymentSwitchQueues.Queues.PaymentRequest }
				});

			await channel.QueueDeclareAsync(PaymentSwitchQueues.Queues.RefundRequest,
				durable: true, exclusive: false, autoDelete: false,
				arguments: new Dictionary<string, object?>
				{
					{ "x-dead-letter-exchange", "dlx.payments" },
					{ "x-dead-letter-routing-key", PaymentSwitchQueues.Queues.RefundRequest }
				});

			await channel.QueueDeclareAsync(PaymentSwitchQueues.Queues.EmailSend,
				durable: true, exclusive: false, autoDelete: false,
				arguments: new Dictionary<string, object?>
				{
					{ "x-dead-letter-exchange", "dlx.notifications" },
					{ "x-dead-letter-routing-key", PaymentSwitchQueues.Queues.EmailSend }
				});

			await channel.QueueDeclareAsync(PaymentSwitchQueues.Queues.AlertCreate,
				durable: true, exclusive: false, autoDelete: false);

			await channel.QueueBindAsync(PaymentSwitchQueues.Queues.PaymentRequest,
				PaymentSwitchQueues.Exchanges.Payments, PaymentSwitchQueues.Queues.PaymentRequest);
			await channel.QueueBindAsync(PaymentSwitchQueues.Queues.RefundRequest,
				PaymentSwitchQueues.Exchanges.Payments, PaymentSwitchQueues.Queues.RefundRequest);
			await channel.QueueBindAsync(PaymentSwitchQueues.Queues.EmailSend,
				PaymentSwitchQueues.Exchanges.Notifications, PaymentSwitchQueues.Queues.EmailSend);
			await channel.QueueBindAsync(PaymentSwitchQueues.Queues.AlertCreate,
				PaymentSwitchQueues.Exchanges.Notifications, PaymentSwitchQueues.Queues.AlertCreate);
		}
	}
}